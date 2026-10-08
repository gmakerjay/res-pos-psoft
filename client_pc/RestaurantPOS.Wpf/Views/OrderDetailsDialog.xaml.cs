using System;
using System.Windows;
using System.Windows.Controls;
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
    private readonly RealtimeClient? _realtime;
    private bool _isProcessing;

    public bool StatusChanged { get; private set; }

    public OrderDetailsDialog(OrderDto order, ApiClient api, PosConfig config, RealtimeClient? realtime = null)
    {
        InitializeComponent();
        _order = order;
        _api = api;
        _config = config;
        _realtime = realtime;

        PopulateOrderDetails();

        if (_realtime != null)
        {
            _realtime.OrderStatusUpdated += OnRealtimeOrderStatusUpdated;
        }

        Closed += (s, e) =>
        {
            if (_realtime != null)
            {
                _realtime.OrderStatusUpdated -= OnRealtimeOrderStatusUpdated;
            }
        };
    }

    private void OnRealtimeOrderStatusUpdated(OrderDto updated)
    {
        if (updated.Id == _order.Id)
        {
            Dispatcher.Invoke(() =>
            {
                StatusChanged = true;
                UpdateStatusBadge(updated.Status);
            });
        }
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

        // Reset button texts & opacities
        BtnAcceptOrder.Content = "[ รับออเดอร์ ]";
        BtnAcceptOrder.Opacity = 1.0;
        BtnPrepareOrder.Content = "[ กำลังปรุง ]";
        BtnPrepareOrder.Opacity = 1.0;
        BtnReadyOrder.Content = "[ ปรุงเสร็จแล้ว ]";
        BtnReadyOrder.Opacity = 1.0;
        BtnCompleteOrder.Content = "[ เสิร์ฟแล้ว ]";
        BtnCompleteOrder.Opacity = 1.0;
        BtnCancelOrder.Content = "[ ยกเลิกบิล ]";
        BtnCancelOrder.Opacity = 1.0;

        switch (status)
        {
            case OrderStatus.New:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red
                BtnAcceptOrder.IsEnabled = true;
                BtnPrepareOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                BtnCompleteOrder.IsEnabled = false;
                BtnCancelOrder.IsEnabled = true;
                break;
            case OrderStatus.Accepted:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)); // Blue
                BtnAcceptOrder.IsEnabled = false;
                BtnPrepareOrder.IsEnabled = true;
                BtnReadyOrder.IsEnabled = false;
                BtnCompleteOrder.IsEnabled = false;
                BtnCancelOrder.IsEnabled = true;
                break;
            case OrderStatus.Preparing:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Amber
                BtnAcceptOrder.IsEnabled = false;
                BtnPrepareOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = true;
                BtnCompleteOrder.IsEnabled = false;
                BtnCancelOrder.IsEnabled = true;
                break;
            case OrderStatus.Ready:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(234, 88, 12)); // Orange
                BtnAcceptOrder.IsEnabled = false;
                BtnPrepareOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                BtnCompleteOrder.IsEnabled = true;
                BtnCancelOrder.IsEnabled = false;
                break;
            case OrderStatus.Completed:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // Green
                BtnAcceptOrder.IsEnabled = false;
                BtnPrepareOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                BtnCompleteOrder.IsEnabled = false;
                BtnCancelOrder.IsEnabled = false;
                break;
            case OrderStatus.Cancelled:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Gray
                BtnAcceptOrder.IsEnabled = false;
                BtnPrepareOrder.IsEnabled = false;
                BtnReadyOrder.IsEnabled = false;
                BtnCompleteOrder.IsEnabled = false;
                BtnCancelOrder.IsEnabled = false;
                break;
            default:
                TxtOrderStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                break;
        }
    }

    private async Task ExecuteStatusChangeAsync(Button triggerButton, OrderStatus newStatus, string successMessage)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        var originalContent = triggerButton.Content;
        triggerButton.Content = "[ กำลังบันทึก... ]";
        triggerButton.Opacity = 0.55;
        triggerButton.IsEnabled = false;

        try
        {
            var operatorName = _api.CurrentUser?.FullName ?? _api.CurrentUser?.Username ?? "แคชเชียร์ POS";
            await _api.UpdateOrderStatusAsync(_order.Id, newStatus, operatorName, "POS");
            StatusChanged = true;
            UpdateStatusBadge(newStatus);
            MessageBox.Show(successMessage, "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[OrderDetails] Failed to change status to {newStatus}: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเปลี่ยนสถานะออเดอร์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            triggerButton.Content = originalContent;
            triggerButton.Opacity = 1.0;
            triggerButton.IsEnabled = true;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async void BtnAcceptOrder_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusChangeAsync(BtnAcceptOrder, OrderStatus.Accepted, "รับออเดอร์เรียบร้อยแล้ว ส่งไปยังแผนกครัวแล้ว");
    }

    private async void BtnPrepareOrder_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusChangeAsync(BtnPrepareOrder, OrderStatus.Preparing, "เปลี่ยนสถานะเป็น [กำลังปรุง] เรียบร้อยแล้ว");
    }

    private async void BtnReadyOrder_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusChangeAsync(BtnReadyOrder, OrderStatus.Ready, "เปลี่ยนสถานะเป็น [ปรุงเสร็จแล้ว] พร้อมเสิร์ฟหรือส่งมอบ");
    }

    private async void BtnCompleteOrder_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteStatusChangeAsync(BtnCompleteOrder, OrderStatus.Completed, "เปลี่ยนสถานะเป็น [เสิร์ฟแล้ว/เสร็จสิ้น] เรียบร้อย");
    }

    private async void BtnCancelOrder_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show($"ต้องการยกเลิกคำสั่งซื้อ {_order.OrderNumber} ใช่หรือไม่?", 
            "ยืนยันการยกเลิก", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            await ExecuteStatusChangeAsync(BtnCancelOrder, OrderStatus.Cancelled, "ยกเลิกคำสั่งซื้อเรียบร้อยแล้ว");
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
