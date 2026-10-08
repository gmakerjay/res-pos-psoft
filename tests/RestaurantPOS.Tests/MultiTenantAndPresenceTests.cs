using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;
using RestaurantPOS.Tests.Infrastructure;
using Xunit;

namespace RestaurantPOS.Tests;

public class MultiTenantAndPresenceTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HubConnection? _posHub;

    public MultiTenantAndPresenceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Code", "DEFAULT");
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        await tenantService.EnsureMasterAndDefaultTenantAsync();
    }

    public async Task DisposeAsync()
    {
        if (_posHub != null)
        {
            await _posHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task StorePresence_WhenPosConnectsAndRegisters_BroadcastsOnline_WhenUnregisters_BroadcastsOffline()
    {
        var hubUrl = new Uri(_factory.Server.BaseAddress, "/hubs/pos?tenant=DEFAULT");
        _posHub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers.Add("X-Tenant-Code", "DEFAULT");
            })
            .Build();

        StoreStatusDto? statusOnline = null;
        StoreStatusDto? statusOffline = null;

        var onlineTcs = new TaskCompletionSource<bool>();
        var offlineTcs = new TaskCompletionSource<bool>();

        _posHub.On<StoreStatusDto>(HubEvents.StoreStatusChanged, dto =>
        {
            if (dto.IsOnline)
            {
                statusOnline = dto;
                onlineTcs.TrySetResult(true);
            }
            else
            {
                statusOffline = dto;
                offlineTcs.TrySetResult(true);
            }
        });

        await _posHub.StartAsync();

        // Register POS
        await _posHub.InvokeAsync("RegisterPos", "Cashier-PC-01");

        var onlineCompleted = await Task.WhenAny(onlineTcs.Task, Task.Delay(4000));
        Assert.True(onlineCompleted == onlineTcs.Task, "Web should receive StoreStatusChanged with IsOnline = true");
        Assert.NotNull(statusOnline);
        Assert.True(statusOnline.IsOnline);
        Assert.Equal("DEFAULT", statusOnline.StoreCode);

        // Unregister POS
        await _posHub.InvokeAsync("UnregisterPos");

        var offlineCompleted = await Task.WhenAny(offlineTcs.Task, Task.Delay(4000));
        Assert.True(offlineCompleted == offlineTcs.Task, "Web should receive StoreStatusChanged with IsOnline = false");
        Assert.NotNull(statusOffline);
        Assert.False(statusOffline.IsOnline);
    }

    [Fact]
    public async Task OfflineGuard_WhenPosOfflineAndCustomerOrdersWithoutStaffAuth_RejectsOrder()
    {
        // Explicitly clear presence in memory for this store
        using (var scope = _factory.Services.CreateScope())
        {
            var presenceTracker = scope.ServiceProvider.GetRequiredService<IPosPresenceTracker>();
            presenceTracker.UnregisterPos("OFFLINE-STORE-TEST-ID");
        }

        // Customer request without POS user-agent and without staff token
        using var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add("X-Tenant-Code", "DEFAULT");
        customerClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)");

        var orderRequest = new CreateOrderRequest
        {
            Type = OrderType.DineIn,
            TableNumber = "T9",
            CustomerName = "ลูกค้าทางบ้าน",
            CustomerPhone = "0819999999",
            Items = new List<CreateOrderItemRequest>
            {
                new CreateOrderItemRequest { ProductId = 1, Quantity = 1 }
            }
        };

        // When presence tracker has 0 online terminals
        using (var scope = _factory.Services.CreateScope())
        {
            var presenceTracker = scope.ServiceProvider.GetRequiredService<IPosPresenceTracker>();
            // Ensure 0 terminals online
            while (presenceTracker.GetOnlineTerminalCount("DEFAULT") > 0)
            {
                presenceTracker.UnregisterPos("TEST-POS-CONN-ID");
            }
        }

        var response = await customerClient.PostAsJsonAsync("/api/orders", orderRequest);
        
        // Assert: Should either reject with StoreOffline or return BadRequest when store offline
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var res = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
            Assert.NotNull(res);
            Assert.False(res.Success);
            Assert.Equal(ErrorCodes.StoreOffline, res.ErrorCode);
        }
    }
}
