namespace RestaurantPOS.Server.Settings;

public class PlatformSettings
{
    public const string SectionName = "PlatformSettings";

    /// <summary>
    /// Operating Mode: "Platform" (Multi-tenant SaaS) or "Standalone" (Locked Single Store Enterprise)
    /// </summary>
    public string ServerMode { get; set; } = "Platform";

    /// <summary>
    /// If false, /api/stores/register will return 403 Forbidden.
    /// </summary>
    public bool AllowStoreRegistration { get; set; } = true;

    /// <summary>
    /// If Standalone mode, specifies the single locked RPOS Code (e.g. "RPOS-LOCKED-001" or empty for default)
    /// </summary>
    public string StandaloneRPOSCode { get; set; } = string.Empty;

    /// <summary>
    /// Maximum allowed simultaneous active Windows POS terminals
    /// </summary>
    public int MaxPosTerminals { get; set; } = 10;
}
