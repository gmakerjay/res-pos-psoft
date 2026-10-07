using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class ServerConfigDialog : Window
{
    private readonly PosConfig _config;

    public bool IsConfigSaved { get; private set; }

    public ServerConfigDialog(PosConfig config)
    {
        InitializeComponent();
        _config = config;

        TxtServerAddress.Text = _config.GetBaseUrl();
        TxtStoreCode.Text = string.IsNullOrWhiteSpace(_config.StoreCode) ? "DEFAULT" : _config.StoreCode;
        TxtStationName.Text = _config.StationName;
    }

    private void BtnSetLocalhost_Click(object sender, RoutedEventArgs e)
    {
        TxtServerAddress.Text = "http://127.0.0.1:5000";
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var address = TxtServerAddress.Text.Trim();
        var storeCode = TxtStoreCode.Text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(address))
        {
            TxtStatus.Text = "กรุณากรอกที่อยู่เซิร์ฟเวอร์";
            TxtStatus.Foreground = Brushes.Red;
            return;
        }

        if (string.IsNullOrWhiteSpace(storeCode))
        {
            TxtStatus.Text = "กรุณากรอกรหัสร้านค้า (เช่น DEFAULT)";
            TxtStatus.Foreground = Brushes.Red;
            return;
        }

        // Normalize URL
        var testConfig = new PosConfig { ServerAddress = address };
        var baseUrl = testConfig.GetBaseUrl();

        BtnTest.IsEnabled = false;
        TxtStatus.Text = "กำลังทดสอบเชื่อมต่อ...";
        TxtStatus.Foreground = Brushes.Blue;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            
            // 1. Health check
            var healthUrl = $"{baseUrl.TrimEnd('/')}/api/health";
            var response = await client.GetAsync(healthUrl);

            if (!response.IsSuccessStatusCode)
            {
                TxtStatus.Text = $"เซิร์ฟเวอร์ตอบกลับรหัส: {(int)response.StatusCode}";
                TxtStatus.Foreground = Brushes.Red;
                return;
            }

            // 2. Store verification
            var storeUrl = $"{baseUrl.TrimEnd('/')}/api/stores/check/{Uri.EscapeDataString(storeCode)}";
            var storeRes = await client.GetAsync(storeUrl);

            if (storeRes.IsSuccessStatusCode)
            {
                var content = await storeRes.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;
                var storeName = root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("storeName", out var nameProp) 
                    ? nameProp.GetString() 
                    : storeCode;

                TxtStatus.Text = $"เชื่อมต่อสำเร็จ [ร้าน: {storeName} ({storeCode})]";
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Green
            }
            else
            {
                TxtStatus.Text = $"เซิร์ฟเวอร์ออนไลน์ แต่ไม่พบร้านค้า '{storeCode}'";
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Orange
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "ไม่พบเซิร์ฟเวอร์ หรือเครือข่ายขัดข้อง";
            TxtStatus.Foreground = Brushes.Red;
            PosLogger.Warn($"[Config Test Failed] Cannot reach {baseUrl}: {ex.Message}");
        }
        finally
        {
            BtnTest.IsEnabled = true;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var address = TxtServerAddress.Text.Trim();
        var storeCode = TxtStoreCode.Text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(address))
        {
            MessageBox.Show("กรุณากรอกที่อยู่หรือ URL ของเซิร์ฟเวอร์", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(storeCode))
        {
            MessageBox.Show("กรุณากรอกรหัสร้านค้า (เช่น DEFAULT หรือ SHOP01)", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.ServerAddress = address;
        _config.StoreCode = storeCode;
        _config.StationName = TxtStationName.Text.Trim();
        _config.Save();

        IsConfigSaved = true;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
