using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RestaurantPOS.Server.Data;
using RestaurantPOS.Shared.DTOs;

namespace RestaurantPOS.Server.Tenancy;

public class TenantService : ITenantService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TenantService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dbProvider;

    public TenantService(
        IConfiguration config,
        ILogger<TenantService> logger,
        IServiceProvider serviceProvider)
    {
        _config = config;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _dbProvider = _config["DatabaseProvider"] ?? "Sqlite";
    }

    public string GetTenantConnectionString(string storeCode)
    {
        var code = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        if (_dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            var baseConn = _config.GetConnectionString("DefaultConnection") 
                           ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(baseConn)
            {
                Database = $"restaurantpos_{code.ToLowerInvariant()}"
            };
            return builder.ConnectionString;
        }

        // SQLite per tenant
        Directory.CreateDirectory("tenants");
        return $"Data Source=tenants/{code}.db";
    }

    public AppDbContext CreateTenantDbContext(string storeCode)
    {
        var connStr = GetTenantConnectionString(storeCode);
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        if (_dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connStr);
        }
        else
        {
            optionsBuilder.UseSqlite(connStr);
        }
        return new AppDbContext(optionsBuilder.Options);
    }

    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task EnsureMasterAndDefaultTenantAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            Directory.CreateDirectory("tenants");

            using var scope = _serviceProvider.CreateScope();
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            await masterDb.Database.EnsureCreatedAsync();

        // 1. Ensure DEFAULT tenant in Master DB
        var defaultTenant = await masterDb.Tenants.FirstOrDefaultAsync(t => t.StoreCode == "DEFAULT");
        if (defaultTenant == null)
        {
            defaultTenant = new TenantEntity
            {
                StoreCode = "DEFAULT",
                StoreName = "ร้านอาหาร Restaurant POS (สาขาหลัก)",
                OwnerName = "ผู้ดูแลระบบ",
                OwnerPhone = "02-000-0000",
                AdminUsername = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                SubscriptionPlan = "Enterprise"
            };
            masterDb.Tenants.Add(defaultTenant);
            await masterDb.SaveChangesAsync();
            _logger.LogInformation("[Tenancy] Seeded DEFAULT tenant in Master Catalog");
        }

        // 2. Ensure physical database for DEFAULT tenant
        if (!_dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists("tenants/DEFAULT.db") && File.Exists("restaurantpos.db"))
            {
                try
                {
                    File.Copy("restaurantpos.db", "tenants/DEFAULT.db", true);
                    _logger.LogInformation("[Tenancy] Migrated existing restaurantpos.db to tenants/DEFAULT.db");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Tenancy] Could not copy existing restaurantpos.db to tenants/DEFAULT.db");
                }
            }
        }

        using var defaultDb = CreateTenantDbContext("DEFAULT");
        await defaultDb.Database.EnsureCreatedAsync();
        await MigrateTenantDbAsync(defaultDb, _dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase));
        defaultDb.SeedInitialData("admin", "psoft123", "ร้านหลัก", isDemoStore: true);

        // Migrate all existing tenants if any
        var allTenants = await masterDb.Tenants.ToListAsync();
        foreach (var t in allTenants)
        {
            if (t.StoreCode == "DEFAULT") continue;
            try
            {
                using var tDb = CreateTenantDbContext(t.StoreCode);
                await tDb.Database.EnsureCreatedAsync();
                await MigrateTenantDbAsync(tDb, _dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Tenancy] Could not migrate tenant db for {StoreCode}", t.StoreCode);
            }
        }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public static async Task MigrateTenantDbAsync(AppDbContext tenantDb, bool isPostgres)
    {
        string[] alterStatements;
        if (isPostgres)
        {
            alterStatements = new[]
            {
                "ALTER TABLE \"Orders\" ADD COLUMN IF NOT EXISTS \"TableNumber\" text;",
                "ALTER TABLE \"Tables\" ADD COLUMN IF NOT EXISTS \"ReservationCustomerName\" text;",
                "ALTER TABLE \"Tables\" ADD COLUMN IF NOT EXISTS \"ReservationCustomerPhone\" text;",
                "ALTER TABLE \"Tables\" ADD COLUMN IF NOT EXISTS \"ReservationTime\" timestamp with time zone;",
                "ALTER TABLE \"Tables\" ADD COLUMN IF NOT EXISTS \"ReservationPartySize\" integer;",
                "ALTER TABLE \"Tables\" ADD COLUMN IF NOT EXISTS \"ReservationNotes\" text;"
            };
        }
        else
        {
            alterStatements = new[]
            {
                "ALTER TABLE Orders ADD COLUMN TableNumber TEXT;",
                "ALTER TABLE Tables ADD COLUMN ReservationCustomerName TEXT;",
                "ALTER TABLE Tables ADD COLUMN ReservationCustomerPhone TEXT;",
                "ALTER TABLE Tables ADD COLUMN ReservationTime TEXT;",
                "ALTER TABLE Tables ADD COLUMN ReservationPartySize INTEGER;",
                "ALTER TABLE Tables ADD COLUMN ReservationNotes TEXT;"
            };
        }

        foreach (var sql in alterStatements)
        {
            try
            {
                await tenantDb.Database.ExecuteSqlRawAsync(sql);
            }
            catch
            {
                // Ignored if column already exists or already up-to-date
            }
        }
    }

    public async Task<TenantEntity?> GetTenantAsync(string storeCode)
    {
        var code = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        return await masterDb.Tenants.FirstOrDefaultAsync(t => t.StoreCode == code);
    }

    public async Task<List<TenantDto>> GetAllTenantsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var list = await masterDb.Tenants.OrderBy(t => t.Id).ToListAsync();
        return list.Select(t => new TenantDto
        {
            Id = t.Id,
            StoreCode = t.StoreCode,
            StoreName = t.StoreName,
            OwnerName = t.OwnerName,
            OwnerPhone = t.OwnerPhone,
            Email = t.Email,
            Address = t.Address,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            ExpiresAt = t.ExpiresAt,
            SubscriptionPlan = t.SubscriptionPlan
        }).ToList();
    }

    public async Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest request)
    {
        var code = string.IsNullOrWhiteSpace(request.StoreCode)
            ? GenerateSecureStoreCode()
            : request.StoreCode.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(code, "^[A-Z0-9_-]{3,30}$"))
        {
            throw new ArgumentException("รหัสร้านค้าต้องเป็นตัวอักษรภาษาอังกฤษ ตัวเลข ขีดกลาง หรืออันเดอร์สกอร์ ความยาว 3-30 ตัวอักษร");
        }

        if (string.IsNullOrWhiteSpace(request.StoreName))
        {
            throw new ArgumentException("กรุณากรอกชื่อร้านอาหาร");
        }

        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        var exists = await masterDb.Tenants.AnyAsync(t => t.StoreCode == code);
        if (exists)
        {
            throw new InvalidOperationException($"รหัสร้านค้า '{code}' มีอยู่ในระบบแล้ว กรุณาเลือกรหัสร้านค้าใหม่");
        }

        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Trim().Length < 4)
        {
            throw new ArgumentException("กรุณากำหนดรหัสผ่านสำหรับเข้าสู่ระบบอย่างน้อย 4 ตัวอักษร");
        }

        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.AdminPassword.Trim() != request.ConfirmPassword.Trim())
        {
            throw new ArgumentException("รหัสผ่านและยืนยันรหัสผ่านไม่ตรงกัน กรุณาตรวจสอบอีกครั้ง");
        }

        var adminUser = string.IsNullOrWhiteSpace(request.AdminUsername) ? "admin" : request.AdminUsername.Trim();
        var adminPass = request.AdminPassword.Trim();

        var tenant = new TenantEntity
        {
            StoreCode = code,
            StoreName = request.StoreName.Trim(),
            OwnerName = request.OwnerName.Trim(),
            OwnerPhone = request.OwnerPhone.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            AdminUsername = adminUser,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            SubscriptionPlan = "Trial"
        };

        masterDb.Tenants.Add(tenant);
        await masterDb.SaveChangesAsync();
        _logger.LogInformation("[Tenancy] Registered new tenant: {StoreCode} ({StoreName}) with 14-day trial", code, tenant.StoreName);

        // Provision dedicated database for this store
        using (var tenantDb = CreateTenantDbContext(code))
        {
            await tenantDb.Database.EnsureCreatedAsync();
            await MigrateTenantDbAsync(tenantDb, _dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase));
            tenantDb.SeedInitialData(adminUser, adminPass, tenant.StoreName, isDemoStore: false);
            _logger.LogInformation("[Tenancy] Successfully initialized isolated database for store: {StoreCode}", code);
        }

        return new TenantDto
        {
            Id = tenant.Id,
            StoreCode = tenant.StoreCode,
            StoreName = tenant.StoreName,
            OwnerName = tenant.OwnerName,
            OwnerPhone = tenant.OwnerPhone,
            Email = tenant.Email,
            Address = tenant.Address,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            ExpiresAt = tenant.ExpiresAt,
            SubscriptionPlan = tenant.SubscriptionPlan
        };
    }

    private const string KeySecret = "RestaurantPOS_Licensing_Master_Secret_Salt_2026";
    private static readonly char[] Base32Chars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ".ToCharArray();

    public string GenerateSecureStoreCode()
    {
        var bytes = new byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var sb = new System.Text.StringBuilder("RPOS-");
        for (int i = 0; i < 12; i++)
        {
            sb.Append(Base32Chars[bytes[i] % Base32Chars.Length]);
            if (i == 3 || i == 7) sb.Append('-');
        }
        return sb.ToString();
    }

    public GenerateKeyResponse GenerateActivationKey(string storeCode, string plan)
    {
        var code = storeCode.Trim().ToUpperInvariant();
        var planTag = plan switch
        {
            "FullLifetime" or "Lifetime" => "LFE",
            "FullYearly" or "Yearly" => "YRL",
            _ => "EXT"
        };

        var signature = ComputeKeySignature(code, planTag);
        var licenseKey = $"ACT-{planTag}-{signature.Substring(0, 4)}-{signature.Substring(4, 4)}";

        var planDesc = planTag switch
        {
            "LFE" => "เวอร์ชันเต็ม ตลอดชีพ (Full Lifetime License)",
            "YRL" => "เวอร์ชันเต็ม รายปี 1 ปี (Full Yearly License)",
            _ => "ต่ออายุการใช้งาน 30 วัน (Extend 30 Days)"
        };

        return new GenerateKeyResponse
        {
            StoreCode = code,
            Plan = plan,
            LicenseKey = licenseKey,
            MessageTemplate = $"[รหัสปลดล็อกสิทธิ์ใช้งาน Restaurant POS]\nรหัสร้าน: {code}\nสิทธิ์: {planDesc}\nรหัสเปิดใช้งาน (Activation Key): {licenseKey}\n(กรุณานำรหัสนี้ไปกรอกในหน้าต่างเปิดใช้งานของโปรแกรม POS หรือระบบจัดการร้าน)"
        };
    }

    private string ComputeKeySignature(string storeCode, string planTag)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(KeySecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{storeCode}:{planTag}"));
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            sb.Append(Base32Chars[hash[i] % Base32Chars.Length]);
        }
        return sb.ToString();
    }

    public async Task<TenantDto?> ActivateTenantWithKeyAsync(string storeCode, string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(storeCode) || string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("กรุณากรอกรหัสร้านค้าและรหัสเปิดใช้งาน");
        }

        var code = storeCode.Trim().ToUpperInvariant();
        var cleanKey = licenseKey.Trim().ToUpperInvariant().Replace(" ", "");
        var parts = cleanKey.Split('-');
        if (parts.Length != 4 || parts[0] != "ACT")
        {
            throw new ArgumentException("รูปแบบรหัสเปิดใช้งานไม่ถูกต้อง (ต้องเป็น ACT-XXX-XXXX-XXXX)");
        }

        var planTag = parts[1];
        var providedSignature = parts[2] + parts[3];
        var expectedSignature = ComputeKeySignature(code, planTag);

        if (!string.Equals(providedSignature, expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("รหัสเปิดใช้งานไม่ถูกต้องสำหรับรหัสร้านค้านี้");
        }

        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenant = await masterDb.Tenants.FirstOrDefaultAsync(t => t.StoreCode == code);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"ไม่พบร้านค้า '{code}' ในระบบ");
        }

        tenant.IsActive = true;
        if (planTag == "LFE")
        {
            tenant.SubscriptionPlan = "FullLifetime";
            tenant.ExpiresAt = null;
        }
        else if (planTag == "YRL")
        {
            tenant.SubscriptionPlan = "FullYearly";
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt.Value > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;
            tenant.ExpiresAt = baseDate.AddYears(1);
        }
        else // EXT
        {
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt.Value > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;
            tenant.ExpiresAt = baseDate.AddDays(30);
        }

        await masterDb.SaveChangesAsync();
        _logger.LogInformation("[Licensing] Activated store {StoreCode} with plan {Plan}, expires at {ExpiresAt}",
            code, tenant.SubscriptionPlan, tenant.ExpiresAt?.ToString("yyyy-MM-dd") ?? "Never");

        return new TenantDto
        {
            Id = tenant.Id,
            StoreCode = tenant.StoreCode,
            StoreName = tenant.StoreName,
            OwnerName = tenant.OwnerName,
            OwnerPhone = tenant.OwnerPhone,
            Email = tenant.Email,
            Address = tenant.Address,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            ExpiresAt = tenant.ExpiresAt,
            SubscriptionPlan = tenant.SubscriptionPlan
        };
    }

    public async Task<TenantDto?> UpdateTenantLicenseAsync(string storeCode, string plan, int addDays)
    {
        var code = storeCode.Trim().ToUpperInvariant();
        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenant = await masterDb.Tenants.FirstOrDefaultAsync(t => t.StoreCode == code);
        if (tenant == null) return null;

        tenant.IsActive = true;
        if (plan == "FullLifetime")
        {
            tenant.SubscriptionPlan = "FullLifetime";
            tenant.ExpiresAt = null;
        }
        else if (plan == "FullYearly")
        {
            tenant.SubscriptionPlan = "FullYearly";
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt.Value > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;
            tenant.ExpiresAt = baseDate.AddYears(1);
        }
        else if (plan == "Trial")
        {
            tenant.SubscriptionPlan = "Trial";
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt.Value > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;
            tenant.ExpiresAt = baseDate.AddDays(addDays > 0 ? addDays : 14);
        }
        else if (addDays > 0)
        {
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt.Value > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;
            tenant.ExpiresAt = baseDate.AddDays(addDays);
        }

        await masterDb.SaveChangesAsync();
        _logger.LogInformation("[Licensing] Updated license for store {StoreCode}: Plan={Plan}, ExpiresAt={ExpiresAt}",
            code, tenant.SubscriptionPlan, tenant.ExpiresAt);

        return new TenantDto
        {
            Id = tenant.Id,
            StoreCode = tenant.StoreCode,
            StoreName = tenant.StoreName,
            OwnerName = tenant.OwnerName,
            OwnerPhone = tenant.OwnerPhone,
            Email = tenant.Email,
            Address = tenant.Address,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            ExpiresAt = tenant.ExpiresAt,
            SubscriptionPlan = tenant.SubscriptionPlan
        };
    }

    public async Task<TenantDto?> SetTenantActiveStatusAsync(string storeCode, bool isActive)
    {
        var code = storeCode.Trim().ToUpperInvariant();
        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenant = await masterDb.Tenants.FirstOrDefaultAsync(t => t.StoreCode == code);
        if (tenant == null) return null;

        tenant.IsActive = isActive;
        await masterDb.SaveChangesAsync();
        _logger.LogInformation("[Licensing] Changed active status for store {StoreCode} to {IsActive}", code, isActive);

        return new TenantDto
        {
            Id = tenant.Id,
            StoreCode = tenant.StoreCode,
            StoreName = tenant.StoreName,
            OwnerName = tenant.OwnerName,
            OwnerPhone = tenant.OwnerPhone,
            Email = tenant.Email,
            Address = tenant.Address,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            ExpiresAt = tenant.ExpiresAt,
            SubscriptionPlan = tenant.SubscriptionPlan
        };
    }

    public async Task<int> ResetAllStoresExceptDefaultAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        var nonDefaultStores = await masterDb.Tenants
            .Where(t => t.StoreCode != "DEFAULT")
            .ToListAsync();

        int deletedCount = 0;
        foreach (var store in nonDefaultStores)
        {
            try
            {
                if (!_dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    var dbFile = Path.Combine("tenants", $"{store.StoreCode}.db");
                    if (File.Exists(dbFile)) File.Delete(dbFile);
                    var shmFile = Path.Combine("tenants", $"{store.StoreCode}.db-shm");
                    if (File.Exists(shmFile)) File.Delete(shmFile);
                    var walFile = Path.Combine("tenants", $"{store.StoreCode}.db-wal");
                    if (File.Exists(walFile)) File.Delete(walFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Tenancy] Could not remove physical DB file for store {StoreCode}", store.StoreCode);
            }

            masterDb.Tenants.Remove(store);
            deletedCount++;
        }

        await masterDb.SaveChangesAsync();
        _logger.LogInformation("[Tenancy] Cleaned up {Count} non-default stores. Preserved DEFAULT demo store.", deletedCount);

        // Guarantee DEFAULT demo store has its full demo tables, categories, menu, and admin account
        await EnsureMasterAndDefaultTenantAsync();

        return deletedCount;
    }
}

