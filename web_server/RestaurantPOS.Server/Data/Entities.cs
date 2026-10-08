using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantPOS.Shared.Enums;

namespace RestaurantPOS.Server.Data;

[Table("Tables")]
public class TableEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string TableNumber { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    public int? CurrentOrderId { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ReservationCustomerName { get; set; }
    [MaxLength(50)]
    public string? ReservationCustomerPhone { get; set; }
    public DateTime? ReservationTime { get; set; }
    public int? ReservationPartySize { get; set; }
    [MaxLength(255)]
    public string? ReservationNotes { get; set; }
}

[Table("Categories")]
public class CategoryEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(255)]
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductEntity> Products { get; set; } = new();
}

[Table("Products")]
public class ProductEntity
{
    [Key]
    public int Id { get; set; }
    public int CategoryId { get; set; }
    [ForeignKey(nameof(CategoryId))]
    public CategoryEntity? Category { get; set; }
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    [MaxLength(200)]
    public string? OutOfStockReason { get; set; }
    public int StockQuantity { get; set; }
    public bool TrackStock { get; set; }
    [MaxLength(50)]
    public string? KitchenStation { get; set; } = "MainKitchen";
    public List<ProductOptionGroupEntity> OptionGroups { get; set; } = new();
}

[Table("ProductOptionGroups")]
public class ProductOptionGroupEntity
{
    [Key]
    public int Id { get; set; }
    public int ProductId { get; set; }
    [ForeignKey(nameof(ProductId))]
    public ProductEntity? Product { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool AllowMultiple { get; set; }
    public List<ProductOptionItemEntity> Options { get; set; } = new();
}

[Table("ProductOptionItems")]
public class ProductOptionItemEntity
{
    [Key]
    public int Id { get; set; }
    public int GroupId { get; set; }
    [ForeignKey(nameof(GroupId))]
    public ProductOptionGroupEntity? Group { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraPrice { get; set; }
}

[Table("Orders")]
public class OrderEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;
    public OrderType Type { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    [ForeignKey(nameof(TableId))]
    public TableEntity? Table { get; set; }
    [MaxLength(50)]
    public string? TableNumber { get; set; }
    [MaxLength(100)]
    public string? CustomerName { get; set; }
    [MaxLength(50)]
    public string? CustomerPhone { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ChangeAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    [MaxLength(100)]
    public string? ClientRequestId { get; set; } // Duplicate prevention
    public List<OrderItemEntity> Items { get; set; } = new();
}

[Table("OrderItems")]
public class OrderItemEntity
{
    [Key]
    public int Id { get; set; }
    public int OrderId { get; set; }
    [ForeignKey(nameof(OrderId))]
    public OrderEntity? Order { get; set; }
    public int ProductId { get; set; }
    [ForeignKey(nameof(ProductId))]
    public ProductEntity? Product { get; set; }
    [Required, MaxLength(150)]
    public string ProductName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    [MaxLength(255)]
    public string? SpecialNotes { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    [MaxLength(50)]
    public string? KitchenStation { get; set; }
    public List<OrderItemOptionEntity> Options { get; set; } = new();
}

[Table("OrderItemOptions")]
public class OrderItemOptionEntity
{
    [Key]
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    [ForeignKey(nameof(OrderItemId))]
    public OrderItemEntity? OrderItem { get; set; }
    [Required, MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string OptionName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExtraPrice { get; set; }
}

[Table("Users")]
public class UserEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
    public string PermissionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("AuditLogs")]
public class AuditLogEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    [MaxLength(100)]
    public string? UserId { get; set; }
    [MaxLength(100)]
    public string? Username { get; set; }
    public string? Details { get; set; }
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

[Table("Ingredients")]
public class IngredientEntity
{
    [Key]
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string Category { get; set; } = "วัตถุดิบทั่วไป";
    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; }
    [Required, MaxLength(50)]
    public string Unit { get; set; } = "กก.";
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinQuantityAlert { get; set; } = 5;
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; }
    public DateTime? LastRestockedAt { get; set; }
    [MaxLength(255)]
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
