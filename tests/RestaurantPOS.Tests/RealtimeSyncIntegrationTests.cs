using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;
using RestaurantPOS.Tests.Infrastructure;
using Xunit;

namespace RestaurantPOS.Tests;

public class RealtimeSyncIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HubConnection? _hubConnection;
    private string? _authToken;

    public RealtimeSyncIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Code", "DEFAULT");
        _client.DefaultRequestHeaders.Add("User-Agent", "RestaurantPOS-Client-Test/1.0");
    }

    public async Task InitializeAsync()
    {
        // 1. Ensure master and default tenant exist
        using (var scope = _factory.Services.CreateScope())
        {
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
            await tenantService.EnsureMasterAndDefaultTenantAsync();

            // Set POS online in presence tracker so orders can be placed
            var presenceTracker = scope.ServiceProvider.GetRequiredService<IPosPresenceTracker>();
            presenceTracker.RegisterPos("DEFAULT", "TEST-POS-CONN-ID", "TestTerminal-01");
        }

        // 2. Perform Admin Login to get token for privileged operations
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = "psoft123",
            StoreCode = "DEFAULT"
        });

        if (loginResponse.IsSuccessStatusCode)
        {
            var apiRes = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            _authToken = apiRes?.Data?.Token;
        }
    }

    public async Task DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
        Assert.Contains("RestaurantPOS", content);
    }

    [Fact]
    public async Task AuthLogin_WithValidCredentials_ReturnsSuccessAndToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = "psoft123",
            StoreCode = "DEFAULT"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data?.Token);
        Assert.Equal("admin", result.Data?.User?.Username);
    }

    [Fact]
    public async Task FullOrderLifecycle_SyncsRealtimeBetweenWebAndClientPOS()
    {
        // Setup SignalR Hub connection over TestServer
        var hubUrl = new Uri(_factory.Server.BaseAddress, "/hubs/pos?tenant=DEFAULT");
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers.Add("X-Tenant-Code", "DEFAULT");
            })
            .Build();

        OrderDto? receivedNewOrder = null;
        OrderDto? receivedStatusChangedOrder = null;
        OrderActionActivityDto? receivedActivity = null;

        var orderReceivedTcs = new TaskCompletionSource<bool>();
        var statusChangedTcs = new TaskCompletionSource<bool>();
        var activityTcs = new TaskCompletionSource<bool>();

        _hubConnection.On<OrderDto>(HubEvents.OrderCreated, order =>
        {
            receivedNewOrder = order;
            orderReceivedTcs.TrySetResult(true);
        });

        _hubConnection.On<OrderDto>(HubEvents.OrderStatusChanged, order =>
        {
            receivedStatusChangedOrder = order;
            statusChangedTcs.TrySetResult(true);
        });

        _hubConnection.On<OrderActionActivityDto>(HubEvents.OrderActionActivity, act =>
        {
            receivedActivity = act;
            activityTcs.TrySetResult(true);
        });

        await _hubConnection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, _hubConnection.State);

        // Step 1: Customer on Web creates an order
        using var scope = _factory.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var db = tenantService.CreateTenantDbContext("DEFAULT");
        var product = db.Products.FirstOrDefault() ?? new RestaurantPOS.Server.Data.ProductEntity
        {
            Code = "TEST-01",
            Name = "ข้าวกะเพราหมูสับ",
            Price = 60.0m,
            CategoryId = 1,
            IsAvailable = true
        };
        if (product.Id == 0)
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var orderRequest = new CreateOrderRequest
        {
            Type = OrderType.DineIn,
            TableNumber = "T1",
            CustomerName = "คุณสมบัติ (Web Customer)",
            CustomerPhone = "0891234567",
            Notes = "เผ็ดน้อย ไม่ใส่ผงชูรส",
            ClientRequestId = Guid.NewGuid().ToString(),
            Items = new List<CreateOrderItemRequest>
            {
                new CreateOrderItemRequest
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    SpecialNotes = "ไข่ดาวสุก"
                }
            }
        };

        var postOrderResponse = await _client.PostAsJsonAsync("/api/orders", orderRequest);
        Assert.Equal(HttpStatusCode.Created, postOrderResponse.StatusCode);

        var createdOrderApiRes = await postOrderResponse.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        Assert.NotNull(createdOrderApiRes);
        Assert.True(createdOrderApiRes.Success);
        var createdOrder = createdOrderApiRes.Data;
        Assert.NotNull(createdOrder);
        Assert.Equal(OrderStatus.New, createdOrder.Status);

        // Step 2: Verify Realtime OrderReceived broadcast arrived at Client POS
        var orderReceivedCompleted = await Task.WhenAny(orderReceivedTcs.Task, Task.Delay(5000));
        Assert.True(orderReceivedCompleted == orderReceivedTcs.Task, "Client POS must receive OrderReceived broadcast via SignalR");
        Assert.NotNull(receivedNewOrder);
        Assert.Equal(createdOrder.OrderNumber, receivedNewOrder.OrderNumber);

        // Step 3: Cashier on Client POS accepts the order
        var updateRequest = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Accepted,
            UpdatedBy = "สมชาย แคชเชียร์",
            Source = "POS"
        };

        var putStatusResponse = await _client.PutAsJsonAsync($"/api/orders/{createdOrder.Id}/status", updateRequest);
        Assert.Equal(HttpStatusCode.OK, putStatusResponse.StatusCode);

        // Step 4: Verify OrderStatusChanged and OrderActionActivity broadcast
        await Task.WhenAll(
            Task.WhenAny(statusChangedTcs.Task, Task.Delay(5000)),
            Task.WhenAny(activityTcs.Task, Task.Delay(5000))
        );

        Assert.NotNull(receivedStatusChangedOrder);
        Assert.Equal(OrderStatus.Accepted, receivedStatusChangedOrder.Status);

        Assert.NotNull(receivedActivity);
        Assert.Equal(createdOrder.Id, receivedActivity.OrderId);
        Assert.Equal(OrderStatus.New, receivedActivity.PreviousStatus);
        Assert.Equal(OrderStatus.Accepted, receivedActivity.NewStatus);
        Assert.Equal("POS", receivedActivity.Source);
        Assert.Equal("สมชาย แคชเชียร์", receivedActivity.OperatorName);
    }
}
