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
        if (string.IsNullOrEmpty(TxtPassword.Password))
        {
            TxtPassword.Password = "psoft123";
        }
        Loaded += (s, e) => BtnLogin.Focus();
    }

    private void BtnQuickDemo_Click(object sender, RoutedEventArgs e)
    {
        TxtUsername.Text = "admin";
        TxtPassword.Password = "psoft123";
        BtnLogin_Click(sender, e);
    }

    private void UpdateSubtitle()
    {
        TxtLoginSubtitle.Text = $"เข้าสู่ระบบประจำร้าน: {_config.StoreCode} (เซิร์ฟเวอร์: {_config.GetBaseUrl()})";
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
            var user = await _api.LoginAsync(username, password);
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
            UpdateSubtitle();
            MessageBox.Show(
                $"บันทึกการตั้งค่าเรียบร้อยแล้ว [ร้านค้า: {_config.StoreCode}]",
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
