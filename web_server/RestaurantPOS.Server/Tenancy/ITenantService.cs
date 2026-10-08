using RestaurantPOS.Server.Data;
using RestaurantPOS.Shared.DTOs;

namespace RestaurantPOS.Server.Tenancy;

public interface ITenantService
{
    Task EnsureMasterAndDefaultTenantAsync();
    string GetTenantConnectionString(string storeCode);
    Task<TenantEntity?> GetTenantAsync(string storeCode);
    Task<List<TenantDto>> GetAllTenantsAsync();
    Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest request);
    AppDbContext CreateTenantDbContext(string storeCode);
    Task<TenantDto?> UpdateTenantLicenseAsync(string storeCode, string plan, int addDays);
    Task<TenantDto?> ActivateTenantWithKeyAsync(string storeCode, string licenseKey);
    Task<TenantDto?> SetTenantActiveStatusAsync(string storeCode, bool isActive);
    GenerateKeyResponse GenerateActivationKey(string storeCode, string plan);
    string GenerateSecureStoreCode();
    Task<int> ResetAllStoresExceptDefaultAsync();
}

