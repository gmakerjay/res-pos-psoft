using RestaurantPOS.Shared.Enums;

namespace RestaurantPOS.Shared.Models;

public class ClientLogDto
{
    public PosLogLevel Level { get; set; } = PosLogLevel.Error;
    public string Source { get; set; } = string.Empty; // e.g., "WPF-POS", "Customer-Web", "Admin-Web"
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Details { get; set; }
    public string? DeviceInfo { get; set; }
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
