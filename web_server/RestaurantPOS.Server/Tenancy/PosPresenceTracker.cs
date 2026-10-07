using System.Collections.Concurrent;

namespace RestaurantPOS.Server.Tenancy;

public interface IPosPresenceTracker
{
    /// <summary>
    /// Registers a Client PC connection as an active POS terminal for a tenant.
    /// Returns true if the store transitioned from Offline to Online.
    /// </summary>
    bool RegisterPos(string tenantCode, string connectionId, string? terminalName);

    /// <summary>
    /// Unregisters a connection when disconnected.
    /// Returns the tenant code and whether the store transitioned from Online to Offline.
    /// </summary>
    (string? TenantCode, bool BecameOffline) UnregisterPos(string connectionId);

    /// <summary>
    /// Checks whether the store currently has at least one active Client PC connected.
    /// </summary>
    bool IsPosOnline(string tenantCode);

    /// <summary>
    /// Returns the number of currently connected Client PC terminals for the store.
    /// </summary>
    int GetOnlineTerminalCount(string tenantCode);

    /// <summary>
    /// Sets an explicit simulated online status (useful for Sales Demo / Presentation mode).
    /// Pass null to remove simulation and return to real presence detection.
    /// </summary>
    void SetSimulatedStatus(string tenantCode, bool? isOnline);

    /// <summary>
    /// Gets the current simulated status, if any.
    /// </summary>
    bool? GetSimulatedStatus(string tenantCode);
}

public class PosPresenceTracker : IPosPresenceTracker
{
    // TenantCode -> (ConnectionId -> TerminalName)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _tenantTerminals =
        new(StringComparer.OrdinalIgnoreCase);

    // ConnectionId -> TenantCode
    private readonly ConcurrentDictionary<string, string> _connectionTenantMap = new();

    // TenantCode -> Simulated IsOnline
    private readonly ConcurrentDictionary<string, bool> _simulatedStatus =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<PosPresenceTracker> _logger;

    public PosPresenceTracker(ILogger<PosPresenceTracker> logger)
    {
        _logger = logger;
        // Default sales demo store to Online by default so standalone web demo works out of the box
        _simulatedStatus["DEFAULT"] = true;
    }

    public bool RegisterPos(string tenantCode, string connectionId, string? terminalName)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        var terminal = string.IsNullOrWhiteSpace(terminalName) ? "Windows POS" : terminalName.Trim();

        _connectionTenantMap[connectionId] = cleanCode;

        var terminals = _tenantTerminals.GetOrAdd(cleanCode, _ => new ConcurrentDictionary<string, string>());
        var wasEmpty = terminals.IsEmpty;

        terminals[connectionId] = terminal;

        _logger.LogInformation("[POS Presence] Client PC '{TerminalName}' ({ConnectionId}) registered for store '{StoreCode}'. Active terminals: {Count}",
            terminal, connectionId, cleanCode, terminals.Count);

        return wasEmpty;
    }

    public (string? TenantCode, bool BecameOffline) UnregisterPos(string connectionId)
    {
        if (_connectionTenantMap.TryRemove(connectionId, out var tenantCode))
        {
            if (_tenantTerminals.TryGetValue(tenantCode, out var terminals))
            {
                terminals.TryRemove(connectionId, out var terminalName);
                var isNowEmpty = terminals.IsEmpty;

                _logger.LogInformation("[POS Presence] Client PC '{TerminalName}' ({ConnectionId}) disconnected from store '{StoreCode}'. Remaining active: {Count}",
                    terminalName ?? "Unknown", connectionId, tenantCode, terminals.Count);

                return (tenantCode, isNowEmpty);
            }
            return (tenantCode, false);
        }

        return (null, false);
    }

    public bool IsPosOnline(string tenantCode)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();

        // If there is an active real POS connection, that takes precedence
        if (_tenantTerminals.TryGetValue(cleanCode, out var terminals) && !terminals.IsEmpty)
        {
            return true;
        }

        // Check if simulation is enabled
        if (_simulatedStatus.TryGetValue(cleanCode, out var simulated))
        {
            return simulated;
        }

        return false;
    }

    public int GetOnlineTerminalCount(string tenantCode)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        if (_tenantTerminals.TryGetValue(cleanCode, out var terminals))
        {
            return terminals.Count;
        }
        return 0;
    }

    public void SetSimulatedStatus(string tenantCode, bool? isOnline)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        if (isOnline.HasValue)
        {
            _simulatedStatus[cleanCode] = isOnline.Value;
            _logger.LogInformation("[POS Presence] Simulated status for '{StoreCode}' set to: {Status}", cleanCode, isOnline.Value);
        }
        else
        {
            _simulatedStatus.TryRemove(cleanCode, out _);
            _logger.LogInformation("[POS Presence] Simulated status for '{StoreCode}' reset to real presence", cleanCode);
        }
    }

    public bool? GetSimulatedStatus(string tenantCode)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        return _simulatedStatus.TryGetValue(cleanCode, out var val) ? val : null;
    }
}
