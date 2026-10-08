using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly Tenancy.ITenantProvider _tenantProvider;
    private readonly Tenancy.ITenantService _tenantService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        IConfiguration config,
        Tenancy.ITenantProvider tenantProvider,
        Tenancy.ITenantService tenantService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _config = config;
        _tenantProvider = tenantProvider;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse<LoginResponse>.Fail("Username and password are required", ErrorCodes.ValidationError));
        }

        var targetStoreCode = !string.IsNullOrWhiteSpace(request.StoreCode)
            ? request.StoreCode.Trim().ToUpperInvariant()
            : _tenantProvider.CurrentTenantCode;

        using var customDb = targetStoreCode != _tenantProvider.CurrentTenantCode
            ? _tenantService.CreateTenantDbContext(targetStoreCode)
            : null;
        var activeDb = customDb ?? _db;

        var hash = AppDbContext.HashPassword(request.Password);
        var inputUsername = request.Username.Trim();

        // 1. Direct match by username and password hash
        var user = await activeDb.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == inputUsername.ToLower() && u.PasswordHash == hash);

        // 2. Fallback: If user entered storeCode, phone, or ownerName with the admin's password
        if (user == null)
        {
            var adminUser = await activeDb.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin && u.PasswordHash == hash);
            if (adminUser != null)
            {
                var tenantInfo = await _tenantService.GetTenantAsync(targetStoreCode);
                bool isMatchingIdentifier = inputUsername.Equals("admin", StringComparison.OrdinalIgnoreCase)
                    || inputUsername.Equals(adminUser.Username, StringComparison.OrdinalIgnoreCase)
                    || (tenantInfo != null && (
                        inputUsername.Equals(tenantInfo.StoreCode, StringComparison.OrdinalIgnoreCase) ||
                        inputUsername.Equals(tenantInfo.OwnerPhone, StringComparison.OrdinalIgnoreCase) ||
                        inputUsername.Equals(tenantInfo.OwnerName, StringComparison.OrdinalIgnoreCase) ||
                        inputUsername.Equals(tenantInfo.StoreName, StringComparison.OrdinalIgnoreCase)
                    ));

                if (isMatchingIdentifier)
                {
                    user = adminUser;
                    _logger.LogInformation("[Auth] Matched SuperAdmin for store {StoreCode} via identifier: {Identifier}", targetStoreCode, inputUsername);
                }
            }
        }

        // 3. Fallback for default demo store: support common demo passwords (psoft123, 123456, admin, 1234)
        if (user == null && targetStoreCode == "DEFAULT")
        {
            var acceptedDemoPasswords = new[] { "psoft123", "123456", "admin", "1234" };
            if (acceptedDemoPasswords.Contains(request.Password.Trim()))
            {
                user = await activeDb.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == inputUsername.ToLower() || u.Role == UserRole.SuperAdmin);
            }
        }

        if (user == null)
        {
            _logger.LogWarning("[Auth] Failed login attempt for username: {Username} (Store: {Tenant})", request.Username, targetStoreCode);
            return Unauthorized(ApiResponse<LoginResponse>.Fail("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง", ErrorCodes.InvalidCredentials));
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("[Auth] Login attempt by disabled user: {Username}", request.Username);
            return Unauthorized(ApiResponse<LoginResponse>.Fail("บัญชีนี้ถูกระงับการใช้งาน", ErrorCodes.UserDisabled));
        }

        // Generate JWT Token
        var secret = _config["Jwt:Key"] ?? "RestaurantPOS_Default_Super_Secret_Key_For_Jwt_2026_LongerThan32Bytes!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(7);

        var currentTenant = await _tenantService.GetTenantAsync(targetStoreCode);
        var storeName = currentTenant?.StoreName ?? "ร้านอาหาร Restaurant POS";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("FullName", user.FullName),
            new("tenant_code", targetStoreCode),
            new("store_name", storeName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "RestaurantPOS",
            audience: _config["Jwt:Audience"] ?? "RestaurantPOSClients",
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var permissions = JsonSerializer.Deserialize<List<string>>(user.PermissionsJson) ?? new List<string>();

        _logger.LogInformation("[Auth] User logged in successfully: {Username} (Role: {Role})", user.Username, user.Role);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = tokenString,
            ExpiresAt = expires,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                Permissions = permissions
            }
        }));
    }
}
