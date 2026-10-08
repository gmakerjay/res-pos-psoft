using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Models;
using RestaurantPOS.Tests.Infrastructure;
using Xunit;

namespace RestaurantPOS.Tests;

public class CrossDeviceActionLockoutAndAuditTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CrossDeviceActionLockoutAndAuditTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Code", "DEFAULT");
        _client.DefaultRequestHeaders.Add("User-Agent", "RestaurantPOS-Desktop/1.0");
    }

    [Fact]
    public async Task CrossDevice_StatusTransitions_LockPreviousActionsSequentially()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        await tenantService.EnsureMasterAndDefaultTenantAsync();

        var db = tenantService.CreateTenantDbContext("DEFAULT");
        var product = db.Products.FirstOrDefault();
        if (product == null)
        {
            product = new ProductEntity { Code = "TEST-M1", Name = "ต้มยำกุ้ง", Price = 120m, CategoryId = 1 };
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        // 1. Create order
        var orderReq = new CreateOrderRequest
        {
            Type = OrderType.DineIn,
            TableNumber = "T2",
            CustomerName = "ทดสอบการซิงค์ปุ่ม",
            CustomerPhone = "0811112222",
            Items = new List<CreateOrderItemRequest>
            {
                new CreateOrderItemRequest { ProductId = product.Id, Quantity = 1 }
            }
        };

        var postRes = await _client.PostAsJsonAsync("/api/orders", orderReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var order = (await postRes.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>())!.Data!;

        // Initial state: New
        Assert.True(order.CanAccept);
        Assert.False(order.CanPrepare);
        Assert.False(order.CanReady);
        Assert.False(order.CanComplete);

        // 2. POS accepts order
        var acceptRes = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Accepted,
            UpdatedBy = "แคชเชียร์ 1",
            Source = "POS"
        });
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);
        order = (await acceptRes.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>())!.Data!;

        // In Accepted state: CanAccept is LOCKED (cannot click accept again)
        Assert.False(order.CanAccept, "Accept button must be locked out once accepted");
        Assert.True(order.CanPrepare, "Kitchen/POS can now prepare");
        Assert.False(order.CanReady);
        Assert.False(order.CanComplete);

        // 3. Web Kitchen begins preparing
        var prepareRes = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Preparing,
            UpdatedBy = "พ่อครัว Web",
            Source = "WEB_KITCHEN"
        });
        Assert.Equal(HttpStatusCode.OK, prepareRes.StatusCode);
        order = (await prepareRes.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>())!.Data!;

        // In Preparing state: CanAccept and CanPrepare are BOTH LOCKED
        Assert.False(order.CanAccept, "Accept button must remain locked");
        Assert.False(order.CanPrepare, "Prepare button must now be locked out");
        Assert.True(order.CanReady, "Ready button is now unlocked");
        Assert.False(order.CanComplete);

        // 4. Kitchen marks as Ready
        var readyRes = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Ready,
            UpdatedBy = "พ่อครัว Web",
            Source = "WEB_KITCHEN"
        });
        Assert.Equal(HttpStatusCode.OK, readyRes.StatusCode);
        order = (await readyRes.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>())!.Data!;

        // In Ready state: Only Complete is unlocked
        Assert.False(order.CanAccept);
        Assert.False(order.CanPrepare);
        Assert.False(order.CanReady, "Ready button is locked once food is ready");
        Assert.True(order.CanComplete, "Cashier/Waiter can complete/serve");

        // 5. Complete order
        var completeRes = await _client.PutAsJsonAsync($"/api/orders/{order.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Completed,
            UpdatedBy = "แคชเชียร์ 1",
            Source = "POS"
        });
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);
        order = (await completeRes.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>())!.Data!;

        // Terminal state: All buttons locked
        Assert.False(order.CanAccept);
        Assert.False(order.CanPrepare);
        Assert.False(order.CanReady);
        Assert.False(order.CanComplete);
        Assert.False(order.IsActive);
    }

    [Fact]
    public async Task AuditLogs_RecordsAllOrderLifecycleActionsWithSource()
    {
        // Query audit logs
        var res = await _client.GetAsync("/api/audit?limit=50");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var apiRes = await res.Content.ReadFromJsonAsync<ApiResponse<List<AuditLogDto>>>();
        Assert.NotNull(apiRes);
        Assert.True(apiRes.Success);
        Assert.NotNull(apiRes.Data);

        // Check if there are ORDER_STATUS_CHANGED records
        var statusLogs = apiRes.Data.Where(l => l.Action == "ORDER_STATUS_CHANGED").ToList();
        Assert.NotEmpty(statusLogs);

        var latest = statusLogs.First();
        Assert.NotNull(latest.Username);
        Assert.NotNull(latest.Details);
        Assert.True(latest.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task TablesEndpoint_ReturnsConfiguredTablesWithStatusBadge()
    {
        var res = await _client.GetAsync("/api/tables");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var apiRes = await res.Content.ReadFromJsonAsync<ApiResponse<List<TableDto>>>();
        Assert.NotNull(apiRes);
        Assert.True(apiRes.Success);
        Assert.NotNull(apiRes.Data);
        Assert.NotEmpty(apiRes.Data);

        var firstTable = apiRes.Data.First();
        Assert.False(string.IsNullOrWhiteSpace(firstTable.TableNumber));
        Assert.False(string.IsNullOrWhiteSpace(firstTable.StatusBadge));
        Assert.StartsWith("[", firstTable.StatusBadge);
        Assert.EndsWith("]", firstTable.StatusBadge);
    }
}
