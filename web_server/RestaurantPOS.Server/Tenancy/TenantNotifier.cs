using Microsoft.AspNetCore.SignalR;
using RestaurantPOS.Server.Hubs;

namespace RestaurantPOS.Server.Tenancy;

public interface ITenantNotifier
{
    Task BroadcastAsync(string eventName, object? data = null);
    Task BroadcastTableAsync(string tableNumber, string eventName, object? data = null);
    Task BroadcastTenantAsync(string tenantCode, string eventName, object? data = null);
}

public class TenantNotifier : ITenantNotifier
{
    private readonly IHubContext<PosHub> _hub;
    private readonly ITenantProvider _tenantProvider;

    public TenantNotifier(IHubContext<PosHub> hub, ITenantProvider tenantProvider)
    {
        _hub = hub;
        _tenantProvider = tenantProvider;
    }

    public async Task BroadcastAsync(string eventName, object? data = null)
    {
        var group = $"tenant_{_tenantProvider.CurrentTenantCode}";
        if (data != null)
        {
            await _hub.Clients.Group(group).SendAsync(eventName, data);
        }
        else
        {
            await _hub.Clients.Group(group).SendAsync(eventName);
        }
    }

    public async Task BroadcastTableAsync(string tableNumber, string eventName, object? data = null)
    {
        var tenantCode = _tenantProvider.CurrentTenantCode;
        var tableGroup = $"tenant_{tenantCode}_table_{tableNumber}";
        var tenantGroup = $"tenant_{tenantCode}";

        if (data != null)
        {
            await _hub.Clients.Group(tableGroup).SendAsync(eventName, data);
            await _hub.Clients.Group(tenantGroup).SendAsync(eventName, data);
        }
        else
        {
            await _hub.Clients.Group(tableGroup).SendAsync(eventName);
            await _hub.Clients.Group(tenantGroup).SendAsync(eventName);
        }
    }

    public async Task BroadcastTenantAsync(string tenantCode, string eventName, object? data = null)
    {
        var group = $"tenant_{tenantCode.Trim().ToUpperInvariant()}";
        if (data != null)
        {
            await _hub.Clients.Group(group).SendAsync(eventName, data);
        }
        else
        {
            await _hub.Clients.Group(group).SendAsync(eventName);
        }
    }
}
