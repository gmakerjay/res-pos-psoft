using RestaurantPOS.Shared.Enums;

namespace RestaurantPOS.Shared.DTOs;

public class TableDto
{
    public int Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public int? CurrentOrderId { get; set; }
    public decimal CurrentBillAmount { get; set; }
    public DateTime? SeatedAt { get; set; }
    public string? ReservationCustomerName { get; set; }
    public string? ReservationCustomerPhone { get; set; }
    public DateTime? ReservationTime { get; set; }
    public int? ReservationPartySize { get; set; }
    public string? ReservationNotes { get; set; }

    public string StatusBadge => Status switch
    {
        TableStatus.Available => "[ว่าง]",
        TableStatus.Occupied => "[มีลูกค้า]",
        TableStatus.Reserved => !string.IsNullOrEmpty(ReservationCustomerName) ? $"[จอง: {ReservationCustomerName}]" : "[จอง]",
        TableStatus.Billing => "[รอชำระเงิน]",
        _ => Status.ToString()
    };
}

public class ReserveTableRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime ReservationTime { get; set; } = DateTime.UtcNow;
    public int PartySize { get; set; } = 2;
    public string? Notes { get; set; }
}

public class CreateTableDto
{
    public string TableNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
}


public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int ProductCount { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? OutOfStockReason { get; set; }
    public int StockQuantity { get; set; }
    public bool TrackStock { get; set; }
    public string? KitchenStation { get; set; } = "MainKitchen"; // "MainKitchen", "Bar", "Dessert"
    public List<ProductOptionGroupDto> OptionGroups { get; set; } = new();
}

public class ProductOptionGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "ระดับความหวาน", "เพิ่มท็อปปิ้ง"
    public bool IsRequired { get; set; }
    public bool AllowMultiple { get; set; }
    public List<ProductOptionItemDto> Options { get; set; } = new();
}

public class ProductOptionItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "หวานน้อย 50%", "เพิ่มไข่ดาว"
    public decimal ExtraPrice { get; set; }
}

