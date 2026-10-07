using System.Windows;
using System.Windows.Media;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class ActivateLicenseDialog : Window
{
    private readonly ApiClient _api;
    private StoreInfoResponse? _storeInfo;

    public StoreInfoResponse? UpdatedStoreInfo => _storeInfo;

    public ActivateLicenseDialog(ApiClient api, StoreInfoResponse? storeInfo = null)
    {
        InitializeComponent();
        _api = api;
        _storeInfo = storeInfo;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_storeInfo == null)
        {
            try
            {
                _storeInfo = await _api.GetStoreInfoAsync();
            }
            catch { }
        }

        UpdateDisplay();
        TxtLicenseKey.Focus();
    }

    private void UpdateDisplay()
    {
        TxtStoreCode.Text = _api.StoreCode;

        if (_storeInfo != null)
        {
            TxtStoreName.Text = _storeInfo.StoreName;

            if (_storeInfo.SubscriptionPlan == "FullLifetime")
            {
                TxtCurrentPlan.Text = "[ เวอร์ชันเต็ม ตลอดชีพ (Full Lifetime) ]";
                TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            }
            else if (_storeInfo.SubscriptionPlan == "FullYearly")
            {
                TxtCurrentPlan.Text = $"[ เวอร์ชันเต็ม รายปี (เหลืออีก {_storeInfo.DaysRemaining} วัน) ]";
                TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(2, 119, 189));
            }
            else
            {
                if (_storeInfo.IsExpired)
                {
                    TxtCurrentPlan.Text = "[ สิทธิ์ทดลองใช้งานหมดอายุแล้ว (กรุณาต่อสิทธิ์) ]";
                    TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                }
                else
                {
                    TxtCurrentPlan.Text = $"[ ทดลองใช้ฟรี: เหลืออีก {_storeInfo.DaysRemaining} วัน ]";
                    TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0));
                }
            }
        }
        else
        {
            TxtStoreName.Text = "ร้านค้า (ไม่ระบุ)";
            TxtCurrentPlan.Text = "[ กำลังเชื่อมต่อ ]";
        }
    }

    private async void BtnActivate_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtLicenseKey.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            TxtStatusMessage.Text = "กรุณากรอกรหัสเปิดใช้งาน (Activation Key)";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            return;
        }

        BtnActivate.IsEnabled = false;
        TxtStatusMessage.Text = "กำลังตรวจสอบรหัสเปิดใช้งานกับเซิร์ฟเวอร์...";
        TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192));

        try
        {
            var result = await _api.ActivateLicenseAsync(key);

            // Refresh store info
            _storeInfo = await _api.GetStoreInfoAsync();
            UpdateDisplay();

            TxtStatusMessage.Text = $"[ เปิดใช้งานสำเร็จ ] สิทธิ์การใช้งานของร้านได้รับการอัปเดตเรียบร้อยแล้ว ({result.SubscriptionPlan})";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));

            MessageBox.Show(
                this,
                $"เปิดใช้งานสิทธิ์ร้าน {_api.StoreCode} สำเร็จแล้ว!\nแผน: {result.SubscriptionPlan}\nวันหมดอายุ: {(result.ExpiresAt.HasValue ? result.ExpiresAt.Value.ToString("dd/MM/yyyy") : "ไม่มีวันหมดอายุ (ตลอดชีพ)")}",
                "เปิดใช้งานสำเร็จ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtStatusMessage.Text = "ไม่สามารถเปิดใช้งานได้: " + ex.Message;
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }
        finally
        {
            BtnActivate.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
