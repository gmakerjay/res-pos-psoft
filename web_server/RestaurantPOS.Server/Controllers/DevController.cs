using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Settings;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevController : ControllerBase
{
    private static readonly DateTime ServerStartTime = DateTime.UtcNow;
    private readonly IPosPresenceTracker _presenceTracker;
    private readonly ITenantNotifier _notifier;
    private readonly ITenantService _tenantService;
    private readonly MasterDbContext _masterDb;
    private readonly PlatformSettings _platformSettings;
    private readonly ILogger<DevController> _logger;

    public DevController(
        IPosPresenceTracker presenceTracker,
        ITenantNotifier notifier,
        ITenantService tenantService,
        MasterDbContext masterDb,
        IOptions<PlatformSettings> platformSettings,
        ILogger<DevController> logger)
    {
        _presenceTracker = presenceTracker;
        _notifier = notifier;
        _tenantService = tenantService;
        _masterDb = masterDb;
        _platformSettings = platformSettings.Value;
        _logger = logger;
    }

    private bool IsDevAuthorized()
    {
        // Standalone Turnkey Edition (Single-Store Package) strictly forbids DEV access
        if (_platformSettings.ServerMode.Equals("Standalone", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Request.Query.TryGetValue("mode", out var qMode) && qMode == "standalone")
        {
            return false;
        }

        if (Request.Headers["X-Request-Mode"].ToString().Equals("standalone", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Request.Headers["Referer"].ToString().Contains("/standalone", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Request.Headers.TryGetValue("X-Dev-Key", out var devKey) && devKey == "rpos_dev_master_2026")
        {
            return true;
        }

        if (User.Identity?.IsAuthenticated == true && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin")))
        {
            return true;
        }

        // Allow query param dev_key for direct dev testing
        if (Request.Query.TryGetValue("dev_key", out var qKey) && qKey == "rpos_dev_master_2026")
        {
            return true;
        }

        return false;
    }

    [HttpGet("sessions")]
    public ActionResult<ApiResponse<IReadOnlyList<ActiveSessionDto>>> GetActiveSessions([FromQuery] string? store)
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<IReadOnlyList<ActiveSessionDto>>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        var sessions = _presenceTracker.GetActiveSessions(store);
        return Ok(ApiResponse<IReadOnlyList<ActiveSessionDto>>.Ok(sessions));
    }

    [HttpPost("sessions/{connectionId}/kick")]
    public async Task<ActionResult<ApiResponse<object>>> KickSession(string connectionId, [FromBody] DevActionRequest? req)
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<object>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        var session = _presenceTracker.GetSession(connectionId);
        _presenceTracker.RemoveSession(connectionId);

        if (session != null)
        {
            await _notifier.BroadcastTenantAsync(session.StoreCode, HubEvents.SessionKicked, new { ConnectionId = connectionId, Reason = req?.Message ?? "Session terminated by DEV admin" });
        }

        _logger.LogInformation("[DEV] Session {ConnectionId} kicked by operator", connectionId);
        return Ok(ApiResponse<object>.Ok(new { kicked = true, connectionId }));
    }

    [HttpGet("diagnostics")]
    public async Task<ActionResult<ApiResponse<ServerDiagnosticsDto>>> GetDiagnostics()
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<ServerDiagnosticsDto>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        var proc = Process.GetCurrentProcess();
        var memMb = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 2);
        var uptime = DateTime.UtcNow - ServerStartTime;
        var uptimeStr = $"{(int)uptime.TotalHours} ชั่วโมง {uptime.Minutes} นาที {uptime.Seconds} วินาที";

        int totalTenants = 1;
        try
        {
            totalTenants = await _masterDb.Tenants.CountAsync();
        }
        catch
        {
            // fallback
        }

        var sessions = _presenceTracker.GetActiveSessions();
        var posCount = sessions.Count(s => s.ClientType.Contains("POS", StringComparison.OrdinalIgnoreCase));

        var diag = new ServerDiagnosticsDto
        {
            ServerMode = _platformSettings.ServerMode,
            AllowRegistration = _platformSettings.AllowStoreRegistration,
            StandaloneRPOSCode = _platformSettings.StandaloneRPOSCode,
            Uptime = uptimeStr,
            MemoryUsageMb = memMb,
            TotalTenants = totalTenants,
            ActiveSessionsCount = sessions.Count,
            ActivePosTerminalsCount = posCount,
            ServerTimeUtc = DateTime.UtcNow,
            ServerTimeLocal = DateTime.UtcNow.AddHours(7),
            OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            DotNetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        };

        return Ok(ApiResponse<ServerDiagnosticsDto>.Ok(diag));
    }

    [HttpPost("force-sync")]
    public async Task<ActionResult<ApiResponse<object>>> ForceSync([FromBody] DevActionRequest? req)
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<object>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        if (!string.IsNullOrWhiteSpace(req?.TargetStoreCode) && !req.TargetStoreCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var clean = req.TargetStoreCode.Trim().ToUpperInvariant();
            await _notifier.BroadcastTenantAsync(clean, HubEvents.ForceSync, new { StoreCode = clean, Timestamp = DateTime.UtcNow });
            _logger.LogInformation("[DEV] Force Sync broadcasted to store: {StoreCode}", clean);
        }
        else
        {
            await _notifier.BroadcastAllAsync(HubEvents.ForceSync, new { StoreCode = "ALL", Timestamp = DateTime.UtcNow });
            _logger.LogInformation("[DEV] Force Sync broadcasted to ALL connected clients");
        }

        return Ok(ApiResponse<object>.Ok(new { success = true, broadcastAt = DateTime.UtcNow }));
    }

    [HttpPost("clear-cache")]
    public ActionResult<ApiResponse<object>> ClearCache()
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<object>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _logger.LogInformation("[DEV] Garbage collection and memory cache cleared");
        return Ok(ApiResponse<object>.Ok(new { success = true, memoryCleared = true, timestamp = DateTime.UtcNow }));
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<ApiResponse<object>>> GetAllTenantsSummary()
    {
        if (!IsDevAuthorized())
        {
            return StatusCode(403, ApiResponse<object>.Fail("ไม่มีสิทธิ์เข้าถึงส่วนควบคุมระดับวิศวกรรม (DEV Access Required)", ErrorCodes.Forbidden));
        }

        var tenants = await _masterDb.Tenants.OrderByDescending(t => t.CreatedAt).ToListAsync();
        var results = new List<object>();

        foreach (var t in tenants)
        {
            int tables = 0;
            int products = 0;
            int orders = 0;
            long dbBytes = 0;

            try
            {
                var dbPath = System.IO.Path.Combine("tenants", $"{t.StoreCode}.db");
                if (System.IO.File.Exists(dbPath))
                {
                    dbBytes = new System.IO.FileInfo(dbPath).Length;
                }

                using var storeDb = _tenantService.CreateTenantDbContext(t.StoreCode);
                tables = await storeDb.Tables.CountAsync();
                products = await storeDb.Products.CountAsync();
                orders = await storeDb.Orders.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DEV] Error reading store DB for {StoreCode}", t.StoreCode);
            }

            var isOnline = _presenceTracker.IsPosOnline(t.StoreCode);
            var terminals = _presenceTracker.GetOnlineTerminalCount(t.StoreCode);

            results.Add(new
            {
                t.Id,
                t.StoreCode,
                t.StoreName,
                t.OwnerName,
                t.OwnerPhone,
                t.IsActive,
                t.CreatedAt,
                t.SubscriptionPlan,
                Tables = tables,
                Products = products,
                Orders = orders,
                DbSizeKb = Math.Round(dbBytes / 1024.0, 1),
                IsOnline = isOnline,
                ActiveTerminals = terminals
            });
        }

        return Ok(ApiResponse<object>.Ok(results));
    }
}
