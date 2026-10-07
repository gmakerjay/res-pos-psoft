using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Views;

namespace RestaurantPOS.Wpf.Services;

public static class ScreenExporter
{
    public static void ExportScreens(string outputDir)
    {
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(outputDir);

        // 1. Export Main POS Window
        var mainWindow = new MainWindow();
        mainWindow.Width = 1380;
        mainWindow.Height = 820;
        mainWindow.Show();

        // Render Main POS
        SaveWindowImage(mainWindow, Path.Combine(outputDir, "01_pos_main_screen.png"), 1380, 820);

        // Switch to KDS / Orders tab
        mainWindow.TabOrders.IsChecked = true;
        SaveWindowImage(mainWindow, Path.Combine(outputDir, "02_kds_orders_screen.png"), 1380, 820);

        // Switch to Reports tab
        mainWindow.TabReports.IsChecked = true;
        SaveWindowImage(mainWindow, Path.Combine(outputDir, "03_daily_report_screen.png"), 1380, 820);

        mainWindow.Close();

        // 2. Export Server IP Connection Dialog
        var config = PosConfig.Load();
        var configDlg = new ServerConfigDialog(config);
        configDlg.Width = 520;
        configDlg.Height = 360;
        configDlg.Show();
        SaveWindowImage(configDlg, Path.Combine(outputDir, "04_server_ip_config_dialog.png"), 520, 360);
        configDlg.Close();

        // 3. Export Payment Dialog
        var sampleOrder = new OrderDto
        {
            OrderNumber = "ORD-20261006-0001",
            TableNumber = "T01",
            Subtotal = 350.00m,
            TotalAmount = 350.00m,
            Items = new List<OrderItemDto>
            {
                new() { ProductName = "ผัดไทยกุ้งสด", UnitPrice = 85, Quantity = 2 },
                new() { ProductName = "ต้มยำกุ้งน้ำข้น", UnitPrice = 150, Quantity = 1 },
                new() { ProductName = "ชาไทยเย็น", UnitPrice = 45, Quantity = 1 }
            }
        };
        var payDlg = new PaymentDialog(sampleOrder);
        payDlg.Width = 620;
        payDlg.Height = 540;
        payDlg.Show();
        SaveWindowImage(payDlg, Path.Combine(outputDir, "05_cashier_payment_dialog.png"), 620, 540);
        payDlg.Close();
        Application.Current.Shutdown();
    }

    private static void SaveWindowImage(Window window, string filePath, int width, int height)
    {
        window.Measure(new Size(width, height));
        window.Arrange(new Rect(0, 0, width, height));
        window.UpdateLayout();

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.OpenWrite(filePath);
        encoder.Save(fs);
    }
}
