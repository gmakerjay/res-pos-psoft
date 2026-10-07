using System.Windows;

namespace RestaurantPOS.Wpf.Views;

public partial class ErrorDialog : Window
{
    private bool _showDetails = false;

    public ErrorDialog(string title, string message, string? technicalDetails = null)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        TxtMessage.Text = message;
        if (!string.IsNullOrWhiteSpace(technicalDetails))
        {
            TxtDetails.Text = technicalDetails;
            BtnToggleDetails.Visibility = Visibility.Visible;
        }
        else
        {
            BtnToggleDetails.Visibility = Visibility.Collapsed;
        }
    }

    public static void Show(string message, string title = "ข้อผิดพลาดของระบบ", string? technicalDetails = null, Window? owner = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new ErrorDialog(title, message, technicalDetails);
            if (owner != null && owner.IsVisible)
            {
                dlg.Owner = owner;
            }
            dlg.ShowDialog();
        });
    }

    private void BtnToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        _showDetails = !_showDetails;
        TxtDetails.Visibility = _showDetails ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleDetails.Content = _showDetails ? "ซ่อนรายละเอียด" : "ดูรายละเอียด...";
        Height = _showDetails ? 400 : 280;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
