namespace RestaurantPOS.Server.Tenancy;

public interface ITenantProvider
{
    string CurrentTenantCode { get; }
    TenantEntity? CurrentTenant { get; }
    void SetTenant(string storeCode, TenantEntity? tenant = null);
}
