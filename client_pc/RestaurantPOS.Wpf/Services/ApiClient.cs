using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Shared.Errors;
using RestaurantPOS.Shared.Models;

namespace RestaurantPOS.Wpf.Services;

public class ApiClient
{
    private HttpClient _http = null!;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _httpLock = new();
    public string BaseUrl { get; private set; }
    public string StoreCode { get; private set; }

    public ApiClient(string baseUrl = "http://localhost:5000", string storeCode = "DEFAULT")
    {
        BaseUrl = baseUrl.TrimEnd('/');
        StoreCode = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        InitHttpClient();
        PosLogger.SetServerUrl(BaseUrl);
    }

    private void InitHttpClient()
    {
        lock (_httpLock)
        {
            var oldHttp = _http;
            var newHttp = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(8)
            };
            newHttp.DefaultRequestHeaders.Add("X-Tenant-Code", StoreCode);
            newHttp.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) RestaurantPOS/1.0");

            if (!string.IsNullOrEmpty(AuthToken))
            {
                newHttp.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
            }

            _http = newHttp;
            try { oldHttp?.Dispose(); } catch { }
        }
    }

    public void UpdateConnection(string baseUrl, string storeCode)
    {
        var cleanUrl = baseUrl.TrimEnd('/');
        var cleanCode = string.IsNullOrWhiteSpace(storeCode) ? "DEFAULT" : storeCode.Trim().ToUpperInvariant();

        bool urlChanged = !string.Equals(BaseUrl, cleanUrl, StringComparison.OrdinalIgnoreCase);
        bool codeChanged = !string.Equals(StoreCode, cleanCode, StringComparison.OrdinalIgnoreCase);

        BaseUrl = cleanUrl;
        StoreCode = cleanCode;

        if (urlChanged || codeChanged || _http == null)
        {
            InitHttpClient();
        }

        PosLogger.SetServerUrl(BaseUrl);
        PosLogger.Info($"[API] Updated connection to {BaseUrl} (Store: {StoreCode})");
    }

    public string? AuthToken { get; private set; }
    public UserDto? CurrentUser { get; private set; }

    public async Task<StoreInfoResponse?> GetStoreInfoAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<StoreInfoResponse>>("/api/stores/info", _jsonOptions);
            return res?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Warn($"[API] Could not get store info: {ex.Message}");
            return null;
        }
    }

    public async Task<UserDto> LoginAsync(string username, string password, string? storeCode = null)
    {
        try
        {
            var code = !string.IsNullOrWhiteSpace(storeCode) ? storeCode.Trim().ToUpperInvariant() : StoreCode;
            if (code != StoreCode)
            {
                UpdateConnection(BaseUrl, code);
            }

            var req = new LoginRequest 
            { 
                Username = username, 
                Password = password,
                StoreCode = code
            };
            var res = await _http.PostAsJsonAsync("/api/auth/login", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
            {
                AuthToken = result.Data.Token;
                CurrentUser = result.Data.User;
                _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
                PosLogger.Info($"[Auth] Logged in successfully: {CurrentUser.Username} ({CurrentUser.Role}) [Store: {code}]");
                return CurrentUser;
            }
            throw new Exception(result?.Message ?? "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
        }
        catch (Exception ex)
        {
            PosLogger.Warn($"[Auth] Login failed: {ex.Message}");
            throw;
        }
    }

    public void Logout()
    {
        AuthToken = null;
        CurrentUser = null;
        _http.DefaultRequestHeaders.Authorization = null;
        PosLogger.Info("[Auth] User logged out");
    }

    public async Task<List<TableDto>> GetTablesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<TableDto>>>("/api/tables", _jsonOptions);
            return res?.Data ?? new List<TableDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get tables: " + ex.Message, ex);
            throw new Exception("ไม่สามารถเชื่อมต่อดึงข้อมูลโต๊ะจากเซิร์ฟเวอร์ได้: " + ex.Message, ex);
        }
    }

    public async Task<TableDto?> UpdateTableStatusAsync(int id, TableStatus status)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/tables/{id}/status", status);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TableDto>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to update table {id} status: " + ex.Message, ex);
            throw new Exception("ไม่สามารถอัปเดตสถานะโต๊ะได้: " + ex.Message, ex);
        }
    }

    public async Task<TableDto?> CreateTableAsync(CreateTableDto dto)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/tables", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TableDto>>(_jsonOptions);
            if (result != null && !result.Success)
                throw new Exception(result.Message ?? "Failed to create table");
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to create table: " + ex.Message, ex);
            throw new Exception("ไม่สามารถเพิ่มโต๊ะได้: " + ex.Message, ex);
        }
    }

    public async Task<bool> DeleteTableAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/tables/{id}");
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(_jsonOptions);
            return result?.Data ?? false;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to delete table {id}: " + ex.Message, ex);
            throw new Exception("ไม่สามารถลบโต๊ะได้: " + ex.Message, ex);
        }
    }

    public async Task<TableDto?> ReserveTableAsync(int id, ReserveTableRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/tables/{id}/reserve", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TableDto>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to reserve table {id}: " + ex.Message, ex);
            throw new Exception("ไม่สามารถบันทึกการจองโต๊ะได้: " + ex.Message, ex);
        }
    }

    public async Task<TableDto?> CheckInTableAsync(int id)
    {
        try
        {
            var res = await _http.PostAsync($"/api/tables/{id}/check-in", null);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TableDto>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to check-in table {id}: " + ex.Message, ex);
            throw new Exception("ไม่สามารถเช็คอินเข้าโต๊ะได้: " + ex.Message, ex);
        }
    }

    public async Task<TableDto?> CancelTableReservationAsync(int id)
    {
        try
        {
            var res = await _http.PostAsync($"/api/tables/{id}/cancel-reservation", null);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TableDto>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to cancel reservation for table {id}: " + ex.Message, ex);
            throw new Exception("ไม่สามารถยกเลิกการจองโต๊ะได้: " + ex.Message, ex);
        }
    }


    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<CategoryDto>>>("/api/categories", _jsonOptions);
            return res?.Data ?? new List<CategoryDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get categories: " + ex.Message, ex);
            return new List<CategoryDto>();
        }
    }

    public async Task<List<ProductDto>> GetProductsAsync(int? categoryId = null)
    {
        try
        {
            var url = categoryId.HasValue ? $"/api/products?categoryId={categoryId.Value}" : "/api/products";
            var res = await _http.GetFromJsonAsync<ApiResponse<List<ProductDto>>>(url, _jsonOptions);
            return res?.Data ?? new List<ProductDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get products: " + ex.Message, ex);
            return new List<ProductDto>();
        }
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/orders", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
            {
                return result.Data;
            }
            throw new Exception(result?.Message ?? "เกิดข้อผิดพลาดในการสร้างออเดอร์");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to create order: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<List<OrderDto>> GetActiveOrdersAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<OrderDto>>>("/api/orders/active", _jsonOptions);
            return res?.Data ?? new List<OrderDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get active orders: " + ex.Message, ex);
            return new List<OrderDto>();
        }
    }

    public async Task<OrderDto?> UpdateOrderStatusAsync(int id, OrderStatus status, string? updatedBy = null, string? source = "POS")
    {
        try
        {
            var req = new UpdateOrderStatusRequest 
            { 
                Status = status,
                UpdatedBy = !string.IsNullOrWhiteSpace(updatedBy) ? updatedBy : (CurrentUser?.FullName ?? CurrentUser?.Username ?? "แคชเชียร์"),
                Source = !string.IsNullOrWhiteSpace(source) ? source : "POS"
            };
            var res = await _http.PutAsJsonAsync($"/api/orders/{id}/status", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(_jsonOptions);
            return result?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to update order {id} status: " + ex.Message, ex);
            throw new Exception("ไม่สามารถอัปเดตสถานะออเดอร์ได้: " + ex.Message, ex);
        }
    }

    public async Task<OrderDto> PayOrderAsync(int id, PaymentRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"/api/orders/{id}/pay", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
            {
                return result.Data;
            }
            throw new Exception(result?.Message ?? "เกิดข้อผิดพลาดในการชำระเงิน");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to process payment for order {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<List<OrderDto>> GetActiveOrdersByTableAsync(string tableRef)
    {
        try
        {
            var encoded = Uri.EscapeDataString(tableRef);
            var res = await _http.GetFromJsonAsync<ApiResponse<List<OrderDto>>>($"/api/orders/table/{encoded}", _jsonOptions);
            return res?.Data ?? new List<OrderDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to get active orders for table {tableRef}: " + ex.Message, ex);
            return new List<OrderDto>();
        }
    }

    public async Task<OrderDto> PayTableOrdersAsync(string tableRef, PaymentRequest request)
    {
        try
        {
            var encoded = Uri.EscapeDataString(tableRef);
            var res = await _http.PostAsJsonAsync($"/api/orders/table/{encoded}/pay", request);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
            {
                return result.Data;
            }
            throw new Exception(result?.Message ?? "เกิดข้อผิดพลาดในการชำระเงินโต๊ะนี้");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to pay table {tableRef}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<DailyReportSummaryDto?> GetDailyReportAsync(DateTime? date = null)
    {
        try
        {
            var dateStr = (date ?? DateTime.Today).ToString("yyyy-MM-dd");
            var res = await _http.GetFromJsonAsync<ApiResponse<DailyReportSummaryDto>>($"/api/reports/daily?date={dateStr}", _jsonOptions);
            return res?.Data;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get daily report: " + ex.Message, ex);
            return null;
        }
    }

    public async Task<bool> AdjustStockAsync(int productId, int changeQuantity, string reason, string? username = null)
    {
        try
        {
            var req = new StockAdjustmentRequest
            {
                ChangeQuantity = changeQuantity,
                Reason = reason,
                AdjustedBy = username ?? CurrentUser?.Username ?? "admin"
            };
            var res = await _http.PostAsJsonAsync($"/api/products/{productId}/stock", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(_jsonOptions);
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to adjust stock for product {productId}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int limit = 100)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>($"/api/audit?limit={limit}", _jsonOptions);
            return res?.Data ?? new List<AuditLogDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get audit logs: " + ex.Message, ex);
            return new List<AuditLogDto>();
        }
    }

    public async Task<List<IngredientDto>> GetIngredientsAsync(string? category = null)
    {
        try
        {
            var query = string.IsNullOrWhiteSpace(category) ? "" : $"?category={Uri.EscapeDataString(category)}";
            var res = await _http.GetFromJsonAsync<ApiResponse<List<IngredientDto>>>($"/api/ingredients{query}", _jsonOptions);
            return res?.Data ?? new List<IngredientDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get ingredients: " + ex.Message, ex);
            return new List<IngredientDto>();
        }
    }

    public async Task<bool> AdjustIngredientStockAsync(int ingredientId, decimal changeQuantity, string reason, string? username = null)
    {
        try
        {
            var req = new IngredientAdjustmentRequest
            {
                ChangeQuantity = changeQuantity,
                Reason = reason,
                AdjustedBy = username ?? CurrentUser?.Username ?? "admin"
            };
            var res = await _http.PostAsJsonAsync($"/api/ingredients/{ingredientId}/adjust", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<IngredientDto>>(_jsonOptions);
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to adjust stock for ingredient {ingredientId}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<CategoryDto> CreateCategoryAsync(CategoryCreateOrUpdateRequest req)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/categories", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<CategoryDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "สร้างหมวดหมู่ไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to create category: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, CategoryCreateOrUpdateRequest req)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/categories/{id}", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<CategoryDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "แก้ไขหมวดหมู่ไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to update category {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/categories/{id}");
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(_jsonOptions);
            if (result != null && result.Success)
                return true;
            throw new Exception(result?.Message ?? "ลบหมวดหมู่ไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to delete category {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<ProductDto> CreateProductAsync(ProductDto dto)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/products", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "เพิ่มเมนูอาหารไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to create product: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<ProductDto> UpdateProductAsync(int id, ProductDto dto)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/products/{id}", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "แก้ไขเมนูอาหารไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to update product {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/products/{id}");
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(_jsonOptions);
            if (result != null && result.Success)
                return true;
            throw new Exception(result?.Message ?? "ลบเมนูอาหารไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to delete product {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<IngredientDto> CreateIngredientAsync(IngredientDto dto)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("/api/ingredients", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<IngredientDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "เพิ่มวัตถุดิบไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to create ingredient: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<IngredientDto> UpdateIngredientAsync(int id, IngredientDto dto)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"/api/ingredients/{id}", dto);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<IngredientDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
                return result.Data;
            throw new Exception(result?.Message ?? "แก้ไขวัตถุดิบไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to update ingredient {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<bool> DeleteIngredientAsync(int id)
    {
        try
        {
            var res = await _http.DeleteAsync($"/api/ingredients/{id}");
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<bool>>(_jsonOptions);
            if (result != null && result.Success)
                return true;
            throw new Exception(result?.Message ?? "ลบวัตถุดิบไม่สำเร็จ");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[API] Failed to delete ingredient {id}: " + ex.Message, ex);
            throw;
        }
    }

    public async Task<TenantDto> ActivateLicenseAsync(string licenseKey)
    {
        try
        {
            var req = new ActivateLicenseRequest
            {
                StoreCode = StoreCode,
                LicenseKey = licenseKey.Trim().ToUpperInvariant()
            };
            var res = await _http.PostAsJsonAsync("/api/stores/license/activate", req);
            var result = await res.Content.ReadFromJsonAsync<ApiResponse<TenantDto>>(_jsonOptions);
            if (result != null && result.Success && result.Data != null)
            {
                PosLogger.Info($"[Licensing] Activated store {StoreCode} successfully with key");
                return result.Data;
            }
            throw new Exception(result?.Message ?? "รหัสเปิดใช้งานไม่ถูกต้อง");
        }
        catch (Exception ex)
        {
            PosLogger.Warn($"[Licensing] Activation failed: {ex.Message}");
            throw;
        }
    }

    public async Task<List<AuditLogDto>> GetAuditLogsAsync(int limit = 100, string? search = null)
    {
        try
        {
            var url = $"/api/audit?limit={limit}";
            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"&search={Uri.EscapeDataString(search.Trim())}";
            }
            var res = await _http.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>(url, _jsonOptions);
            return res?.Data ?? new List<AuditLogDto>();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[API] Failed to get audit logs: " + ex.Message, ex);
            return new List<AuditLogDto>();
        }
    }

    public async Task<string> ExportStoreBackupRawJsonAsync()
    {
        try
        {
            var res = await _http.GetAsync("/api/backup/export");
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Backup] Failed to export backup: " + ex.Message, ex);
            throw;
        }
    }
}


