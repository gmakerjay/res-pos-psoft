using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPOS.Server.Tenancy;

[Table("Tenants")]
public class TenantEntity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string StoreCode { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string StoreName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string OwnerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string OwnerPhone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string AdminUsername { get; set; } = "admin";

    public string? ConnectionString { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    [MaxLength(50)]
    public string SubscriptionPlan { get; set; } = "Standard";
}
