using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using RestaurantPOS.KeyGen.Services;

namespace RestaurantPOS.KeyGen;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnPasteMachineCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = Clipboard.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                TxtMachineCode.Text = text.Trim();
                TxtStatus.Text = "[ วางรหัสเครื่องเรียบร้อยแล้ว ]";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ไม่สามารถเข้าถึงคลิปบอร์ดได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnDetectThisPc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var myMachineCode = DetectLocalMachineCode();
            TxtMachineCode.Text = myMachineCode;
            TxtStatus.Text = $"[ ตรวจพบรหัสเครื่องนี้เรียบร้อย: {myMachineCode} ]";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ไม่สามารถดึงข้อมูลฮาร์ดแวร์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnGenerateKey_Click(object sender, RoutedEventArgs e)
    {
        var machineCode = TxtMachineCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(machineCode) || !machineCode.StartsWith("RPOS-", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "กรุณากรอกรหัสเครื่องของลูกค้าให้ถูกต้อง (รูปแบบ: RPOS-XXXX-XXXX-XXXX-XXXX)", "ข้อมูลไม่ถูกต้อง", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtMachineCode.Focus();
            return;
        }

        var storeCode = TxtStoreCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(storeCode)) storeCode = "DEFAULT";

        string plan = "FullLifetime";
        int customDays = 0;

        if (RbLifetime.IsChecked == true)
        {
            plan = "FullLifetime";
        }
        else if (RbYearly.IsChecked == true)
        {
            plan = "FullYearly";
        }
        else if (RbCustomDays.IsChecked == true)
        {
            plan = "CustomDays";
            if (!int.TryParse(TxtCustomDays.Text.Trim(), out customDays) || customDays <= 0)
            {
                MessageBox.Show(this, "กรุณาระบุจำนวนวันให้ถูกต้อง (ตัวเลขมากกว่า 0)", "ข้อมูลไม่ถูกต้อง", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCustomDays.Focus();
                return;
            }
        }

        try
        {
            var generatedKey = KeyGenEngine.GenerateKey(machineCode, storeCode, plan, customDays);
            TxtOutputKey.Text = generatedKey;
            TxtStatus.Text = $"[ สร้าง Activation Key สำเร็จสำหรับร้าน {storeCode} (ผูกกับเครื่อง {machineCode}) ]";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "เกิดข้อผิดพลาดในการสร้างคีย์: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCopyKey_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtOutputKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show(this, "ยังไม่มีรหัสคีย์ที่ถูกสร้าง กรุณากดปุ่มสร้างคีย์ก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(key);
            TxtStatus.Text = "[ คัดลอก Activation Key ไปยังคลิปบอร์ดเรียบร้อยแล้ว ]";
            MessageBox.Show(this, "คัดลอก Activation Key ไปยังคลิปบอร์ดแล้ว ท่านสามารถนำไปส่งให้ลูกค้าได้ทันที", "คัดลอกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ไม่สามารถคัดลอกได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnSaveLicFile_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtOutputKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show(this, "ยังไม่มีรหัสคีย์ที่ถูกสร้าง กรุณากดปุ่มสร้างคีย์ก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "License File (*.lic)|*.lic|All Files (*.*)|*.*",
            FileName = "license.lic",
            Title = "บันทึกไฟล์สิทธิ์การใช้งาน"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(sfd.FileName, key, Encoding.UTF8);
                TxtStatus.Text = $"[ บันทึกไฟล์ {Path.GetFileName(sfd.FileName)} สำเร็จ ]";
                MessageBox.Show(this, $"บันทึกไฟล์สิทธิ์เรียบร้อยที่:\n{sfd.FileName}", "บันทึกสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ไม่สามารถบันทึกไฟล์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string DetectLocalMachineCode()
    {
        var sb = new StringBuilder();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var val = item["ProcessorId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) { sb.Append("CPU:").Append(val.Trim()).Append(';'); break; }
            }
        }
        catch
        {
            sb.Append("CPUE:").Append(Environment.ProcessorCount).Append(':')
              .Append(Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "GENERIC").Append(';');
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var item in searcher.Get())
            {
                var ser = item["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(ser) && ser.Trim() != "To be filled by O.E.M." && ser.Trim() != "None")
                {
                    sb.Append("MB:").Append(ser.Trim()).Append(';'); break;
                }
            }
        }
        catch { }

        try
        {
            var sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var driveInfo = new DriveInfo(sysDrive);
            sb.Append("DRV:").Append(driveInfo.DriveFormat).Append(':')
              .Append(driveInfo.TotalSize / (1024 * 1024)).Append(';');
        }
        catch { }

        try
        {
            using var rk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                      .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = rk?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(guid)) sb.Append("GUID:").Append(guid.Trim()).Append(';');
        }
        catch { }

        if (sb.Length == 0)
        {
            sb.Append("HOST:").Append(Environment.MachineName).Append(';')
              .Append("USER:").Append(Environment.UserName);
        }

        var salt = new byte[] { 0x52, 0x50, 0x4F, 0x53, 0x2D, 0x48, 0x57, 0x49, 0x44, 0x2D, 0x53, 0x45, 0x43, 0x55, 0x52, 0x45 };
        var rawBytes = Encoding.UTF8.GetBytes(sb.ToString());

        using var hmac = new HMACSHA256(salt);
        var hash = hmac.ComputeHash(rawBytes);

        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var codeBuilder = new StringBuilder();

        for (int i = 0; i < 16; i++)
        {
            var idx = hash[i] % alphabet.Length;
            codeBuilder.Append(alphabet[idx]);
        }

        var s = codeBuilder.ToString();
        return $"RPOS-{s.Substring(0, 4)}-{s.Substring(4, 4)}-{s.Substring(8, 4)}-{s.Substring(12, 4)}";
    }
}