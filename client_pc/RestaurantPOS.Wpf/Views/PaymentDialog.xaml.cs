using System.Windows;
using System.Windows.Controls;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;

namespace RestaurantPOS.Wpf.Views;

public class PaymentResult
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal PaidAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public bool ShouldPrintReceipt { get; set; }
    public bool ShouldPrintKitchen { get; set; }
}

public partial class PaymentDialog : Window
{
    private readonly OrderDto _order;
    private decimal _subtotal;
    private decimal _discount;
    private decimal _netTotal;
    private decimal _cashReceived;

    public PaymentResult? Result { get; private set; }

    public PaymentDialog(OrderDto order)
    {
        InitializeComponent();
        _order = order;
        _subtotal = order.Subtotal > 0 ? order.Subtotal : order.Items.Sum(i => i.Subtotal);
        _discount = order.DiscountAmount;
        _netTotal = Math.Max(0, _subtotal - _discount);

        TxtBillTitle.Text = $"ชำระเงิน: {order.TableNumber ?? "สั่งกลับบ้าน"} ({order.OrderNumber})";
        TxtItemsCount.Text = $"{order.Items.Sum(i => i.Quantity)} รายการ";
        TxtSubtotal.Text = $"{_subtotal:N2} บาท";
        TxtDiscount.Text = _discount > 0 ? _discount.ToString("0.##") : "0";
        TxtTotal.Text = $"{_netTotal:N2} บาท";

        TxtCashReceived.Text = _netTotal.ToString("0.##");
        Recalculate();
    }

    private void Recalculate()
    {
        if (TxtDiscount == null || TxtTotal == null || TxtCashReceived == null || TxtChange == null || RbCash == null) 
            return;

        decimal.TryParse(TxtDiscount.Text.Trim(), out _discount);
        _netTotal = Math.Max(0, _subtotal - _discount);
        TxtTotal.Text = $"{_netTotal:N2} บาท";

        decimal.TryParse(TxtCashReceived.Text.Trim(), out _cashReceived);

        if (RbCash.IsChecked == true)
        {
            var change = Math.Max(0, _cashReceived - _netTotal);
            TxtChange.Text = $"{change:N2} บาท";
        }
        else
        {
            _cashReceived = _netTotal;
            TxtCashReceived.Text = _netTotal.ToString("0.##");
            TxtChange.Text = "0.00 บาท";
        }
    }

    private void TxtDiscount_TextChanged(object sender, TextChangedEventArgs e)
    {
        Recalculate();
    }

    private void TxtCashReceived_TextChanged(object sender, TextChangedEventArgs e)
    {
        Recalculate();
    }

    private void PaymentMethod_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelCash == null) return;

        if (RbCash.IsChecked == true)
        {
            PanelCash.Visibility = Visibility.Visible;
        }
        else
        {
            PanelCash.Visibility = Visibility.Collapsed;
        }
        Recalculate();
    }

    private void BtnQuickCash_Exact(object sender, RoutedEventArgs e)
    {
        TxtCashReceived.Text = _netTotal.ToString("0.##");
    }

    private void BtnQuickCash_100(object sender, RoutedEventArgs e)
    {
        AddCash(100);
    }

    private void BtnQuickCash_500(object sender, RoutedEventArgs e)
    {
        AddCash(500);
    }

    private void BtnQuickCash_1000(object sender, RoutedEventArgs e)
    {
        AddCash(1000);
    }

    private void AddCash(decimal amount)
    {
        decimal.TryParse(TxtCashReceived.Text.Trim(), out var current);
        TxtCashReceived.Text = (current + amount).ToString("0.##");
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        Recalculate();

        var method = PaymentMethod.Cash;
        if (RbPromptPay.IsChecked == true) method = PaymentMethod.PromptPayQR;
        else if (RbCard.IsChecked == true) method = PaymentMethod.CreditCard;
        else if (RbTransfer.IsChecked == true) method = PaymentMethod.Transfer;

        if (method == PaymentMethod.Cash && _cashReceived < _netTotal)
        {
            MessageBox.Show($"ยอดเงินสดที่รับมา ({_cashReceived:N2} บาท) น้อยกว่ายอดสุทธิ ({_netTotal:N2} บาท)",
                "ยอดเงินไม่เพียงพอ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new PaymentResult
        {
            Method = method,
            PaidAmount = method == PaymentMethod.Cash ? _cashReceived : _netTotal,
            DiscountAmount = _discount,
            TotalAmount = _netTotal,
            ChangeAmount = method == PaymentMethod.Cash ? Math.Max(0, _cashReceived - _netTotal) : 0,
            ShouldPrintReceipt = ChkPrintReceipt.IsChecked == true,
            ShouldPrintKitchen = ChkPrintKitchen.IsChecked == true
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
