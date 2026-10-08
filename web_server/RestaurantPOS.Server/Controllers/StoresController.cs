using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Server.Tenancy;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Events;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantProvider _tenantProvider;
    private readonly AppDbContext _db;
    private readonly IPosPresenceTracker _presenceTracker;
    private readonly ITenantNotifier _notifier;
    private readonly ILogger<StoresController> _logger;

    public StoresController(
        ITenantService tenantService,
        ITenantProvider tenantProvider,
        AppDbContext db,
        IPosPresenceTracker presenceTracker,
        ITenantNotifier notifier,
        ILogger<StoresController> logger)
    {
        _tenantService = tenantService;
        _tenantProvider = tenantProvider;
        _db = db;
        _presenceTracker = presenceTracker;
        _notifier = notifier;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> RegisterStore([FromBody] RegisterTenantRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.StoreCode) || string.IsNullOrWhiteSpace(request.StoreName))
            {
                return BadRequest(ApiResponse<TenantDto>.Fail("กรุณากรอกรหัสร้านค้าและชื่อร้านค้า", ErrorCodes.ValidationError));
            }

            var result = await _tenantService.RegisterTenantAsync(request);
            _logger.LogInformation("[Store] Registered new restaurant store: {StoreCode} - {StoreName}", result.StoreCode, result.StoreName);
            return Ok(ApiResponse<TenantDto>.Ok(result, "ลงทะเบียนร้านค้าใหม่และสร้างฐานข้อมูลประจำร้านสำเร็จ"));
        }
        catch (ArgumentException aex)
        {
            return BadRequest(ApiResponse<TenantDto>.Fail(aex.Message, ErrorCodes.ValidationError));
        }
        catch (InvalidOperationException ioex)
        {
            return Conflict(ApiResponse<TenantDto>.Fail(ioex.Message, ErrorCodes.Conflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Store] Error registering store: {StoreCode}", request.StoreCode);
            return StatusCode(500, ApiResponse<TenantDto>.Fail("เกิดข้อผิดพลาดในการลงทะเบียนร้านค้า: " + ex.Message, ErrorCodes.ServerError));
        }
    }

    [HttpGet("generate-code")]
    public ActionResult<ApiResponse<object>> GenerateStoreCode()
    {
        var code = _tenantService.GenerateSecureStoreCode();
        return Ok(ApiResponse<object>.Ok(new { code }));
    }

    [HttpGet("check/{storeCode}")]
    public async Task<ActionResult<ApiResponse<StoreInfoResponse>>> CheckStore(string storeCode)
    {
        var tenant = await _tenantService.GetTenantAsync(storeCode);
        if (tenant == null)
        {
            return NotFound(ApiResponse<StoreInfoResponse>.Fail($"ไม่พบรหัสร้านค้า '{storeCode}' ในระบบ", ErrorCodes.NotFound));
        }

        if (!tenant.IsActive)
        {
            return BadRequest(ApiResponse<StoreInfoResponse>.Fail($"ร้านค้า '{tenant.StoreName}' ถูกระงับการใช้งานชั่วคราว", ErrorCodes.UserDisabled));
        }

        // Get count from that store's isolated DB
        int tableCount = 0;
        int productCount = 0;
        int billCount = 0;
        try
        {
            using var storeDb = _tenantService.CreateTenantDbContext(tenant.StoreCode);
            tableCount = await storeDb.Tables.CountAsync();
            productCount = await storeDb.Products.CountAsync(p => p.IsAvailable);
            billCount = await storeDb.Orders.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Store] Could not query store DB stats for {StoreCode}", tenant.StoreCode);
        }

        bool isExpired = false;
        int daysRemaining = 9999;
        if (tenant.SubscriptionPlan != "FullLifetime" && tenant.ExpiresAt.HasValue)
        {
            var diff = tenant.ExpiresAt.Value - DateTime.UtcNow;
            daysRemaining = (int)Math.Max(0, Math.Ceiling(diff.TotalDays));
            isExpired = DateTime.UtcNow > tenant.ExpiresAt.Value;
        }

        return Ok(ApiResponse<StoreInfoResponse>.Ok(new StoreInfoResponse
        {
            StoreCode = tenant.StoreCode,
            StoreName = tenant.StoreName,
            OwnerName = tenant.OwnerName,
            OwnerPhone = tenant.OwnerPhone,
            Address = tenant.Address,
            IsActive = tenant.IsActive,
            TableCount = tableCount,
            ProductCount = productCount,
            CreatedAt = tenant.CreatedAt,
            ExpiresAt = tenant.ExpiresAt,
            SubscriptionPlan = tenant.SubscriptionPlan,
            DaysRemaining = daysRemaining,
            IsExpired = isExpired,
            TotalBillCount = billCount,
            IsPosOnline = _presenceTracker.IsPosOnline(tenant.StoreCode),
            ActivePosCount = _presenceTracker.GetOnlineTerminalCount(tenant.StoreCode)
        }));
    }

    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<StoreInfoResponse>>> GetCurrentStoreInfo()
    {
        var currentCode = _tenantProvider.CurrentTenantCode;
        var tenant = await _tenantService.GetTenantAsync(currentCode);

        var storeName = tenant?.StoreName ?? "ร้านอาหาร Restaurant POS";
        var tableCount = await _db.Tables.CountAsync();
        var productCount = await _db.Products.CountAsync(p => p.IsAvailable);
        var billCount = await _db.Orders.CountAsync();

        bool isExpired = false;
        int daysRemaining = 9999;
        var plan = tenant?.SubscriptionPlan ?? "Trial";
        var expiresAt = tenant?.ExpiresAt;

        if (plan != "FullLifetime" && expiresAt.HasValue)
        {
            var diff = expiresAt.Value - DateTime.UtcNow;
            daysRemaining = (int)Math.Max(0, Math.Ceiling(diff.TotalDays));
            isExpired = DateTime.UtcNow > expiresAt.Value;
        }

        return Ok(ApiResponse<StoreInfoResponse>.Ok(new StoreInfoResponse
        {
            StoreCode = currentCode,
            StoreName = storeName,
            OwnerName = tenant?.OwnerName ?? "",
            OwnerPhone = tenant?.OwnerPhone ?? "",
            Address = tenant?.Address ?? "",
            IsActive = tenant?.IsActive ?? true,
            TableCount = tableCount,
            ProductCount = productCount,
            CreatedAt = tenant?.CreatedAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt,
            SubscriptionPlan = plan,
            DaysRemaining = daysRemaining,
            IsExpired = isExpired,
            TotalBillCount = billCount,
            IsPosOnline = _presenceTracker.IsPosOnline(currentCode),
            ActivePosCount = _presenceTracker.GetOnlineTerminalCount(currentCode)
        }));
    }

    private bool IsDevAuthorized()
    {
        var devKey = Request.Headers["X-Dev-Key"].ToString();
        if (devKey == "rpos_dev_master_2026" || devKey == "dev2026" || devKey == "9999")
        {
            return true;
        }

        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip != null && System.Net.IPAddress.IsLoopback(ip))
        {
            return true;
        }

        return false;
    }

    [HttpPost("status/simulate")]
    public async Task<ActionResult<ApiResponse<StoreStatusDto>>> SimulateStoreStatus([FromBody] SimulateStoreStatusRequest request)
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<StoreStatusDto>.Fail("ไม่อนุญาต: ฟังก์ชันจำลองสถานะสำหรับโหมดนักพัฒนาเท่านั้น", ErrorCodes.Unauthorized));
        }

        var tenantCode = string.IsNullOrWhiteSpace(request.StoreCode)
            ? _tenantProvider.CurrentTenantCode
            : request.StoreCode.Trim().ToUpperInvariant();

        _presenceTracker.SetSimulatedStatus(tenantCode, request.IsOnline);

        var isOnline = _presenceTracker.IsPosOnline(tenantCode);
        var activeCount = _presenceTracker.GetOnlineTerminalCount(tenantCode);
        var statusDto = new StoreStatusDto
        {
            StoreCode = tenantCode,
            IsOnline = isOnline,
            ActivePosCount = activeCount,
            Message = isOnline ? "ร้านเปิดให้บริการแล้ว (โหมดจำลอง)" : "ร้านปิดรับออเดอร์แล้ว (โหมดจำลอง)",
            Timestamp = DateTime.UtcNow
        };

        await _notifier.BroadcastTenantAsync(tenantCode, HubEvents.StoreStatusChanged, statusDto);
        return Ok(ApiResponse<StoreStatusDto>.Ok(statusDto, "อัปเดตสถานะจำลองของร้านค้าเรียบร้อยแล้ว"));
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<List<TenantDto>>>> GetAllStores()
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<List<TenantDto>>.Fail("ไม่อนุญาต: ต้องใช้สิทธิ์นักพัฒนาในการเข้าถึงรายชื่อร้านค้า", ErrorCodes.Unauthorized));
        }

        var list = await _tenantService.GetAllTenantsAsync();
        return Ok(ApiResponse<List<TenantDto>>.Ok(list));
    }

    [HttpPost("reset-clean")]
    public async Task<ActionResult<ApiResponse<object>>> ResetAllStoresExceptDefault()
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<object>.Fail("ไม่อนุญาต: ต้องใช้สิทธิ์นักพัฒนาในการรีเซ็ตล้างข้อมูลร้านค้า", ErrorCodes.Unauthorized));
        }

        try
        {
            var count = await _tenantService.ResetAllStoresExceptDefaultAsync();
            return Ok(ApiResponse<object>.Ok(new { deletedCount = count, preservedStore = "DEFAULT" }, $"รีเซ็ตระบบและลบร้านค้าทดสอบทั้งหมด {count} ร้านเรียบร้อยแล้ว คงเหลือเฉพาะร้านค้าตัวอย่าง (DEFAULT)"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stores] Error resetting stores");
            return StatusCode(500, ApiResponse<object>.Fail("เกิดข้อผิดพลาดในการรีเซ็ตร้านค้า: " + ex.Message, ErrorCodes.ServerError));
        }
    }


    [HttpPost("license/upgrade")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> UpgradeLicense([FromBody] UpgradeLicenseRequest request)
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<TenantDto>.Fail("ไม่อนุญาต: ต้องใช้สิทธิ์นักพัฒนาในการอัปเกรดสิทธิ์ร้านค้า", ErrorCodes.Unauthorized));
        }

        try
        {
            var updated = await _tenantService.UpdateTenantLicenseAsync(request.StoreCode, request.Plan, request.ExtendDays);
            if (updated == null)
            {
                return NotFound(ApiResponse<TenantDto>.Fail($"ไม่พบร้านค้า '{request.StoreCode}'", ErrorCodes.NotFound));
            }
            return Ok(ApiResponse<TenantDto>.Ok(updated, "อัปเดตสิทธิ์การใช้งานร้านค้าสำเร็จ"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[License] Error upgrading license for {StoreCode}", request.StoreCode);
            return StatusCode(500, ApiResponse<TenantDto>.Fail("เกิดข้อผิดพลาดในการอัปเดตสิทธิ์: " + ex.Message, ErrorCodes.ServerError));
        }
    }

    [HttpPost("license/activate")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> ActivateLicense([FromBody] ActivateLicenseRequest request)
    {
        try
        {
            var updated = await _tenantService.ActivateTenantWithKeyAsync(request.StoreCode, request.LicenseKey);
            if (updated == null)
            {
                return NotFound(ApiResponse<TenantDto>.Fail($"ไม่พบร้านค้า '{request.StoreCode}'", ErrorCodes.NotFound));
            }
            return Ok(ApiResponse<TenantDto>.Ok(updated, "เปิดใช้งานโปรแกรมด้วย Activation Key สำเร็จ"));
        }
        catch (ArgumentException aex)
        {
            return BadRequest(ApiResponse<TenantDto>.Fail(aex.Message, ErrorCodes.ValidationError));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[License] Error activating license for {StoreCode}", request.StoreCode);
            return StatusCode(500, ApiResponse<TenantDto>.Fail("เกิดข้อผิดพลาดในการเปิดใช้งาน: " + ex.Message, ErrorCodes.ServerError));
        }
    }

    [HttpPost("license/generate-key")]
    public ActionResult<ApiResponse<GenerateKeyResponse>> GenerateKey([FromBody] UpgradeLicenseRequest request)
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<GenerateKeyResponse>.Fail("ไม่อนุญาต: ต้องใช้สิทธิ์นักพัฒนาในการสร้าง Activation Key", ErrorCodes.Unauthorized));
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.StoreCode))
            {
                return BadRequest(ApiResponse<GenerateKeyResponse>.Fail("กรุณากรอกรหัสร้านค้า", ErrorCodes.ValidationError));
            }
            var keyInfo = _tenantService.GenerateActivationKey(request.StoreCode, request.Plan);
            return Ok(ApiResponse<GenerateKeyResponse>.Ok(keyInfo, "สร้าง Activation Key สำเร็จ"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<GenerateKeyResponse>.Fail("เกิดข้อผิดพลาดในการสร้างรหัสเปิดใช้งาน: " + ex.Message, ErrorCodes.ServerError));
        }
    }

    [HttpPost("license/toggle-status")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> ToggleStoreStatus([FromBody] TenantDto request)
    {
        if (!IsDevAuthorized())
        {
            return Unauthorized(ApiResponse<TenantDto>.Fail("ไม่อนุญาต: ต้องใช้สิทธิ์นักพัฒนาในการปรับสถานะร้านค้า", ErrorCodes.Unauthorized));
        }

        try
        {
            var updated = await _tenantService.SetTenantActiveStatusAsync(request.StoreCode, request.IsActive);
            if (updated == null)
            {
                return NotFound(ApiResponse<TenantDto>.Fail($"ไม่พบร้านค้า '{request.StoreCode}'", ErrorCodes.NotFound));
            }
            return Ok(ApiResponse<TenantDto>.Ok(updated, $"เปลี่ยนสถานะร้านค้าเป็น {(request.IsActive ? "[เปิดใช้งาน]" : "[ระงับสิทธิ์]")} สำเร็จ"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<TenantDto>.Fail("เกิดข้อผิดพลาดในการเปลี่ยนสถานะร้านค้า: " + ex.Message, ErrorCodes.ServerError));
        }
    }
}
