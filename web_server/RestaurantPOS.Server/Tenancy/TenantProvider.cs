using Microsoft.AspNetCore.Http;

namespace RestaurantPOS.Server.Tenancy;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _explicitTenantCode;
    private TenantEntity? _explicitTenant;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentTenantCode
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_explicitTenantCode))
            {
                return _explicitTenantCode;
            }

            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                // 1. Header X-Tenant-Code
                if (context.Request.Headers.TryGetValue("X-Tenant-Code", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
                {
                    return headerVal.ToString().Trim().ToUpperInvariant();
                }

                // 2. Query string ?tenant=...
                if (context.Request.Query.TryGetValue("tenant", out var queryVal) && !string.IsNullOrWhiteSpace(queryVal))
                {
                    return queryVal.ToString().Trim().ToUpperInvariant();
                }

                // 3. User JWT Claim
                var claimVal = context.User?.FindFirst("tenant_code")?.Value;
                if (!string.IsNullOrWhiteSpace(claimVal))
                {
                    return claimVal.Trim().ToUpperInvariant();
                }
            }

            return "DEFAULT";
        }
    }

    public TenantEntity? CurrentTenant => _explicitTenant;

    public void SetTenant(string storeCode, TenantEntity? tenant = null)
    {
        _explicitTenantCode = storeCode.Trim().ToUpperInvariant();
        _explicitTenant = tenant;
    }
}
