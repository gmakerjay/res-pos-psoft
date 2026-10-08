using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class TableReservationDialog : Window
{
    private readonly ApiClient _api;
    private readonly RealtimeClient _realtime;
    private List<TableDto> _tables = new();
    private List<OrderDto> _liveOrders = new();
    private TableDto? _selectedTable;

    public string? SelectedTableNumberToOrder { get; private set; }

    public TableReservationDialog(ApiClient api, RealtimeClient realtime, List<OrderDto> liveOrders)
    {
        InitializeComponent();
        _api = api;
        _realtime = realtime;
        _liveOrders = liveOrders ?? new List<OrderDto>();

        Loaded += TableReservationDialog_Loaded;
        Closing += TableReservationDialog_Closing;
    }

    private async void TableReservationDialog_Loaded(object sender, RoutedEventArgs e)
    {
        _realtime.TableStatusUpdated += Realtime_TableStatusUpdated;
        await LoadTablesAsync();
    }

    private void TableReservationDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _realtime.TableStatusUpdated -= Realtime_TableStatusUpdated;
    }

    private void Realtime_TableStatusUpdated(TableDto updatedTable)
    {
        Dispatcher.Invoke(async () =>
        {
            await LoadTablesAsync();
        });
    }

    public async Task LoadTablesAsync()
    {
        try
        {
            _tables = await _api.GetTablesAsync();
            UpdateSummaryBadges();
            RenderTableCards();

            // Reselect previously selected table if still present
            if (_selectedTable != null)
            {
                var match = _tables.FirstOrDefault(t => t.Id == _selectedTable.Id);
                SelectTable(match);
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[TableReservationDialog] LoadTablesAsync error: " + ex.Message, ex);
        }
    }

    private void UpdateSummaryBadges()
    {
        var countAvail = _tables.Count(t => t.Status == TableStatus.Available);
        var countRes = _tables.Count(t => t.Status == TableStatus.Reserved);
        var countOcc = _tables.Count(t => t.Status == TableStatus.Occupied);

        TxtCountAvailable.Text = $"[ว่าง: {countAvail}]";
        TxtCountReserved.Text = $"[จองแล้ว: {countRes}]";
        TxtCountOccupied.Text = $"[ทำงานอยู่: {countOcc}]";
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        RenderTableCards();
    }

    private void RenderTableCards()
    {
        PanelTableCards.Children.Clear();

        IEnumerable<TableDto> filtered = _tables;
        if (RbFilterAvailable?.IsChecked == true)
        {
            filtered = _tables.Where(t => t.Status == TableStatus.Available);
        }
        else if (RbFilterReserved?.IsChecked == true)
        {
            filtered = _tables.Where(t => t.Status == TableStatus.Reserved);
        }
        else if (RbFilterOccupied?.IsChecked == true)
        {
            filtered = _tables.Where(t => t.Status == TableStatus.Occupied);
        }

        foreach (var t in filtered)
        {
            var isCurrentSelected = _selectedTable?.Id == t.Id;

            var card = new Border
            {
                Width = 135,
                Height = 110,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(isCurrentSelected ? 2.5 : 1.5),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8),
                Tag = t
            };

            // Color Coding:
            // Reserved -> Yellow
            // Occupied -> Red
            // Available -> Normal (White)
            if (t.Status == TableStatus.Occupied)
            {
                card.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #FEE2E2
                card.BorderBrush = new SolidColorBrush(isCurrentSelected ? Color.FromRgb(185, 28, 28) : Color.FromRgb(220, 38, 38));
            }
            else if (t.Status == TableStatus.Reserved)
            {
                card.Background = new SolidColorBrush(Color.FromRgb(254, 240, 138)); // #FEF08A
                card.BorderBrush = new SolidColorBrush(isCurrentSelected ? Color.FromRgb(180, 83, 9) : Color.FromRgb(234, 179, 8));
            }
            else
            {
                card.Background = Brushes.White;
                card.BorderBrush = new SolidColorBrush(isCurrentSelected ? Color.FromRgb(29, 78, 216) : Color.FromRgb(203, 213, 225));
            }

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Table Number & Capacity
            var topPanel = new DockPanel();
            var txtNo = new TextBlock
            {
                Text = t.TableNumber,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = t.Status == TableStatus.Occupied 
                    ? new SolidColorBrush(Color.FromRgb(153, 27, 27))
                    : (t.Status == TableStatus.Reserved 
                        ? new SolidColorBrush(Color.FromRgb(133, 77, 14)) 
                        : new SolidColorBrush(Color.FromRgb(15, 23, 42)))
            };
            var txtCap = new TextBlock
            {
                Text = $"{t.Capacity} ที่นั่ง",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(txtCap, Dock.Right);
            topPanel.Children.Add(txtCap);
            topPanel.Children.Add(txtNo);
            Grid.SetRow(topPanel, 0);
            grid.Children.Add(topPanel);

            // Row 1: Middle info (Table Name or Reservation Name)
            var midPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 3) };
            if (t.Status == TableStatus.Reserved)
            {
                var resLabel = new TextBlock
                {
                    Text = !string.IsNullOrWhiteSpace(t.ReservationCustomerName) ? t.ReservationCustomerName : "จองแล้ว",
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(133, 77, 14)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                var resTime = new TextBlock
                {
                    Text = t.ReservationTime.HasValue ? t.ReservationTime.Value.ToLocalTime().ToString("HH:mm น.") : "",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7))
                };
                midPanel.Children.Add(resLabel);
                if (!string.IsNullOrEmpty(resTime.Text)) midPanel.Children.Add(resTime);
            }
            else if (t.Status == TableStatus.Occupied)
            {
                var occLabel = new TextBlock
                {
                    Text = t.CurrentBillAmount > 0 ? $"{t.CurrentBillAmount:N0} บ." : "มีลูกค้า",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27))
                };
                midPanel.Children.Add(occLabel);
            }
            else
            {
                var nameText = new TextBlock
                {
                    Text = !string.IsNullOrWhiteSpace(t.Name) ? t.Name : "โต๊ะว่าง",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                midPanel.Children.Add(nameText);
            }
            Grid.SetRow(midPanel, 1);
            grid.Children.Add(midPanel);

            // Row 2: Bottom Badge
            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var badgeText = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };

            if (t.Status == TableStatus.Occupied)
            {
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(254, 202, 202));
                badgeText.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
                badgeText.Text = "[มีลูกค้า]";
            }
            else if (t.Status == TableStatus.Reserved)
            {
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(253, 230, 138));
                badgeText.Foreground = new SolidColorBrush(Color.FromRgb(133, 77, 14));
                badgeText.Text = "[จองแล้ว]";
            }
            else
            {
                badgeBorder.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                badgeText.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                badgeText.Text = "[ว่าง]";
            }
            badgeBorder.Child = badgeText;
            Grid.SetRow(badgeBorder, 2);
            grid.Children.Add(badgeBorder);

            card.Child = grid;
            card.MouseLeftButtonUp += (s, ev) =>
            {
                if (card.Tag is TableDto clicked)
                {
                    SelectTable(clicked);
                }
            };

            PanelTableCards.Children.Add(card);
        }
    }

    private void SelectTable(TableDto? table)
    {
        _selectedTable = table;

        if (table == null)
        {
            TxtSelectedTableTitle.Text = "กรุณาเลือกโต๊ะในผัง";
            TxtSelectedCapacity.Text = "ความจุ: - ที่นั่ง";
            TxtSelectedStatus.Text = "[ไม่มีการเลือก]";
            BadgeSelectedStatus.Background = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            TxtSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));

            PanelReservedDetails.Visibility = Visibility.Collapsed;
            PanelNewReservationForm.Visibility = Visibility.Collapsed;
            PanelOccupiedDetails.Visibility = Visibility.Collapsed;
            TxtNoTableSelectedNotice.Visibility = Visibility.Visible;
            return;
        }

        TxtNoTableSelectedNotice.Visibility = Visibility.Collapsed;
        TxtSelectedTableTitle.Text = $"{table.TableNumber} ({table.Name})";
        TxtSelectedCapacity.Text = $"ความจุรองรับ: {table.Capacity} ที่นั่ง";

        if (table.Status == TableStatus.Reserved)
        {
            TxtSelectedStatus.Text = "[จองแล้ว]";
            BadgeSelectedStatus.Background = new SolidColorBrush(Color.FromRgb(254, 240, 138));
            TxtSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(133, 77, 14));

            TxtDetailCustName.Text = !string.IsNullOrWhiteSpace(table.ReservationCustomerName) ? table.ReservationCustomerName : "-";
            TxtDetailCustPhone.Text = !string.IsNullOrWhiteSpace(table.ReservationCustomerPhone) ? table.ReservationCustomerPhone : "-";
            TxtDetailResTime.Text = table.ReservationTime.HasValue ? table.ReservationTime.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm น.") : "-";
            TxtDetailPartySize.Text = $"{table.ReservationPartySize ?? table.Capacity} ท่าน";
            TxtDetailNotes.Text = !string.IsNullOrWhiteSpace(table.ReservationNotes) ? table.ReservationNotes : "-";

            PanelReservedDetails.Visibility = Visibility.Visible;
            PanelNewReservationForm.Visibility = Visibility.Collapsed;
            PanelOccupiedDetails.Visibility = Visibility.Collapsed;
        }
        else if (table.Status == TableStatus.Occupied)
        {
            TxtSelectedStatus.Text = "[ทำงานอยู่ / มีลูกค้า]";
            BadgeSelectedStatus.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
            TxtSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));

            TxtOccupiedBillInfo.Text = table.CurrentBillAmount > 0
                ? $"ยอดรวมบิลปัจจุบัน: {table.CurrentBillAmount:N2} บ."
                : "ยังไม่มียอดสั่งอาหาร";

            TxtOccupiedTimeInfo.Text = table.SeatedAt.HasValue
                ? $"เริ่มนั่งเมื่อ: {table.SeatedAt.Value.ToLocalTime():HH:mm น.}"
                : "เริ่มนั่งเมื่อ: วันนี้";

            PanelReservedDetails.Visibility = Visibility.Collapsed;
            PanelNewReservationForm.Visibility = Visibility.Collapsed;
            PanelOccupiedDetails.Visibility = Visibility.Visible;
        }
        else
        {
            // Available
            TxtSelectedStatus.Text = "[โต๊ะว่าง]";
            BadgeSelectedStatus.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            TxtSelectedStatus.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));

            TxtInputCustName.Text = "";
            TxtInputCustPhone.Text = "";
            TxtInputResTime.Text = DateTime.Now.AddHours(1).ToString("HH:mm");
            TxtInputPartySize.Text = table.Capacity.ToString();
            TxtInputNotes.Text = "";

            PanelReservedDetails.Visibility = Visibility.Collapsed;
            PanelNewReservationForm.Visibility = Visibility.Visible;
            PanelOccupiedDetails.Visibility = Visibility.Collapsed;
        }

        // Re-render highlight outline
        RenderTableCards();
    }

    private async void BtnSaveReservation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;

        var name = TxtInputCustName.Text.Trim();
        var phone = TxtInputCustPhone.Text.Trim();
        var timeStr = TxtInputResTime.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("กรุณาระบุชื่อลูกค้าผู้จอง", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            MessageBox.Show("กรุณาระบุเบอร์โทรศัพท์ติดต่อ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateTime reservationTime = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(timeStr))
        {
            if (TimeSpan.TryParse(timeStr, out var parsedSpan))
            {
                var now = DateTime.Now;
                reservationTime = new DateTime(now.Year, now.Month, now.Day, parsedSpan.Hours, parsedSpan.Minutes, 0).ToUniversalTime();
            }
        }

        int.TryParse(TxtInputPartySize.Text.Trim(), out var partySize);
        if (partySize <= 0) partySize = _selectedTable.Capacity;

        var req = new ReserveTableRequest
        {
            CustomerName = name,
            CustomerPhone = phone,
            ReservationTime = reservationTime,
            PartySize = partySize,
            Notes = TxtInputNotes.Text.Trim()
        };

        try
        {
            var updated = await _api.ReserveTableAsync(_selectedTable.Id, req);
            MessageBox.Show($"บันทึกการจองโต๊ะ {_selectedTable.TableNumber} เรียบร้อยแล้ว (สีเหลือง)", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadTablesAsync();
            if (updated != null) SelectTable(updated);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[ReserveTable] Error: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถบันทึกการจองได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDirectOccupy_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;

        try
        {
            var updated = await _api.UpdateTableStatusAsync(_selectedTable.Id, TableStatus.Occupied);
            await LoadTablesAsync();
            if (updated != null) SelectTable(updated);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[DirectOccupy] Error: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเปิดโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCheckInReserved_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;

        var confirm = MessageBox.Show($"ต้องการเช็คอินลูกค้าเข้าโต๊ะ {_selectedTable.TableNumber} เพื่อเริ่มสั่งอาหารใน POS เลยหรือไม่?", "ยืนยันการเช็คอิน", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var updated = await _api.CheckInTableAsync(_selectedTable.Id);
            SelectedTableNumberToOrder = _selectedTable.TableNumber;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[CheckInReserved] Error: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเช็คอินเข้าโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCancelReservation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;

        var confirm = MessageBox.Show($"ต้องการยกเลิกการจองโต๊ะ {_selectedTable.TableNumber} ใช่หรือไม่?\nสถานะจะเปลี่ยนกลับเป็นโต๊ะว่าง (สีปกติ)", "ยืนยันการยกเลิกจอง", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var updated = await _api.CancelTableReservationAsync(_selectedTable.Id);
            MessageBox.Show($"ยกเลิกการจองโต๊ะ {_selectedTable.TableNumber} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadTablesAsync();
            if (updated != null) SelectTable(updated);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[CancelReservation] Error: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถยกเลิกการจองได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSelectTableInPos_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;
        SelectedTableNumberToOrder = _selectedTable.TableNumber;
        DialogResult = true;
        Close();
    }

    private async void BtnClearOccupiedTable_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null) return;

        var confirm = MessageBox.Show($"ต้องการสลับสถานะโต๊ะ {_selectedTable.TableNumber} เป็นโต๊ะว่าง ใช่หรือไม่?", "ยืนยัน", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var updated = await _api.UpdateTableStatusAsync(_selectedTable.Id, TableStatus.Available);
            await LoadTablesAsync();
            if (updated != null) SelectTable(updated);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[ClearOccupiedTable] Error: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเปลี่ยนสถานะโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTablesAsync();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
