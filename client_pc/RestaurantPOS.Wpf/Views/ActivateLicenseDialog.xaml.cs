using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
        TxtLicenseKey.Focus();
    }

    private void UpdateDisplay()
    {
        var machineCode = HardwareFingerprintService.GetMachineCode();
        TxtMachineCode.Text = machineCode;
        TxtStoreCode.Text = !string.IsNullOrWhiteSpace(_api.StoreCode) ? _api.StoreCode : "DEFAULT";

        var status = ClientLicenseService.Instance.GetCurrentLicenseStatus();

        if (status.IsPermanentLifetime)
        {
            TxtCurrentPlan.Text = "[ เวอร์ชันเต็ม ตลอดชีพ (Full Lifetime) ]";
            TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
            TxtPlanDetail.Text = "โปรแกรมได้รับการปลดล็อกสิทธิ์ใช้งานถาวรสำหรับเครื่องนี้แล้ว";
        }
        else if (status.IsValid && !status.IsTrial)
        {
            TxtCurrentPlan.Text = $"[ เวอร์ชันเต็ม รายปี (เหลืออีก {status.DaysRemaining} วัน) ]";
            TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(2, 119, 189));
            TxtPlanDetail.Text = status.StatusDescription;
        }
        else if (status.IsTampered)
        {
            TxtCurrentPlan.Text = "[ สิทธิ์การใช้งานถูกระงับ (ตรวจพบการปรับเวลา) ]";
            TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            TxtPlanDetail.Text = status.StatusDescription;
        }
        else if (status.IsExpired)
        {
            TxtCurrentPlan.Text = "[ สิทธิ์ทดลองใช้งาน 14 วันหมดอายุแล้ว ]";
            TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            TxtPlanDetail.Text = "ครบกำหนดระยะเวลาทดลองใช้ 14 วันจริงนับจากวันแรกที่ติดตั้ง กรุณาส่งรหัสเครื่องด้านบนให้ผู้พัฒนาเพื่อขอ Activation Key";
        }
        else
        {
            TxtCurrentPlan.Text = $"[ สิทธิ์ทดลองใช้งาน: เหลืออีก {status.DaysRemaining} วัน ]";
            TxtCurrentPlan.Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0));
            TxtPlanDetail.Text = $"นับจากวันที่ติดตั้งครั้งแรก ({status.ExpirationDateUtc?.ToLocalTime():dd/MM/yyyy}) วันจริงตามปฏิทิน";
        }
    }

    private void BtnCopyMachineCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtMachineCode.Text);
            TxtStatusMessage.Text = "[ คัดลอกรหัสประจำเครื่องเรียบร้อยแล้ว ส่งรหัสนี้ให้ผู้พัฒนาเพื่อขอคีย์ ]";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192));
            MessageBox.Show(this, "คัดลอกรหัสประจำเครื่องไปยังคลิปบอร์ดแล้ว:\n" + TxtMachineCode.Text + "\n\nกรุณาส่งรหัสนี้ให้ผู้พัฒนาเพื่อออก Activation Key ครับ", "คัดลอกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ไม่สามารถคัดลอกได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnPasteKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                TxtLicenseKey.Text = text.Trim();
                TxtStatusMessage.Text = "[ วางรหัสเปิดใช้งานจากคลิปบอร์ดเรียบร้อย ]";
                TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ไม่สามารถเข้าถึงคลิปบอร์ดได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnLoadLicFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "License Files (*.lic;*.txt)|*.lic;*.txt|All Files (*.*)|*.*",
            Title = "เลือกไฟล์สิทธิ์การใช้งาน (license.lic)"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(ofd.FileName, System.Text.Encoding.UTF8).Trim();
                TxtLicenseKey.Text = content;
                TxtStatusMessage.Text = $"[ โหลดไฟล์ {Path.GetFileName(ofd.FileName)} เรียบร้อย ]";
                TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ไม่สามารถอ่านไฟล์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnActivate_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtLicenseKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            TxtStatusMessage.Text = "กรุณากรอกหรือวางรหัสเปิดใช้งาน (Activation Key)";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            return;
        }

        BtnActivate.IsEnabled = false;
        TxtStatusMessage.Text = "กำลังตรวจสอบลายเซ็นดิจิทัลประจำเครื่อง...";
        TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192));

        try
        {
            var result = ClientLicenseService.Instance.ActivateKey(key);

            if (!result.IsValid)
            {
                TxtStatusMessage.Text = "[ การเปิดใช้งานไม่สำเร็จ ] " + result.StatusDescription;
                TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                MessageBox.Show(this, result.StatusDescription, "เปิดใช้งานไม่สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sync with server if connected
            _ = Task.Run(async () =>
            {
                try
                {
                    await _api.ActivateLicenseAsync(key);
                }
                catch { }
            });

            UpdateDisplay();

            TxtStatusMessage.Text = $"[ เปิดใช้งานสำเร็จ ] {result.StatusDescription}";
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));

            MessageBox.Show(
                this,
                $"เปิดใช้งานสิทธิ์โปรแกรมบนเครื่องนี้สำเร็จแล้ว!\nแผน: {result.PlanName}\nรายละเอียด: {result.StatusDescription}\nรหัสเครื่อง: {result.MachineCode}",
                "เปิดใช้งานสำเร็จ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtStatusMessage.Text = "เกิดข้อผิดพลาด: " + ex.Message;
            TxtStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }
        finally
        {
            BtnActivate.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
