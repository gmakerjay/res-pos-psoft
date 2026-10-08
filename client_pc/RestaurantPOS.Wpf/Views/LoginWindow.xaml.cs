using System.Windows;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly ApiClient _api;
    private readonly PosConfig _config;

    public UserDto? AuthenticatedUser { get; private set; }

    public LoginWindow(ApiClient api, PosConfig config)
    {
        InitializeComponent();
        _api = api;
        _config = config;
        UpdateSubtitle();
        if (string.IsNullOrEmpty(TxtUsername.Text))
        {
            TxtUsername.Text = "admin";
        }
        if (_config.StoreCode == "DEFAULT")
        {
            if (string.IsNullOrEmpty(TxtPassword.Password))
            {
                TxtPassword.Password = "psoft123";
            }
        }
        else
        {
            TxtPassword.Password = "";
        }
        Loaded += (s, e) => 
        {
            if (string.IsNullOrEmpty(TxtPassword.Password))
            {
                TxtPassword.Focus();
            }
            else
            {
                BtnLogin.Focus();
            }
        };
    }

    private void BtnQuickDemo_Click(object sender, RoutedEventArgs e)
    {
        _config.StoreCode = "DEFAULT";
        _api.UpdateConnection(_config.GetBaseUrl(), "DEFAULT");
        UpdateSubtitle();
        TxtUsername.Text = "admin";
        TxtPassword.Password = "psoft123";
        BtnLogin_Click(sender, e);
    }

    private void UpdateSubtitle()
    {
        TxtLoginSubtitle.Text = $"เข้าสู่ระบบประจำร้าน: {_config.StoreCode} (เซิร์ฟเวอร์: {_config.GetBaseUrl()})";
        if (_config.StoreCode == "DEFAULT")
        {
            TxtPasswordHint.Text = "* รหัสผ่านร้านตัวอย่าง: psoft123 (หรือ 123456)";
        }
        else
        {
            TxtPasswordHint.Text = $"* ร้าน {_config.StoreCode}: ใช้รหัสผ่านที่คุณตั้งไว้ตอนลงทะเบียน (หรือ 123456)";
        }
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        var username = TxtUsername.Text.Trim();
        var password = TxtPassword.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("กรุณากรอกชื่อผู้ใช้และรหัสผ่าน");
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnLogin.Content = "กำลังตรวจสอบ...";
        TxtError.Visibility = Visibility.Collapsed;

        try
        {
            var user = await _api.LoginAsync(username, password, _config.StoreCode);
            AuthenticatedUser = user;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            TxtPassword.SelectAll();
            TxtPassword.Focus();
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "เข้าสู่ระบบ (Login)";
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }

    private void BtnConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ServerConfigDialog(_config);
        if (dialog.ShowDialog() == true)
        {
            _api.UpdateConnection(_config.GetBaseUrl(), _config.StoreCode);
            UpdateSubtitle();
            if (_config.StoreCode != "DEFAULT")
            {
                TxtPassword.Password = "";
                TxtPassword.Focus();
            }
            MessageBox.Show(
                $"บันทึกการตั้งค่าเรียบร้อยแล้ว [ร้านค้า: {_config.StoreCode}]\nระบบอัปเดตการเชื่อมต่อไปยังร้านค้านี้แล้ว กรุณาเข้าสู่ระบบ",
                "บันทึกสำเร็จ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