public class OrderItemOptionDto
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string OptionName { get; set; } = string.Empty;
    public decimal ExtraPrice { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => (UnitPrice + (Options?.Sum(o => o.ExtraPrice) ?? 0)) * Quantity;
    public string? SpecialNotes { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public string? KitchenStation { get; set; }
    public List<OrderItemOptionDto> Options { get; set; } = new();

    public string OptionsAndNotesDisplay
    {
        get
        {
            var parts = new List<string>();
            if (Options != null && Options.Any())
            {
                parts.Add("ตัวเลือก: " + string.Join(", ", Options.Select(o => o.OptionName)));
            }
            if (!string.IsNullOrWhiteSpace(SpecialNotes))
            {
                parts.Add("หมายเหตุ: " + SpecialNotes);
            }
            return parts.Count > 0 ? string.Join(" | ", parts) : "-";
        }
    }

    public string StatusBadge => Status switch
    {
        OrderStatus.New => "[ออเดอร์ใหม่]",
        OrderStatus.Accepted => "[รับแล้ว]",
        OrderStatus.Preparing => "[กำลังปรุง]",
        OrderStatus.Ready => "[ปรุงเสร็จแล้ว]",
        OrderStatus.Completed => "[เสร็จสิ้น]",
        OrderStatus.Cancelled => "[ยกเลิก]",
        _ => Status.ToString()
    };
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType Type { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();

    public string CustomerDisplay => !string.IsNullOrWhiteSpace(CustomerPhone)
        ? (!string.IsNullOrWhiteSpace(CustomerName) ? $"{CustomerName} ({CustomerPhone})" : CustomerPhone)
        : (CustomerName ?? "-");

    public string TableDisplay => !string.IsNullOrWhiteSpace(TableNumber)
        ? TableNumber
        : (Type == OrderType.TakeAway ? "สั่งกลับบ้าน" : (Type == OrderType.Delivery ? "เดลิเวอรี" : "-"));

    public string StatusBadge => Status switch
    {
        OrderStatus.New => "[ออเดอร์ใหม่]",
        OrderStatus.Accepted => "[รับออเดอร์แล้ว]",
        OrderStatus.Preparing => "[กำลังปรุง]",
        OrderStatus.Ready => "[ปรุงเสร็จแล้ว]",
        OrderStatus.Completed => "[เสร็จสิ้น]",
        OrderStatus.Cancelled => "[ยกเลิก]",
        _ => Status.ToString()
    };

    public bool CanAccept => Status == OrderStatus.New;
    public bool CanPrepare => Status == OrderStatus.Accepted;
    public bool CanReady => Status == OrderStatus.Preparing;
    public bool CanComplete => Status == OrderStatus.Ready;
    public bool CanCancel => Status != OrderStatus.Completed && Status != OrderStatus.Cancelled;
    public bool IsActive => Status != OrderStatus.Completed && Status != OrderStatus.Cancelled;

    public string StatusColorHex => Status switch
    {
        OrderStatus.New => "#DC2626",
        OrderStatus.Accepted => "#1D4ED8",
        OrderStatus.Preparing => "#D97706",
        OrderStatus.Ready => "#EA580C",
        OrderStatus.Completed => "#16A34A",
        OrderStatus.Cancelled => "#64748B",
        _ => "#334155"
    };
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? SpecialNotes { get; set; }
    public List<int> SelectedOptionIds { get; set; } = new();
}

public class CreateOrderRequest
{
    public OrderType Type { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    public string? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public string? ClientRequestId { get; set; } // For duplicate prevention
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
    public string? Reason { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Source { get; set; } // "POS" | "WEB_KITCHEN" | "WEB_MANAGE"
}

public class OrderActionActivityDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? TableDisplay { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string Source { get; set; } = "POS";
    public string OperatorName { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PaymentRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CashierName { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? StoreCode { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

public class DailyReportSummaryDto
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public decimal CashSales { get; set; }
    public decimal QrSales { get; set; }
    public decimal CardSales { get; set; }
    public List<TopSellingProductDto> TopProducts { get; set; } = new();
}

public class TopSellingProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class StockAdjustmentRequest
{
    public int ChangeQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AdjustedBy { get; set; }
}

public class AuditLogDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public DateTime LocalTimestamp => Timestamp.ToLocalTime();
}

public class CategoryCreateOrUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
}

public class IngredientDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "วัตถุดิบทั่วไป";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "กก.";
    public decimal MinQuantityAlert { get; set; } = 5;
    public decimal CostPrice { get; set; }
    public DateTime? LastRestockedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class IngredientAdjustmentRequest
{
    public decimal ChangeQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AdjustedBy { get; set; }
}

public class RegisterTenantRequest
{
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
    public string? Address { get; set; }
}

public class TenantDto
{
    public int Id { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string SubscriptionPlan { get; set; } = "Standard";
}

public class StoreInfoResponse
{
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int TableCount { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string SubscriptionPlan { get; set; } = "Trial";
    public int DaysRemaining { get; set; } = 14;
    public bool IsExpired { get; set; }
    public int TotalBillCount { get; set; }
    public bool IsPosOnline { get; set; }
    public int ActivePosCount { get; set; }
    public string? OpeningHours { get; set; } = "10:00 - 22:00 น.";
}

public class StoreStatusDto
{
    public string StoreCode { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public int ActivePosCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SimulateStoreStatusRequest
{
    public string StoreCode { get; set; } = string.Empty;
    public bool? IsOnline { get; set; }
}

public class UpgradeLicenseRequest
{
    public string StoreCode { get; set; } = string.Empty;
    public string Plan { get; set; } = "FullLifetime"; // "FullLifetime", "FullYearly", "Trial"
    public int ExtendDays { get; set; }
}

public class ActivateLicenseRequest
{
    public string StoreCode { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
}

public class GenerateKeyResponse
{
    public string StoreCode { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
}


