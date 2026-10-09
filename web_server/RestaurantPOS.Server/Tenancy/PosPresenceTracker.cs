using System.Collections.Concurrent;
using RestaurantPOS.Shared.DTOs;

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

    /// <summary>
    /// Registers any connected client (POS, KDS, Cashier, QR Table, Management) as an active session.
    /// </summary>
    ActiveSessionDto RegisterSession(string connectionId, string tenantCode, string clientType, string? username, string? deviceName, string? ipAddress);

    /// <summary>
    /// Updates heartbeat timestamp for an active connection.
    /// </summary>
    void UpdateHeartbeat(string connectionId);

    /// <summary>
    /// Unregisters an active session.
    /// </summary>
    ActiveSessionDto? UnregisterSession(string connectionId);

    /// <summary>
    /// Returns all currently active sessions, optionally filtered by RPOS Code / TenantCode.
    /// </summary>
    IReadOnlyList<ActiveSessionDto> GetActiveSessions(string? tenantCode = null);

    /// <summary>
    /// Gets an active session by connection ID.
    /// </summary>
    ActiveSessionDto? GetSession(string connectionId);

    /// <summary>
    /// Removes/kicks a session by connection ID.
    /// </summary>
    bool RemoveSession(string connectionId);
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

    // ConnectionId -> ActiveSessionDto
    private readonly ConcurrentDictionary<string, ActiveSessionDto> _activeSessions = new();

    private readonly ILogger<PosPresenceTracker> _logger;

    public PosPresenceTracker(ILogger<PosPresenceTracker> logger)
    {
        _logger = logger;
        // Default sales demo stores to Online by default so standalone web demo works out of the box
        _simulatedStatus["DEFAULT"] = true;
        _simulatedStatus["RPOS-DEMO-0001"] = true;
    }

    public bool RegisterPos(string tenantCode, string connectionId, string? terminalName)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        var terminal = string.IsNullOrWhiteSpace(terminalName) ? "Windows POS" : terminalName.Trim();

        _connectionTenantMap[connectionId] = cleanCode;

        var terminals = _tenantTerminals.GetOrAdd(cleanCode, _ => new ConcurrentDictionary<string, string>());
        var wasEmpty = terminals.IsEmpty;

        terminals[connectionId] = terminal;

        // Also update or add session info
        if (_activeSessions.TryGetValue(connectionId, out var session))
        {
            session.StoreCode = cleanCode;
            session.ClientType = "Windows POS";
            session.DeviceName = terminal;
            session.LastHeartbeat = DateTime.UtcNow;
            session.IsActive = true;
        }
        else
        {
            _activeSessions[connectionId] = new ActiveSessionDto
            {
                ConnectionId = connectionId,
                StoreCode = cleanCode,
                ClientType = "Windows POS",
                Username = "Cashier",
                Role = "Cashier",
                DeviceName = terminal,
                IpAddress = "127.0.0.1",
                ConnectedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                IsActive = true
            };
        }

        _logger.LogInformation("[POS Presence] Client PC '{TerminalName}' ({ConnectionId}) registered for store '{StoreCode}'. Active terminals: {Count}",
            terminal, connectionId, cleanCode, terminals.Count);

        return wasEmpty;
    }

    public (string? TenantCode, bool BecameOffline) UnregisterPos(string connectionId)
    {
        UnregisterSession(connectionId);

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

        if (cleanCode.StartsWith("RPOS-DEMO-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
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

    public ActiveSessionDto RegisterSession(string connectionId, string tenantCode, string clientType, string? username, string? deviceName, string? ipAddress)
    {
        var cleanCode = string.IsNullOrWhiteSpace(tenantCode) ? "DEFAULT" : tenantCode.Trim().ToUpperInvariant();
        var session = new ActiveSessionDto
        {
            ConnectionId = connectionId,
            StoreCode = cleanCode,
            ClientType = string.IsNullOrWhiteSpace(clientType) ? "Web App" : clientType,
            Username = string.IsNullOrWhiteSpace(username) ? "ผู้ใช้งาน" : username,
            Role = !string.IsNullOrWhiteSpace(username) && username.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User",
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Web Browser" : deviceName,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            ConnectedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            IsActive = true
        };

        _activeSessions[connectionId] = session;
        _connectionTenantMap[connectionId] = cleanCode;

        _logger.LogInformation("[Session Tracker] New active session registered: {ConnectionId} | Store: {StoreCode} | Type: {ClientType} | User: {User}",
            connectionId, cleanCode, session.ClientType, session.Username);

        return session;
    }

    public void UpdateHeartbeat(string connectionId)
    {
        if (_activeSessions.TryGetValue(connectionId, out var session))
        {
            session.LastHeartbeat = DateTime.UtcNow;
            session.IsActive = true;
        }
    }

    public ActiveSessionDto? UnregisterSession(string connectionId)
    {
        if (_activeSessions.TryRemove(connectionId, out var session))
        {
            session.IsActive = false;
            _logger.LogInformation("[Session Tracker] Session ended: {ConnectionId} | Store: {StoreCode} | Type: {Type}",
                connectionId, session.StoreCode, session.ClientType);
            return session;
        }
        return null;
    }

    public IReadOnlyList<ActiveSessionDto> GetActiveSessions(string? tenantCode = null)
    {
        // Purge dead sessions older than 3 minutes without heartbeat
        var cutoff = DateTime.UtcNow.AddMinutes(-3);
        foreach (var kvp in _activeSessions)
        {
            if (kvp.Value.LastHeartbeat < cutoff)
            {
                _activeSessions.TryRemove(kvp.Key, out _);
            }
        }

        var list = _activeSessions.Values.ToList();
        if (!string.IsNullOrWhiteSpace(tenantCode) && !tenantCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var clean = tenantCode.Trim().ToUpperInvariant();
            list = list.Where(s => s.StoreCode.Equals(clean, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return list.OrderByDescending(s => s.ConnectedAt).ToList();
    }

    public ActiveSessionDto? GetSession(string connectionId)
    {
        return _activeSessions.TryGetValue(connectionId, out var session) ? session : null;
    }

    public bool RemoveSession(string connectionId)
    {
        UnregisterPos(connectionId);
        return _activeSessions.TryRemove(connectionId, out _);
    }
}
