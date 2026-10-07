using Microsoft.AspNetCore.SignalR;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Events;

namespace RestaurantPOS.Server.Hubs;

public class PosHub : Hub
{
    private readonly ILogger<PosHub> _logger;
    private readonly IPosPresenceTracker _presenceTracker;
    private readonly ITenantNotifier _notifier;

    public PosHub(ILogger<PosHub> logger, IPosPresenceTracker presenceTracker, ITenantNotifier notifier)
    {
        _logger = logger;
        _presenceTracker = presenceTracker;
        _notifier = notifier;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var tenantCode = http?.Request.Query["tenant"].ToString();
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            tenantCode = http?.Request.Headers["X-Tenant-Code"].ToString();
        }
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            tenantCode = Context.User?.FindFirst("tenant_code")?.Value;
        }
        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            tenantCode = "DEFAULT";
        }

        tenantCode = tenantCode.Trim().ToUpperInvariant();
        Context.Items["TenantCode"] = tenantCode;

        // Auto join tenant-scoped group
        var tenantGroupName = $"tenant_{tenantCode}";
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantGroupName);

        _logger.LogInformation("[SignalR] Client connected: {ConnectionId} (Store: {TenantCode})", Context.ConnectionId, tenantCode);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (tenantCode, becameOffline) = _presenceTracker.UnregisterPos(Context.ConnectionId);
        if (!string.IsNullOrEmpty(tenantCode) && becameOffline)
        {
            var statusDto = new StoreStatusDto
            {
                StoreCode = tenantCode,
                IsOnline = false,
                ActivePosCount = 0,
                Message = "เครื่องแคชเชียร์หน้าร้านปิดการเชื่อมต่อแล้ว (ร้านยังไม่เปิดรับออเดอร์)",
                Timestamp = DateTime.UtcNow
            };
            await _notifier.BroadcastTenantAsync(tenantCode, HubEvents.StoreStatusChanged, statusDto);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "[SignalR] Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("[SignalR] Client disconnected gracefully: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RegisterPos(string? terminalName)
    {
        var tenantCode = Context.Items["TenantCode"]?.ToString() ?? "DEFAULT";
        var becameOnline = _presenceTracker.RegisterPos(tenantCode, Context.ConnectionId, terminalName);
        _logger.LogInformation("[SignalR] Connection {ConnectionId} registered as POS for {TenantCode}. BecameOnline: {BecameOnline}",
            Context.ConnectionId, tenantCode, becameOnline);

        if (becameOnline)
        {
            var statusDto = new StoreStatusDto
            {
                StoreCode = tenantCode,
                IsOnline = true,
                ActivePosCount = _presenceTracker.GetOnlineTerminalCount(tenantCode),
                Message = "เครื่องแคชเชียร์หน้าร้านเปิดให้บริการแล้ว (พร้อมรับออเดอร์)",
                Timestamp = DateTime.UtcNow
            };
            await _notifier.BroadcastTenantAsync(tenantCode, HubEvents.StoreStatusChanged, statusDto);
        }
    }

    public async Task UnregisterPos()
    {
        var (tenantCode, becameOffline) = _presenceTracker.UnregisterPos(Context.ConnectionId);
        if (!string.IsNullOrEmpty(tenantCode) && becameOffline)
        {
            var statusDto = new StoreStatusDto
            {
                StoreCode = tenantCode,
                IsOnline = false,
                ActivePosCount = 0,
                Message = "เครื่องแคชเชียร์หน้าร้านปิดการเชื่อมต่อแล้ว (ร้านยังไม่เปิดรับออเดอร์)",
                Timestamp = DateTime.UtcNow
            };
            await _notifier.BroadcastTenantAsync(tenantCode, HubEvents.StoreStatusChanged, statusDto);
        }
    }

    public async Task JoinTableGroup(string tableNumber)
    {
        var tenantCode = Context.Items["TenantCode"]?.ToString() ?? "DEFAULT";
        var groupName = $"tenant_{tenantCode}_table_{tableNumber}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("[SignalR] Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveTableGroup(string tableNumber)
    {
        var tenantCode = Context.Items["TenantCode"]?.ToString() ?? "DEFAULT";
        var groupName = $"tenant_{tenantCode}_table_{tableNumber}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("[SignalR] Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task JoinRoleGroup(string role)
    {
        var tenantCode = Context.Items["TenantCode"]?.ToString() ?? "DEFAULT";
        var groupName = $"tenant_{tenantCode}_role_{role.ToLower()}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("[SignalR] Client {ConnectionId} joined role group {GroupName}", Context.ConnectionId, groupName);
    }
}
