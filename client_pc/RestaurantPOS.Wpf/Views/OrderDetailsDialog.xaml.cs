using System;
using System.Windows;
using System.Windows.Media;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class OrderDetailsDialog : Window
{
    private readonly OrderDto _order;
    private readonly ApiClient _api;
    private readonly PosConfig _config;

    public bool StatusChanged { get; private set; }

    public OrderDetailsDialog(OrderDto order, ApiClient api, PosConfig config)
    {
        InitializeComponent();
        _order = order;
        _api = api;
        _config = config;

        PopulateOrderDetails();
    }

    private void PopulateOrderDetails()
    {
        TxtHeaderTitle.Text = $"รายละเอียดคำสั่งซื้อ: {_order.OrderNumber}";
        TxtOrderNumber.Text = _order.OrderNumber;
        TxtTableInfo.Text = _order.TableDisplay;
        TxtOrderTime.Text = _order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

        UpdateStatusBadge(_order.Status);

        // Customer details
        TxtCustomerName.Text = !string.IsNullOrWhiteSpace(_order.CustomerName) ? _order.CustomerName : "ลูกค้าทั่วไป";
        if (!string.IsNullOrWhiteSpace(_order.CustomerPhone))
        {
            TxtCustomerPhone.Text = _order.CustomerPhone;
            TxtCustomerPhone.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)); // Blue
        }
        else
        {
            TxtCustomerPhone.Text = "[ ไม่ได้ระบุเบอร์โทร ]";
            TxtCustomerPhone.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // Red
        }

        // Customer Order Notes
        if (!string.IsNullOrWhiteSpace(_order.Notes))
        {
            PanelOrderNotes.Visibility = Visibility.Visible;
            TxtOrderNotes.Text = _order.Notes;
        }
        else
        {
            PanelOrderNotes.Visibility = Visibility.Collapsed;
        }

        // Items Grid
        GridOrderItems.ItemsSource = _order.Items;

        // Total
        TxtTotalAmount.Text = $"{_order.TotalAmount:N2} บาท";
    }

    private void UpdateStatusBadge(OrderStatus status)
    {
        _order.Status = status;
        TxtHeaderStatus.Text = _order.StatusBadge;
        TxtOrderStatus.Text = _order.StatusBadge;

        switch (status)
        {
            case OrderStatus.New:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
                BtnAcceptOrder.IsEnabled = true;
                BtnReadyOrder.IsEnabled = false;
                break;
            case OrderStatus.Accepted:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)); // Blue
                BtnAcceptOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = true;
                break;
            case OrderStatus.Ready:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(234, 88, 12)); // Orange
                BtnAcceptOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                break;
            case OrderStatus.Completed:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // Green
                BtnAcceptOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                break;
            default:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                break;
        }
    }

    private async void BtnAcceptOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnAcceptOrder.IsEnabled = false;
            await _api.UpdateOrderStatusAsync(_order.Id, OrderStatus.Accepted);
            StatusChanged = true;
            UpdateStatusBadge(OrderStatus.Accepted);
            MessageBox.Show("รับออเดอร์เรียบร้อยแล้ว ส่งไปยังแผนกครัวแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[OrderDetails] Failed to accept order: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถรับออเดอร์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnAcceptOrder.IsEnabled = true;
        }
    }

    private async void BtnReadyOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnReadyOrder.IsEnabled = false;
            await _api.UpdateOrderStatusAsync(_order.Id, OrderStatus.Ready);
            StatusChanged = true;
            UpdateStatusBadge(OrderStatus.Ready);
            MessageBox.Show("เปลี่ยนสถานะเป็น [ปรุงเสร็จแล้ว] พร้อมเสิร์ฟหรือส่งมอบ", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[OrderDetails] Failed to update ready status: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถอัปเดตสถานะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnReadyOrder.IsEnabled = true;
        }
    }

    private void BtnPrintKitchen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PrintingService.PrintKitchenSlip(_order, _config.KitchenPrinterName);
            MessageBox.Show("ส่งคำสั่งพิมพ์สลิปครัวเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[OrderDetails] Failed to print kitchen slip: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถพิมพ์สลิปครัวได้: " + ex.Message, "ข้อผิดพลาดการพิมพ์", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = StatusChanged;
        Close();
    }
}
