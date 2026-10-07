using System.IO;
using System.Text.Json;

namespace RestaurantPOS.Wpf.Services;

public class PosConfig
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos_config.json");

    // Server Connection (Supports IP, Localhost, or Domain with HTTP/HTTPS)
    public string ServerAddress { get; set; } = "http://127.0.0.1:5000";
    public string StoreCode { get; set; } = "DEFAULT";
    public string StationName { get; set; } = "POS-01";

    // Legacy fields for backward compatibility
    public string ServerIp { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 5000;

    // Printers & Receipts
    public string ReceiptPrinterName { get; set; } = "";
    public string KitchenPrinterName { get; set; } = "";
    public bool AutoPrintReceipt { get; set; } = true;
    public bool AutoPrintKitchenSlip { get; set; } = true;

    // Sound & Audio Alerts
    public bool SoundEnabled { get; set; } = true;
    public int SoundVolume { get; set; } = 90; // 0 - 100%

    // Store Info & Tax
    public string StoreName { get; set; } = "ร้านอาหาร Restaurant POS";
    public string StoreAddress { get; set; } = "123/45 ถนนหลัก แขวงเมือง เขตเมือง กรุงเทพฯ";
    public string StorePhone { get; set; } = "02-000-0000";
    public string TaxId { get; set; } = "0105560000000";
    public string ReceiptHeader { get; set; } = "ยินดีต้อนรับสู่ร้านอาหารของเรา";
    public string ReceiptFooter { get; set; } = "ขอบคุณที่ใช้บริการ / THANK YOU";
    public decimal VatPercent { get; set; } = 7.0m;
    public decimal ServiceChargePercent { get; set; } = 0.0m;
    public int LowStockThreshold { get; set; } = 5;

    public string GetBaseUrl()
    {
        var raw = string.IsNullOrWhiteSpace(ServerAddress) ? ServerIp : ServerAddress.Trim();
        raw = raw.TrimEnd('/');

        // If user typed a full URL with scheme
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        // If user typed only IP or hostname without scheme
        if (raw.Contains(':'))
        {
            return $"http://{raw}";
        }

        // Default with port
        return $"http://{raw}:{ServerPort}";
    }

    public static PosConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<PosConfig>(json);
                if (config != null)
                {
                    // Migrate legacy if needed
                    if (string.IsNullOrWhiteSpace(config.ServerAddress) && !string.IsNullOrWhiteSpace(config.ServerIp))
                    {
                        config.ServerAddress = $"http://{config.ServerIp}:{config.ServerPort}";
                    }
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Config] Failed to load pos_config.json, using defaults: " + ex.Message, ex);
        }

        var defaultConfig = new PosConfig();
        defaultConfig.Save();
        return defaultConfig;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            PosLogger.Info($"[Config] Configuration saved: Server = {GetBaseUrl()} | Station = {StationName}");
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Config] Failed to save pos_config.json: " + ex.Message, ex);
        }
    }
}
