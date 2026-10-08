using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Services;
using RestaurantPOS.Wpf.Views;

namespace RestaurantPOS.Wpf;

public partial class MainWindow : Window
{
    private PosConfig _config;
    private ApiClient _api;
    private RealtimeClient _realtime;
    private DispatcherTimer _clockTimer;

    // In-memory data
    private List<CategoryDto> _categories = new();
    private List<ProductDto> _products = new();
    private List<IngredientDto> _ingredients = new();
    private ObservableCollection<OrderItemDto> _cartItems = new();
    private List<OrderDto> _liveOrders = new();
    private List<OrderDto> _currentTableOrders = new();
    private List<TableDto> _tablesList = new();
    private readonly HashSet<int> _processingOrderIds = new();
    private DispatcherTimer? _activityBannerTimer;
    private DispatcherTimer? _autoRefreshTimer;

    // Unaccepted orders repeating alert tracking (+30s cycles with escalating volume & pitch)
    private DateTime? _unacceptedTrackingStartTime;
    private DateTime _lastUnacceptedAlertTime = DateTime.MinValue;
    private int _currentEscalationRound = 0;
    private bool _snoozeBannerUntilNextCycle = false;

    private OrderType _orderType = OrderType.DineIn;
    private int? _selectedCategoryId = null;
    private UserDto? _currentUser;

    // Selected items for management tabs
    private CategoryDto? _selectedManageCategory;
    private ProductDto? _selectedManageProduct;
    private IngredientDto? _selectedIngredient;

    public MainWindow()
    {
        InitializeComponent();

        _config = PosConfig.Load();
        _api = new ApiClient(_config.GetBaseUrl(), _config.StoreCode);
        _realtime = new RealtimeClient(_config.GetBaseUrl(), _config.StoreCode);

        GridCartItems.ItemsSource = _cartItems;

        // Clock & unaccepted orders recurring reminder timer
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) =>
        {
            TxtClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CheckUnacceptedOrdersReminder();
        };
        _clockTimer.Start();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadPrinters();
        LoadStoreSettings();
        LoadSoundSettings();
        LoadAdvancedSettings();

        // Enforce user authentication first
        if (!PromptLogin())
        {
            Application.Current.Shutdown();
            return;
        }

        // Once authenticated, sync connection and start realtime client for this store
        _config = PosConfig.Load();
        _api.UpdateConnection(_config.GetBaseUrl(), _config.StoreCode);

        if (_realtime != null)
        {
            await _realtime.DisposeAsync();
        }
        _realtime = new RealtimeClient(_config.GetBaseUrl(), _config.StoreCode);
        SetupRealtime();
        await InitializeSystemAsync();

        var startupLic = ClientLicenseService.Instance.GetCurrentLicenseStatus();
        UpdateLicenseBadge(_currentStoreInfo);
        if (startupLic.IsExpired || startupLic.IsTampered)
        {
            OpenActivateLicenseDialog();
        }
    }

    private bool PromptLogin()
    {
        _config = PosConfig.Load();
        _api.UpdateConnection(_config.GetBaseUrl(), _config.StoreCode);
        var loginWin = new LoginWindow(_api, _config) { Owner = this };
        var result = loginWin.ShowDialog();
        if (result == true && loginWin.AuthenticatedUser != null)
        {
            _currentUser = loginWin.AuthenticatedUser;
            _config = PosConfig.Load();
            _api.UpdateConnection(_config.GetBaseUrl(), _config.StoreCode);
            TxtCashier.Text = $"ผู้ใช้งาน: {_currentUser.Username} [{_currentUser.Role}]";
            PosLogger.Info($"[POS] Current user set to {_currentUser.Username} (Store: {_config.StoreCode})");
            return true;
        }
        return false;
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show("คุณต้องการออกจากระบบใช่หรือไม่?", "ยืนยันการออกจากระบบ", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _api.Logout();
            _currentUser = null;
            TxtCashier.Text = "ยังไม่ได้เข้าสู่ระบบ";
            if (!PromptLogin())
            {
                Application.Current.Shutdown();
            }
        }
    }

    private void LoadPrinters()
    {
        try
        {
            var printers = new List<string> { "(ไม่ใช้งานเครื่องพิมพ์)" };
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                printers.Add(printer);
            }

            CmbReceiptPrinter.ItemsSource = printers;
            CmbKitchenPrinter.ItemsSource = printers;

            CmbReceiptPrinter.SelectedItem = string.IsNullOrWhiteSpace(_config.ReceiptPrinterName)
                ? printers[0]
                : _config.ReceiptPrinterName;

            CmbKitchenPrinter.SelectedItem = string.IsNullOrWhiteSpace(_config.KitchenPrinterName)
                ? printers[0]
                : _config.KitchenPrinterName;

            ChkAutoReceipt.IsChecked = _config.AutoPrintReceipt;
            ChkAutoKitchen.IsChecked = _config.AutoPrintKitchenSlip;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Settings] Failed to load printers: " + ex.Message, ex);
        }
    }

    private void LoadStoreSettings()
    {
        TxtStoreName.Text = _config.StoreName;
        TxtStoreAddress.Text = _config.StoreAddress;
        TxtStorePhone.Text = _config.StorePhone;
        TxtTaxId.Text = _config.TaxId;
        TxtVatPercent.Text = _config.VatPercent.ToString("0.0");
        TxtServiceChargePercent.Text = _config.ServiceChargePercent.ToString("0.0");
        TxtReceiptHeader.Text = _config.ReceiptHeader;
        TxtReceiptFooter.Text = _config.ReceiptFooter;
    }

    private void LoadSoundSettings()
    {
        ChkSoundEnabled.IsChecked = _config.SoundEnabled;
        ChkRepeatAlert.IsChecked = _config.RepeatAlertForUnacceptedOrders;
        ChkEscalateSound.IsChecked = _config.EscalateAlertVolumeAndPitch;
        SliderSoundVolume.Value = _config.SoundVolume;
        TxtSoundVolumeLabel.Text = $"{_config.SoundVolume}%";
    }

    private void SetupRealtime()
    {
        _realtime.ConnectionStatusChanged += (connected, msg) =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtConnection.Text = msg;
                BadgeConnection.Background = connected ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                TxtStatusBar.Text = $"สถานะ: {msg} | เซิร์ฟเวอร์: {_config.GetBaseUrl()}";
            });
        };

        _realtime.OrderReceived += order =>
        {
            Dispatcher.Invoke(() =>
            {
                _unacceptedTrackingStartTime = DateTime.UtcNow;
                _lastUnacceptedAlertTime = DateTime.UtcNow;
                _currentEscalationRound = 1;
                _snoozeBannerUntilNextCycle = false;

                // Play loud audio alert if sound is enabled
                if (_config.SoundEnabled)
                {
                    AudioAlertService.PlayEscalatingOrderAlert(1, _config.SoundVolume, _config.EscalateAlertVolumeAndPitch);
                }

                // Show alert banner
                TxtAlertMessage.Text = $"[ออเดอร์ใหม่] บิล #{order.OrderNumber} ({order.TableNumber}) ยอด {order.TotalAmount:N2} บ. - กรุณากดรับออเดอร์";
                BtnAlertAcceptOrder.Tag = order.Id;
                BorderAlert.Background = new SolidColorBrush(Color.FromRgb(239, 108, 0));
                BorderAlert.Visibility = Visibility.Visible;

                // Print kitchen slip if auto-print enabled
                if (_config.AutoPrintKitchenSlip && !string.IsNullOrWhiteSpace(_config.KitchenPrinterName))
                {
                    PrintingService.PrintKitchenSlip(order, _config.KitchenPrinterName);
                }

                _ = RefreshLiveOrdersAsync();
            });
        };

        _realtime.OrderStatusUpdated += order =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = RefreshLiveOrdersAsync();
            });
        };

        _realtime.MenuUpdated += product =>
        {
            Dispatcher.Invoke(async () =>
            {
                await LoadCategoriesAndProductsAsync();
            });
        };

        _realtime.CategoryUpdated += category =>
        {
            Dispatcher.Invoke(async () =>
            {
                await LoadCategoriesAndProductsAsync();
            });
        };

        _realtime.IngredientUpdated += ingredient =>
        {
            Dispatcher.Invoke(async () =>
            {
                await LoadIngredientsAsync();
            });
        };

        _realtime.TableStatusUpdated += table =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = RefreshLiveOrdersAsync();
                _ = LoadTablesSettingsAsync();
            });
        };

        _realtime.OrderActionActivityReceived += activity =>
        {
            Dispatcher.Invoke(() =>
            {
                OnOrderActionActivityReceived(activity);
            });
        };

        _realtime.BillClosed += order =>
        {
            Dispatcher.Invoke(() =>
            {
                _ = RefreshLiveOrdersAsync();
                _ = RefreshReportsAsync();
            });
        };

        _ = _realtime.StartAsync();
    }

    private void OnOrderActionActivityReceived(OrderActionActivityDto activity)
    {
        if (_config.ShowActivityNotifications)
        {
            TxtLiveActivityMessage.Text = $"[{activity.Timestamp.ToLocalTime():HH:mm:ss}] {activity.ActionDescription}";
            BorderLiveActivity.Visibility = Visibility.Visible;

            _activityBannerTimer?.Stop();
            _activityBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _activityBannerTimer.Tick += (s, e) =>
            {
                BorderLiveActivity.Visibility = Visibility.Collapsed;
                _activityBannerTimer?.Stop();
            };
            _activityBannerTimer.Start();
        }
    }

    private void BtnHideLiveActivity_Click(object sender, RoutedEventArgs e)
    {
        _activityBannerTimer?.Stop();
        BorderLiveActivity.Visibility = Visibility.Collapsed;
    }

    private StoreInfoResponse? _currentStoreInfo;

    private async Task InitializeSystemAsync()
    {
        try
        {
            var storeInfo = await _api.GetStoreInfoAsync();
            UpdateLicenseBadge(storeInfo);
            if (storeInfo != null && !string.IsNullOrWhiteSpace(storeInfo.StoreName))
            {
                TxtStoreBadge.Text = $"[ร้าน: {storeInfo.StoreName} ({_config.StoreCode})]";
                _config.StoreName = storeInfo.StoreName;
            }
            else
            {
                TxtStoreBadge.Text = $"[ร้าน: {_config.StoreCode}]";
            }

            if (storeInfo != null && storeInfo.IsExpired)
            {
                MessageBox.Show(
                    this,
                    $"สิทธิ์การทดลองใช้งาน 14 วันของร้าน {_config.StoreCode} หมดอายุแล้ว\nกรุณากรอกรหัสเปิดใช้งาน (Activation Key) เพื่อดำเนินการขายต่อ",
                    "สิทธิ์การใช้งานหมดอายุ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch
        {
            TxtStoreBadge.Text = $"[ร้าน: {_config.StoreCode}]";
            UpdateLicenseBadge(null);
        }

        await LoadCategoriesAndProductsAsync();
        await RefreshLiveOrdersAsync();
        await LoadIngredientsAsync();
        await RefreshReportsAsync();
        await LoadTableOrdersAsync(TxtTableRef.Text.Trim());
    }

    private void UpdateLicenseBadge(StoreInfoResponse? storeInfo)
    {
        _currentStoreInfo = storeInfo;
        var clientLic = ClientLicenseService.Instance.GetCurrentLicenseStatus();

        if (clientLic.IsPermanentLifetime)
        {
            TxtLicenseBadge.Text = "[ เวอร์ชันเต็ม ตลอดชีพ ]";
            BorderLicenseBadge.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));
        }
        else if (clientLic.IsValid && !clientLic.IsTrial)
        {
            TxtLicenseBadge.Text = $"[ เวอร์ชันเต็ม เหลือ {clientLic.DaysRemaining} วัน ]";
            BorderLicenseBadge.Background = new SolidColorBrush(Color.FromRgb(2, 119, 189));
        }
        else if (clientLic.IsTampered)
        {
            TxtLicenseBadge.Text = "[ ตรวจพบการปรับเวลา (คลิกเพื่อปลดล็อก) ]";
            BorderLicenseBadge.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }
        else if (clientLic.IsExpired)
        {
            TxtLicenseBadge.Text = "[ สิทธิ์ทดลองใช้งาน 14 วันหมดอายุ (คลิกเพื่อใส่คีย์) ]";
            BorderLicenseBadge.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }
        else
        {
            TxtLicenseBadge.Text = $"[ ทดลองใช้: เหลืออีก {clientLic.DaysRemaining} วัน ]";
            BorderLicenseBadge.Background = new SolidColorBrush(Color.FromRgb(230, 81, 0));
        }
    }

    private void BorderLicenseBadge_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenActivateLicenseDialog();
    }

    private void BtnActivateLicense_Click(object sender, RoutedEventArgs e)
    {
        OpenActivateLicenseDialog();
    }

    private void OpenActivateLicenseDialog()
    {
        var dlg = new ActivateLicenseDialog(_api, _currentStoreInfo)
        {
            Owner = this
        };
        dlg.ShowDialog();
        UpdateLicenseBadge(_currentStoreInfo);
    }

    private bool CheckLicenseValidOrPrompt()
    {
        var clientLic = ClientLicenseService.Instance.GetCurrentLicenseStatus();
        if (clientLic.IsExpired || clientLic.IsTampered || !clientLic.IsValid)
        {
            var msg = clientLic.IsTampered
                ? "ตรวจพบการปรับเปลี่ยนเวลาของระบบเพื่อยืดอายุสิทธิ์\n\nกรุณากรอกรหัสเปิดใช้งาน (Activation Key) ที่ถูกต้องจากผู้พัฒนาเพื่อปลดล็อกการใช้งาน"
                : $"สิทธิ์การทดลองใช้งาน 14 วันของเครื่องนี้หมดอายุแล้ว (ครบกำหนด 14 วันจริงนับจากวันแรกที่ติดตั้ง)\n\nรหัสประจำเครื่องนี้: {clientLic.MachineCode}\n\nกรุณาส่งรหัสเครื่องด้านบนให้ผู้พัฒนาเพื่อขอรหัสเปิดใช้งาน (Activation Key) ก่อนดำเนินการขายอาหารต่อ";

            MessageBox.Show(
                this,
                msg,
                "สิทธิ์การใช้งานหมดอายุ",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            OpenActivateLicenseDialog();
            var recheck = ClientLicenseService.Instance.GetCurrentLicenseStatus();
            return recheck.IsValid && !recheck.IsExpired;
        }

        return true;
    }

    // -------------------------------------------------------------
    // NAVIGATION TABS
    // -------------------------------------------------------------
    private async void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (ViewPos == null || ViewOrders == null || ViewMenu == null || 
            ViewStock == null || ViewReports == null || ViewSettings == null) 
            return;

        ViewPos.Visibility = Visibility.Collapsed;
        ViewOrders.Visibility = Visibility.Collapsed;
        ViewMenu.Visibility = Visibility.Collapsed;
        ViewStock.Visibility = Visibility.Collapsed;
        ViewReports.Visibility = Visibility.Collapsed;
        ViewSettings.Visibility = Visibility.Collapsed;

        if (TabPos.IsChecked == true)
        {
            ViewPos.Visibility = Visibility.Visible;
        }
        else if (TabOrders.IsChecked == true)
        {
            ViewOrders.Visibility = Visibility.Visible;
            await RefreshLiveOrdersAsync();
        }
        else if (TabMenu.IsChecked == true)
        {
            ViewMenu.Visibility = Visibility.Visible;
            await LoadCategoriesAndProductsAsync();
        }
        else if (TabStock.IsChecked == true)
        {
            ViewStock.Visibility = Visibility.Visible;
            await LoadIngredientsAsync();
        }
        else if (TabReports.IsChecked == true)
        {
            ViewReports.Visibility = Visibility.Visible;
            await RefreshReportsAsync();
        }
        else if (TabSettings.IsChecked == true)
        {
            ViewSettings.Visibility = Visibility.Visible;
            _ = LoadTablesSettingsAsync();
            _ = LoadAuditLogsAsync();
            LoadAdvancedSettings();
        }
    }

    // -------------------------------------------------------------
    // TAB 1: POS SALES (No Table Locking)
    // -------------------------------------------------------------
    private void OrderType_Changed(object sender, RoutedEventArgs e)
    {
        if (TxtTableRef == null) return;

        if (RbDineIn?.IsChecked == true)
        {
            _orderType = OrderType.DineIn;
            if (string.IsNullOrWhiteSpace(TxtTableRef.Text) || TxtTableRef.Text.Contains("กลับบ้าน") || TxtTableRef.Text.Contains("เดลิเวอรี"))
                TxtTableRef.Text = "โต๊ะ 1";
        }
        else if (RbTakeAway?.IsChecked == true)
        {
            _orderType = OrderType.TakeAway;
            TxtTableRef.Text = "สั่งกลับบ้าน";
        }
        else if (RbDelivery?.IsChecked == true)
        {
            _orderType = OrderType.Delivery;
            TxtTableRef.Text = "เดลิเวอรี";
        }
    }

    private async void QuickTable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tableStr)
        {
            TxtTableRef.Text = tableStr;
            if (tableStr.Contains("กลับบ้าน"))
            {
                RbTakeAway.IsChecked = true;
            }
            else
            {
                RbDineIn.IsChecked = true;
            }
            await LoadTableOrdersAsync(tableStr);
        }
    }

    private void TxtTableRef_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _ = LoadTableOrdersAsync(TxtTableRef.Text.Trim());
    }

    private void BtnClearTableRef_Click(object sender, RoutedEventArgs e)
    {
        TxtTableRef.Text = "";
        _currentTableOrders.Clear();
        BorderTableActiveOrders.Visibility = Visibility.Collapsed;
        ListTableExistingItems.ItemsSource = null;
        BtnSendKitchen.Content = "ส่งเข้าครัว (Send)";
        BtnPay.Content = "ชำระเงินและปิดบิล (F10)";
        UpdateTableButtonsVisuals();
    }

    private async Task LoadTableOrdersAsync(string tableRef)
    {
        if (string.IsNullOrWhiteSpace(tableRef))
        {
            _currentTableOrders.Clear();
            BorderTableActiveOrders.Visibility = Visibility.Collapsed;
            ListTableExistingItems.ItemsSource = null;
            BtnSendKitchen.Content = "ส่งเข้าครัว (Send)";
            BtnPay.Content = "ชำระเงินและปิดบิล (F10)";
            UpdateTableButtonsVisuals();
            return;
        }

        try
        {
            _currentTableOrders = await _api.GetActiveOrdersByTableAsync(tableRef);
            if (_currentTableOrders.Any())
            {
                BorderTableActiveOrders.Visibility = Visibility.Visible;
                var allItems = _currentTableOrders.SelectMany(o => o.Items).ToList();
                ListTableExistingItems.ItemsSource = allItems;

                var totalSoFar = _currentTableOrders.Sum(o => o.Subtotal);
                TxtTableStatusBadge.Text = $"[มีออเดอร์ค้าง {_currentTableOrders.Count} บิล]";
                TxtTableExistingCount.Text = $"{allItems.Sum(i => i.Quantity)} รายการ";
                TxtTableExistingTotal.Text = $"{totalSoFar:N2} บ.";

                BtnSendKitchen.Content = $"สั่งอาหารเพิ่มเข้า {tableRef} (Send)";
                BtnPay.Content = _cartItems.Any()
                    ? $"ชำระเงินรวม {tableRef} (F10)"
                    : $"เช็คบิลปิด {tableRef} (F10)";
            }
            else
            {
                BorderTableActiveOrders.Visibility = Visibility.Collapsed;
                ListTableExistingItems.ItemsSource = null;
                BtnSendKitchen.Content = "ส่งเข้าครัว (Send)";
                BtnPay.Content = "ชำระเงินและปิดบิล (F10)";
            }
        }
        catch (Exception ex)
        {
            PosLogger.Warn($"[LoadTableOrders] Error loading orders for {tableRef}: {ex.Message}");
        }
        finally
        {
            UpdateTableButtonsVisuals();
        }
    }

    private void UpdateTableButtonsVisuals()
    {
        if (PanelQuickTables == null) return;
        var currentRef = TxtTableRef?.Text?.Trim() ?? "";

        foreach (var child in PanelQuickTables.Children)
        {
            if (child is Button btn && btn.Tag is string tag)
            {
                var isSelected = !string.IsNullOrEmpty(currentRef) &&
                    (string.Equals(tag, currentRef, StringComparison.OrdinalIgnoreCase) ||
                     (tag == "สั่งกลับบ้าน" && currentRef.Contains("กลับบ้าน")));

                var isOccupied = _liveOrders.Any(o =>
                    !string.IsNullOrWhiteSpace(o.TableNumber) &&
                    (string.Equals(o.TableNumber, tag, StringComparison.OrdinalIgnoreCase) ||
                     o.TableNumber.Contains(tag) || tag.Contains(o.TableNumber)));

                if (isSelected)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(29, 78, 216)); // Deep Blue
                    btn.Foreground = Brushes.White;
                    btn.BorderBrush = isOccupied
                        ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) // Gold/Amber border if occupied
                        : new SolidColorBrush(Color.FromRgb(30, 64, 175)); // Blue border
                    btn.FontWeight = FontWeights.Bold;
                }
                else if (isOccupied)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Light Amber
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));  // Dark Amber Text
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6)); // Amber Border
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Background = Brushes.White;
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                    btn.FontWeight = FontWeights.SemiBold;
                }
            }
        }
    }

    private void BtnViewTableOrdersPopup_Click(object sender, RoutedEventArgs e)
    {
        if (!_currentTableOrders.Any())
        {
            MessageBox.Show("ยังไม่มีออเดอร์ในโต๊ะนี้", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_currentTableOrders.Count == 1)
        {
            OpenOrderDetails(_currentTableOrders[0].Id);
        }
        else
        {
            var tableRef = TxtTableRef.Text.Trim();
            var consolidated = new OrderDto
            {
                Id = _currentTableOrders.First().Id,
                OrderNumber = string.Join(", ", _currentTableOrders.Select(o => o.OrderNumber)),
                TableNumber = tableRef,
                Type = _orderType,
                Status = OrderStatus.Preparing,
                Items = _currentTableOrders.SelectMany(o => o.Items).ToList(),
                Subtotal = _currentTableOrders.Sum(o => o.Subtotal),
                TotalAmount = _currentTableOrders.Sum(o => o.TotalAmount),
                CreatedAt = _currentTableOrders.First().CreatedAt,
                CustomerName = _currentTableOrders.FirstOrDefault(o => !string.IsNullOrEmpty(o.CustomerName))?.CustomerName,
                CustomerPhone = _currentTableOrders.FirstOrDefault(o => !string.IsNullOrEmpty(o.CustomerPhone))?.CustomerPhone,
                Notes = string.Join("; ", _currentTableOrders.Where(o => !string.IsNullOrWhiteSpace(o.Notes)).Select(o => o.Notes))
            };
            var dlg = new OrderDetailsDialog(consolidated, _api, _config) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _ = RefreshLiveOrdersAsync();
            }
        }
    }

    private void BtnOpenTableQr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new TableQrDialog(_api, _config) { Owner = this };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[MainWindow] Failed to open TableQrDialog: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเปิดหน้าต่างจัดการ QR Code โต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private async void BtnPayTableDirect_Click(object sender, RoutedEventArgs e)
    {
        await PayTableDirectAsync();
    }

    private async Task PayTableDirectAsync()
    {
        var tableRef = string.IsNullOrWhiteSpace(TxtTableRef.Text) ? "ทั่วไป" : TxtTableRef.Text.Trim();
        if (!_currentTableOrders.Any())
        {
            _currentTableOrders = await _api.GetActiveOrdersByTableAsync(tableRef);
        }

        if (!_currentTableOrders.Any())
        {
            MessageBox.Show($"ไม่พบออเดอร์ค้างชำระสำหรับ {tableRef}", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var allItems = _currentTableOrders.SelectMany(o => o.Items).ToList();
            var subtotal = _currentTableOrders.Sum(o => o.Subtotal);
            var orderNumbers = string.Join(", ", _currentTableOrders.Select(o => o.OrderNumber));

            var consolidated = new OrderDto
            {
                Id = _currentTableOrders.First().Id,
                OrderNumber = orderNumbers,
                Type = _orderType,
                TableNumber = tableRef,
                Status = OrderStatus.Ready,
                Items = allItems,
                Subtotal = subtotal,
                TotalAmount = subtotal,
                CustomerName = _currentTableOrders.FirstOrDefault(o => !string.IsNullOrEmpty(o.CustomerName))?.CustomerName,
                CustomerPhone = _currentTableOrders.FirstOrDefault(o => !string.IsNullOrEmpty(o.CustomerPhone))?.CustomerPhone,
                Notes = string.Join("; ", _currentTableOrders.Where(o => !string.IsNullOrWhiteSpace(o.Notes)).Select(o => o.Notes))
            };

            var payWin = new PaymentDialog(consolidated) { Owner = this };
            var result = payWin.ShowDialog();

            if (result == true && payWin.Result != null)
            {
                await _api.PayTableOrdersAsync(tableRef, new PaymentRequest
                {
                    PaymentMethod = payWin.Result.Method,
                    PaidAmount = payWin.Result.PaidAmount,
                    DiscountAmount = payWin.Result.DiscountAmount,
                    CashierName = _config.StationName
                });

                AudioAlertService.PlaySuccessChime(_config.SoundVolume);

                _cartItems.Clear();
                RecalculateCart();
                _currentTableOrders.Clear();
                BorderTableActiveOrders.Visibility = Visibility.Collapsed;
                ListTableExistingItems.ItemsSource = null;

                await RefreshLiveOrdersAsync();
                await RefreshReportsAsync();
                UpdateTableButtonsVisuals();

                MessageBox.Show($"ชำระเงินและปิดบิล {tableRef} สำเร็จเรียบร้อยแล้ว\nยอดชำระ: {payWin.Result.TotalAmount:N2} บ. (เงินทอน {payWin.Result.ChangeAmount:N2} บ.)",
                    "ชำระเงินสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[PayTable] Failed to checkout table orders: " + ex.Message, ex);
            ErrorDialog.Show("ไม่สามารถชำระเงินและปิดบิลโต๊ะได้: " + ex.Message, "ข้อผิดพลาดการชำระเงิน", ex.ToString(), this);
        }
    }

    private async Task LoadCategoriesAndProductsAsync()
    {
        try
        {
            _categories = await _api.GetCategoriesAsync();
            _products = await _api.GetProductsAsync();

            var baseUrl = _config.GetBaseUrl().TrimEnd('/');
            // Game-client asset download & local caching
            await AssetSyncService.SyncProductImagesAsync(baseUrl, _config.StoreCode, _products);

            // Build Category Filter Pills in POS
            PanelCategoryTabs.Children.Clear();

            var btnAll = new Button
            {
                Content = "ทั้งหมด",
                Height = 38,
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Background = _selectedCategoryId == null ? new SolidColorBrush(Color.FromRgb(29, 78, 216)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Foreground = _selectedCategoryId == null ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            };
            btnAll.Click += (s, e) => FilterProducts(null);
            PanelCategoryTabs.Children.Add(btnAll);

            foreach (var cat in _categories)
            {
                var isSelected = _selectedCategoryId == cat.Id;
                var btnCat = new Button
                {
                    Content = cat.Name,
                    Height = 38,
                    Padding = new Thickness(16, 4, 16, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = isSelected ? new SolidColorBrush(Color.FromRgb(29, 78, 216)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                    FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    Tag = cat.Id
                };
                btnCat.Click += (s, e) => FilterProducts((int)((Button)s).Tag);
                PanelCategoryTabs.Children.Add(btnCat);
            }

            FilterProducts(_selectedCategoryId);

            // Also refresh manage menu tab
            GridCategories.ItemsSource = null;
            GridCategories.ItemsSource = _categories;

            GridManageProducts.ItemsSource = null;
            GridManageProducts.ItemsSource = _products;

            CmbProductCategory.ItemsSource = _categories;
            CmbProductCategory.DisplayMemberPath = "Name";
            CmbProductCategory.SelectedValuePath = "Id";
            if (_categories.Any()) CmbProductCategory.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Menu] Failed to load categories/products: " + ex.Message, ex);
        }
    }

    private void FilterProducts(int? categoryId)
    {
        _selectedCategoryId = categoryId;
        var search = TxtSearchProduct?.Text?.Trim().ToLowerInvariant() ?? "";

        var filtered = _products.Where(p =>
        {
            var matchCategory = !categoryId.HasValue || p.CategoryId == categoryId.Value;
            var matchSearch = string.IsNullOrEmpty(search) || 
                              p.Name.ToLowerInvariant().Contains(search) || 
                              p.Code.ToLowerInvariant().Contains(search);
            return matchCategory && matchSearch;
        }).ToList();

        ListProducts.ItemsSource = filtered;
    }

    private void TxtSearchProduct_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterProducts(_selectedCategoryId);
    }

    private void ProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            var product = _products.FirstOrDefault(p => p.Id == productId);
            if (product == null) return;

            if (!product.IsAvailable)
            {
                var reason = string.IsNullOrEmpty(product.OutOfStockReason) ? "" : $"\nสาเหตุ: {product.OutOfStockReason}";
                MessageBox.Show($"เมนู '{product.Name}' สินค้าหมด ไม่สามารถสั่งได้{reason}", "สินค้าหมด", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = _cartItems.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                _cartItems.Add(new OrderItemDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = 1,
                    KitchenStation = product.KitchenStation
                });
            }

            RecalculateCart();
        }
    }

    private void BtnQtyMinus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    _cartItems.Remove(item);
                }
                RecalculateCart();
            }
        }
    }

    private void BtnQtyPlus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                item.Quantity++;
                RecalculateCart();
            }
        }
    }

    private void BtnRemoveCartItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int productId)
        {
            var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                _cartItems.Remove(item);
                RecalculateCart();
            }
        }
    }

    private void BtnClearCart_Click(object sender, RoutedEventArgs e)
    {
        _cartItems.Clear();
        RecalculateCart();
    }

    private void RecalculateCart()
    {
        GridCartItems.Items.Refresh();
        var subtotal = _cartItems.Sum(i => i.Subtotal);
        TxtCartSubtotal.Text = $"{subtotal:N2} บาท";
        TxtCartDiscount.Text = "0.00 บาท";
        TxtCartTotal.Text = $"{subtotal:N2} บาท";
    }

    // Send order to kitchen (No Table Locking)
    private async void BtnSendKitchen_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckLicenseValidOrPrompt()) return;

        if (!_cartItems.Any())
        {
            MessageBox.Show("ยังไม่มีรายการอาหารในบิล กรุณาเลือกเมนูก่อน", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            BtnSendKitchen.IsEnabled = false;

            var tableRef = string.IsNullOrWhiteSpace(TxtTableRef.Text) 
                ? (_orderType == OrderType.DineIn ? "ทานที่ร้าน" : "กลับบ้าน") 
                : TxtTableRef.Text.Trim();

            var req = new CreateOrderRequest
            {
                Type = _orderType,
                TableNumber = tableRef,
                ClientRequestId = Guid.NewGuid().ToString(),
                Items = _cartItems.Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    SpecialNotes = i.SpecialNotes
                }).ToList()
            };

            var order = await _api.CreateOrderAsync(req);
            PosLogger.Info($"[POS] Order {order.OrderNumber} sent to kitchen for Reference: {order.TableNumber}");

            // Print kitchen ticket
            if (_config.AutoPrintKitchenSlip && !string.IsNullOrWhiteSpace(_config.KitchenPrinterName))
            {
                PrintingService.PrintKitchenSlip(order, _config.KitchenPrinterName);
            }

            AudioAlertService.PlaySuccessChime(_config.SoundVolume);

            MessageBox.Show($"ส่งออเดอร์เข้าครัวเรียบร้อยแล้ว\nเลขที่บิล: {order.OrderNumber}\nอ้างอิง: {order.TableNumber}", 
                "ส่งครัวสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);

            // Clear cart immediately so cashier can take next order without table locking
            _cartItems.Clear();
            RecalculateCart();

            await RefreshLiveOrdersAsync();
            await LoadTableOrdersAsync(tableRef);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[SendKitchen] Failed to send order: " + ex.Message, ex);
            ErrorDialog.Show("ไม่สามารถส่งออเดอร์เข้าครัวได้: " + ex.Message, "ข้อผิดพลาดส่งครัว", ex.ToString(), this);
        }
        finally
        {
            BtnSendKitchen.IsEnabled = true;
        }
    }

    // Checkout & Payment (F10)
    private async void BtnPay_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckLicenseValidOrPrompt()) return;

        var tableRef = string.IsNullOrWhiteSpace(TxtTableRef.Text) 
            ? (_orderType == OrderType.DineIn ? "โต๊ะ 1" : "ทั่วไป") 
            : TxtTableRef.Text.Trim();

        // 1. If cart is empty, check if current table has active orders to checkout
        if (!_cartItems.Any())
        {
            if (_currentTableOrders.Any())
            {
                await PayTableDirectAsync();
                return;
            }
            MessageBox.Show("ยังไม่มีรายการในบิล ไม่สามารถชำระเงินได้", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 2. If cart has items AND table already has active orders:
        // Automatically submit the cart items to kitchen/order first, then pay table altogether
        if (_currentTableOrders.Any())
        {
            try
            {
                var reqAdd = new CreateOrderRequest
                {
                    Type = _orderType,
                    TableNumber = tableRef,
                    ClientRequestId = Guid.NewGuid().ToString(),
                    Items = _cartItems.Select(i => new CreateOrderItemRequest
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        SpecialNotes = i.SpecialNotes
                    }).ToList()
                };

                var newOrder = await _api.CreateOrderAsync(reqAdd);
                if (_config.AutoPrintKitchenSlip && !string.IsNullOrWhiteSpace(_config.KitchenPrinterName))
                {
                    PrintingService.PrintKitchenSlip(newOrder, _config.KitchenPrinterName);
                }

                _cartItems.Clear();
                RecalculateCart();

                // Refresh table orders to include newly submitted order
                _currentTableOrders = await _api.GetActiveOrdersByTableAsync(tableRef);
                await PayTableDirectAsync();
                return;
            }
            catch (Exception ex)
            {
                PosLogger.Error("[PayTable] Failed during add and pay: " + ex.Message, ex);
                ErrorDialog.Show("ไม่สามารถส่งรายการและปิดบิลได้: " + ex.Message, "ข้อผิดพลาดการชำระเงิน", ex.ToString(), this);
                return;
            }
        }

        // 3. Table has no prior orders, single new order checkout
        try
        {
            var req = new CreateOrderRequest
            {
                Type = _orderType,
                TableNumber = tableRef,
                ClientRequestId = Guid.NewGuid().ToString(),
                Items = _cartItems.Select(i => new CreateOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    SpecialNotes = i.SpecialNotes
                }).ToList()
            };

            var order = await _api.CreateOrderAsync(req);

            var payWin = new PaymentDialog(order) { Owner = this };
            var result = payWin.ShowDialog();

            if (result == true && payWin.Result != null)
            {
                await _api.PayOrderAsync(order.Id, new PaymentRequest
                {
                    PaymentMethod = payWin.Result.Method,
                    PaidAmount = payWin.Result.PaidAmount,
                    DiscountAmount = payWin.Result.DiscountAmount,
                    CashierName = _config.StationName
                });

                AudioAlertService.PlaySuccessChime(_config.SoundVolume);

                _cartItems.Clear();
                RecalculateCart();
                TxtTableRef.Text = "โต๊ะ 1";

                await RefreshLiveOrdersAsync();
                await RefreshReportsAsync();
                await LoadTableOrdersAsync(TxtTableRef.Text.Trim());
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Payment] Failed to process checkout: " + ex.Message, ex);
            ErrorDialog.Show("ไม่สามารถดำเนินการชำระเงินได้: " + ex.Message, "ข้อผิดพลาดการชำระเงิน", ex.ToString(), this);
        }
    }

    // -------------------------------------------------------------
    // TAB 2: LIVE ORDERS (KDS)
    // -------------------------------------------------------------
    private async Task RefreshLiveOrdersAsync()
    {
        try
        {
            _liveOrders = await _api.GetActiveOrdersAsync();
            ApplyLiveOrdersFilter();
            CheckUnacceptedOrdersReminder();

            var tableRef = TxtTableRef?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(tableRef))
            {
                _ = LoadTableOrdersAsync(tableRef);
            }
            else
            {
                UpdateTableButtonsVisuals();
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Orders] Failed to refresh live orders: " + ex.Message, ex);
        }
    }

    private void ApplyLiveOrdersFilter()
    {
        if (_liveOrders == null) return;

        List<OrderDto> filtered;
        if (RbFilterNew?.IsChecked == true)
        {
            filtered = _liveOrders.Where(o => o.Status == OrderStatus.New).ToList();
        }
        else if (RbFilterCooking?.IsChecked == true)
        {
            filtered = _liveOrders.Where(o => o.Status == OrderStatus.Accepted || o.Status == OrderStatus.Preparing).ToList();
        }
        else if (RbFilterReady?.IsChecked == true)
        {
            filtered = _liveOrders.Where(o => o.Status == OrderStatus.Ready).ToList();
        }
        else if (RbFilterCompleted?.IsChecked == true)
        {
            filtered = _liveOrders.Where(o => o.Status == OrderStatus.Completed).ToList();
        }
        else
        {
            filtered = _liveOrders;
        }

        GridLiveOrders.ItemsSource = null;
        GridLiveOrders.ItemsSource = filtered;
    }

    private void FilterOrders_Changed(object sender, RoutedEventArgs e)
    {
        ApplyLiveOrdersFilter();
    }

    private async Task ExecuteOrderStatusUpdateAsync(Button btn, int orderId, OrderStatus targetStatus, string successMessage)
    {
        if (_processingOrderIds.Contains(orderId)) return;

        if (_config.ConfirmStatusChange)
        {
            var confirm = MessageBox.Show($"ต้องการเปลี่ยนสถานะออเดอร์เป็น {targetStatus} ใช่หรือไม่?", 
                "ยืนยันการเปลี่ยนสถานะ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        _processingOrderIds.Add(orderId);
        var originalContent = btn.Content;
        btn.Content = "[ กำลังบันทึก... ]";
        btn.IsEnabled = false;
        btn.Opacity = 0.55;

        try
        {
            var operatorName = _currentUser?.FullName ?? _currentUser?.Username ?? "แคชเชียร์ POS";
            var updatedOrder = await _api.UpdateOrderStatusAsync(orderId, targetStatus, operatorName, "POS");

            // Auto print kitchen slip if status is Preparing and config is enabled
            if (targetStatus == OrderStatus.Preparing && _config.AutoPrintKitchenOnPreparing && !string.IsNullOrWhiteSpace(_config.KitchenPrinterName))
            {
                var ord = _liveOrders.FirstOrDefault(o => o.Id == orderId) ?? updatedOrder;
                if (ord != null)
                {
                    PrintingService.PrintKitchenSlip(ord, _config.KitchenPrinterName);
                }
            }

            await RefreshLiveOrdersAsync();
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[Orders] Failed to update order {orderId} to {targetStatus}: {ex.Message}", ex);
            MessageBox.Show("ไม่สามารถปรับปรุงสถานะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            btn.Content = originalContent;
            btn.IsEnabled = true;
            btn.Opacity = 1.0;
        }
        finally
        {
            _processingOrderIds.Remove(orderId);
        }
    }

    private async void BtnRefreshOrders_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLiveOrdersAsync();
    }

    private async void BtnOrderAccept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            await ExecuteOrderStatusUpdateAsync(btn, orderId, OrderStatus.Accepted, "รับออเดอร์แล้ว");
        }
    }

    private async void BtnOrderPrepare_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            await ExecuteOrderStatusUpdateAsync(btn, orderId, OrderStatus.Preparing, "เริ่มปรุงแล้ว");
        }
    }

    private async void BtnOrderReady_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            await ExecuteOrderStatusUpdateAsync(btn, orderId, OrderStatus.Ready, "ปรุงเสร็จแล้ว");
        }
    }

    private async void BtnOrderComplete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            await ExecuteOrderStatusUpdateAsync(btn, orderId, OrderStatus.Completed, "เสิร์ฟแล้ว");
        }
    }

    private async void BtnOrderCancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            var confirm = MessageBox.Show($"ต้องการยกเลิกคำสั่งซื้อนี้ใช่หรือไม่?", "ยืนยันการยกเลิก", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                await ExecuteOrderStatusUpdateAsync(btn, orderId, OrderStatus.Cancelled, "ยกเลิกคำสั่งซื้อแล้ว");
            }
        }
    }

    private void BtnOrderDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            OpenOrderDetails(orderId);
        }
    }

    private void GridLiveOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridLiveOrders.SelectedItem is OrderDto order)
        {
            OpenOrderDetails(order.Id);
        }
    }

    private void OpenOrderDetails(int orderId)
    {
        var order = _liveOrders.FirstOrDefault(o => o.Id == orderId);
        if (order == null) return;

        var dlg = new OrderDetailsDialog(order, _api, _config, _realtime) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _ = RefreshLiveOrdersAsync();
        }
    }

    private void BtnOrderPrintKitchen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int orderId)
        {
            var order = _liveOrders.FirstOrDefault(o => o.Id == orderId);
            if (order != null && !string.IsNullOrWhiteSpace(_config.KitchenPrinterName))
            {
                PrintingService.PrintKitchenSlip(order, _config.KitchenPrinterName);
            }
        }
    }

    private async void BtnAlertAcceptOrder_Click(object sender, RoutedEventArgs e)
    {
        if (BtnAlertAcceptOrder.Tag is int orderId && orderId > 0)
        {
            BtnAlertAcceptOrder.IsEnabled = false;
            BtnAlertAcceptOrder.Content = "[ กำลังรับออเดอร์... ]";
            try
            {
                var operatorName = _currentUser?.FullName ?? _currentUser?.Username ?? "แคชเชียร์ POS";
                await _api.UpdateOrderStatusAsync(orderId, OrderStatus.Accepted, operatorName, "POS");
                await RefreshLiveOrdersAsync();
            }
            catch (Exception ex)
            {
                PosLogger.Error("[Alert] Failed to accept order from banner: " + ex.Message, ex);
                MessageBox.Show("ไม่สามารถรับออเดอร์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnAlertAcceptOrder.IsEnabled = true;
                BtnAlertAcceptOrder.Content = "[ รับออเดอร์ทันที ]";
            }
        }
        else
        {
            TabOrders.IsChecked = true;
        }
    }

    private void BtnAlertViewOrders_Click(object sender, RoutedEventArgs e)
    {
        BorderAlert.Visibility = Visibility.Collapsed;
        TabOrders.IsChecked = true;
    }

    private void BtnAlertDismiss_Click(object sender, RoutedEventArgs e)
    {
        BorderAlert.Visibility = Visibility.Collapsed;
        _snoozeBannerUntilNextCycle = true;
    }

    // -------------------------------------------------------------
    // UNACCEPTED ORDERS REPEATING ALERT & ESCALATION SYSTEM
    // -------------------------------------------------------------
    private void CheckUnacceptedOrdersReminder()
    {
        if (_liveOrders == null || _liveOrders.Count == 0)
        {
            ResetUnacceptedOrderTracking();
            return;
        }

        var unacceptedOrders = _liveOrders.Where(o => o.Status == OrderStatus.New).ToList();
        if (unacceptedOrders.Count == 0)
        {
            ResetUnacceptedOrderTracking();
            return;
        }

        var oldestOrder = unacceptedOrders.OrderBy(o => o.CreatedAt).FirstOrDefault() ?? unacceptedOrders[0];
        var now = DateTime.UtcNow;

        if (_unacceptedTrackingStartTime == null)
        {
            _unacceptedTrackingStartTime = oldestOrder.CreatedAt > DateTime.MinValue && oldestOrder.CreatedAt <= now
                ? oldestOrder.CreatedAt
                : now;
            _lastUnacceptedAlertTime = now;
            _currentEscalationRound = 1;
            _snoozeBannerUntilNextCycle = false;
            UpdateUnacceptedOrderBanner(unacceptedOrders, oldestOrder, 1, 0);
            return;
        }

        var totalWaitingSec = (int)Math.Max(0, (now - _unacceptedTrackingStartTime.Value).TotalSeconds);
        var intervalSec = _config.UnacceptedAlertIntervalSeconds > 0 ? _config.UnacceptedAlertIntervalSeconds : 30;
        var secondsSinceLastAlert = (now - _lastUnacceptedAlertTime).TotalSeconds;

        if (secondsSinceLastAlert >= intervalSec)
        {
            _currentEscalationRound++;
            _lastUnacceptedAlertTime = now;
            _snoozeBannerUntilNextCycle = false; // Reset snooze on alert cycle

            if (_config.SoundEnabled && _config.RepeatAlertForUnacceptedOrders)
            {
                AudioAlertService.PlayEscalatingOrderAlert(
                    _currentEscalationRound,
                    _config.SoundVolume,
                    _config.EscalateAlertVolumeAndPitch);
            }

            UpdateUnacceptedOrderBanner(unacceptedOrders, oldestOrder, _currentEscalationRound, totalWaitingSec);
        }
        else if (!_snoozeBannerUntilNextCycle)
        {
            UpdateUnacceptedOrderBanner(unacceptedOrders, oldestOrder, _currentEscalationRound, totalWaitingSec);
        }
    }

    private void ResetUnacceptedOrderTracking()
    {
        if (_unacceptedTrackingStartTime != null || _currentEscalationRound > 0)
        {
            _unacceptedTrackingStartTime = null;
            _lastUnacceptedAlertTime = DateTime.MinValue;
            _currentEscalationRound = 0;
            _snoozeBannerUntilNextCycle = false;

            if (BorderAlert.Visibility == Visibility.Visible)
            {
                BorderAlert.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateUnacceptedOrderBanner(List<OrderDto> unacceptedOrders, OrderDto oldestOrder, int round, int waitingSec)
    {
        if (_snoozeBannerUntilNextCycle) return;

        BorderAlert.Visibility = Visibility.Visible;
        BtnAlertAcceptOrder.Tag = oldestOrder.Id;
        BtnAlertAcceptOrder.Visibility = Visibility.Visible;

        var countText = unacceptedOrders.Count > 1 ? $" (รวม {unacceptedOrders.Count} ออเดอร์รอรับ)" : "";
        var tableDisplay = string.IsNullOrWhiteSpace(oldestOrder.TableNumber) ? "กลับบ้าน/ออนไลน์" : $"โต๊ะ {oldestOrder.TableNumber}";

        if (round <= 1)
        {
            BorderAlert.Background = new SolidColorBrush(Color.FromRgb(239, 108, 0)); // #EF6C00
            TxtAlertMessage.Text = $"[ออเดอร์ใหม่] บิล #{oldestOrder.OrderNumber} ({tableDisplay}) ยอด {oldestOrder.TotalAmount:N2} บ.{countText} - กรุณากดรับออเดอร์";
        }
        else if (round == 2)
        {
            BorderAlert.Background = new SolidColorBrush(Color.FromRgb(230, 81, 0)); // #E65100
            TxtAlertMessage.Text = $"[เตือนซ้ำรอบที่ 1 (+30 วิ | รอ {waitingSec} วิ)] บิล #{oldestOrder.OrderNumber} ({tableDisplay}){countText} ยังไม่ได้รับออเดอร์!";
        }
        else if (round == 3)
        {
            BorderAlert.Background = new SolidColorBrush(Color.FromRgb(216, 67, 21)); // #D84315
            TxtAlertMessage.Text = $"[เตือนซ้ำรอบที่ 2 (+60 วิ | รอ {waitingSec} วิ)] บิล #{oldestOrder.OrderNumber} ({tableDisplay}){countText} กรุณารับออเดอร์ทันที!";
        }
        else
        {
            BorderAlert.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // #C62828
            TxtAlertMessage.Text = $"[เตือนด่วนระดับสูงสุด! (+90 วิ | รอ {waitingSec} วิ)] บิล #{oldestOrder.OrderNumber} ({tableDisplay}){countText} รอนานเกินกำหนด!";
        }
    }

    // -------------------------------------------------------------
    // TAB 3: MENU & CATEGORY MANAGEMENT
    // -------------------------------------------------------------
    private void GridCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridCategories.SelectedItem is CategoryDto cat)
        {
            _selectedManageCategory = cat;
            TxtCategoryName.Text = cat.Name;
            TxtCategoryDesc.Text = cat.Description ?? "";
        }
    }

    private void BtnNewCategory_Click(object sender, RoutedEventArgs e)
    {
        _selectedManageCategory = null;
        GridCategories.SelectedItem = null;
        TxtCategoryName.Text = "";
        TxtCategoryDesc.Text = "";
        TxtCategoryName.Focus();
    }

    private async void BtnSaveCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtCategoryName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("กรุณากรอกชื่อหมวดหมู่", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var req = new CategoryCreateOrUpdateRequest
            {
                Name = name,
                Description = TxtCategoryDesc.Text.Trim(),
                SortOrder = _categories.Count + 1
            };

            if (_selectedManageCategory != null)
            {
                await _api.UpdateCategoryAsync(_selectedManageCategory.Id, req);
                MessageBox.Show("แก้ไขหมวดหมู่อาหารเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _api.CreateCategoryAsync(req);
                MessageBox.Show("เพิ่มหมวดหมู่อาหารใหม่เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LoadCategoriesAndProductsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เกิดข้อผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedManageCategory == null)
        {
            MessageBox.Show("กรุณาเลือกหมวดหมู่ที่ต้องการลบ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"ต้องการลบหมวดหมู่ '{_selectedManageCategory.Name}' หรือไม่?", "ยืนยันการลบ", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            await _api.DeleteCategoryAsync(_selectedManageCategory.Id);
            MessageBox.Show("ลบหมวดหมู่เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            _selectedManageCategory = null;
            TxtCategoryName.Text = "";
            TxtCategoryDesc.Text = "";
            await LoadCategoriesAndProductsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ไม่สามารถลบหมวดหมู่ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GridManageProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridManageProducts.SelectedItem is ProductDto prod)
        {
            _selectedManageProduct = prod;
            TxtProductCode.Text = prod.Code;
            TxtProductName.Text = prod.Name;
            TxtProductPrice.Text = prod.Price.ToString("0.00");
            CmbProductCategory.SelectedValue = prod.CategoryId;
            ChkProductAvailable.IsChecked = prod.IsAvailable;
            TxtProductOutOfStockReason.Text = prod.OutOfStockReason ?? "";

            // Station select
            foreach (ComboBoxItem item in CmbProductStation.Items)
            {
                if (item.Content.ToString() == prod.KitchenStation)
                {
                    CmbProductStation.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void BtnNewProduct_Click(object sender, RoutedEventArgs e)
    {
        _selectedManageProduct = null;
        GridManageProducts.SelectedItem = null;
        TxtProductCode.Text = $"M{_products.Count + 1:D2}";
        TxtProductName.Text = "";
        TxtProductPrice.Text = "80.00";
        ChkProductAvailable.IsChecked = true;
        TxtProductOutOfStockReason.Text = "";
        TxtProductName.Focus();
    }

    private async void BtnSaveProduct_Click(object sender, RoutedEventArgs e)
    {
        var code = TxtProductCode.Text.Trim();
        var name = TxtProductName.Text.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("กรุณากรอกรหัสและชื่อเมนูอาหาร", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtProductPrice.Text.Trim(), out var price) || price < 0)
        {
            MessageBox.Show("กรุณากรอกราคาที่ถูกต้อง", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var catId = (int?)CmbProductCategory.SelectedValue ?? (_categories.FirstOrDefault()?.Id ?? 1);
        var station = (CmbProductStation.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "MainKitchen";
        var isAvailable = ChkProductAvailable.IsChecked == true;
        var outReason = isAvailable ? null : TxtProductOutOfStockReason.Text.Trim();

        try
        {
            var dto = new ProductDto
            {
                Code = code,
                Name = name,
                Price = price,
                CategoryId = catId,
                KitchenStation = station,
                IsAvailable = isAvailable,
                OutOfStockReason = outReason
            };

            if (_selectedManageProduct != null)
            {
                dto.Id = _selectedManageProduct.Id;
                await _api.UpdateProductAsync(_selectedManageProduct.Id, dto);
                MessageBox.Show("บันทึกการแก้ไขเมนูอาหารเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _api.CreateProductAsync(dto);
                MessageBox.Show("เพิ่มเมนูอาหารใหม่เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LoadCategoriesAndProductsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เกิดข้อผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnToggleProductStock_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedManageProduct == null)
        {
            MessageBox.Show("กรุณาเลือกเมนูที่ต้องการเปลี่ยนสถานะ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var newStatus = !_selectedManageProduct.IsAvailable;
            _selectedManageProduct.IsAvailable = newStatus;
            if (newStatus)
            {
                _selectedManageProduct.OutOfStockReason = null;
            }
            else if (string.IsNullOrWhiteSpace(_selectedManageProduct.OutOfStockReason))
            {
                _selectedManageProduct.OutOfStockReason = "วัตถุดิบหมดชั่วคราว";
            }

            await _api.UpdateProductAsync(_selectedManageProduct.Id, _selectedManageProduct);
            MessageBox.Show($"เปลี่ยนสถานะเมนู '{_selectedManageProduct.Name}' เป็น: {(newStatus ? "พร้อมขาย" : "หมด")}", 
                "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadCategoriesAndProductsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เกิดข้อผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedManageProduct == null)
        {
            MessageBox.Show("กรุณาเลือกเมนูที่ต้องการลบ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"ต้องการลบเมนู '{_selectedManageProduct.Name}' หรือไม่?", "ยืนยันการลบ", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            await _api.DeleteProductAsync(_selectedManageProduct.Id);
            MessageBox.Show("ลบเมนูอาหารเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            _selectedManageProduct = null;
            await LoadCategoriesAndProductsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ไม่สามารถลบเมนูอาหารได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -------------------------------------------------------------
    // TAB 4: RAW MATERIALS & INGREDIENT STOCK (Matching Web App)
    // -------------------------------------------------------------
    private async Task LoadIngredientsAsync()
    {
        try
        {
            _ingredients = await _api.GetIngredientsAsync();
            FilterIngredients();
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Stock] Failed to load ingredients: " + ex.Message, ex);
        }
    }

    private void FilterIngredients()
    {
        var search = TxtSearchIngredient?.Text?.Trim().ToLowerInvariant() ?? "";
        var filtered = _ingredients.Where(i => 
            string.IsNullOrEmpty(search) || 
            i.Name.ToLowerInvariant().Contains(search) || 
            i.Code.ToLowerInvariant().Contains(search) ||
            i.Category.ToLowerInvariant().Contains(search)
        ).ToList();

        GridStock.ItemsSource = null;
        GridStock.ItemsSource = filtered;
    }

    private void TxtSearchIngredient_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterIngredients();
    }

    private async void BtnRefreshStock_Click(object sender, RoutedEventArgs e)
    {
        await LoadIngredientsAsync();
    }

    private void GridStock_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridStock.SelectedItem is IngredientDto ing)
        {
            _selectedIngredient = ing;
            TxtSelectedStockProduct.Text = $"{ing.Name} (รหัส: {ing.Code}, คงเหลือ: {ing.Quantity:N2} {ing.Unit})";

            // Fill edit fields
            TxtIngCode.Text = ing.Code;
            TxtIngName.Text = ing.Name;
            TxtIngUnit.Text = ing.Unit;
            TxtIngQty.Text = ing.Quantity.ToString("0.##");
            TxtIngMinAlert.Text = ing.MinQuantityAlert.ToString("0.##");
            TxtIngCost.Text = ing.CostPrice.ToString("0.##");

            foreach (ComboBoxItem item in CmbIngCategory.Items)
            {
                if (item.Content.ToString() == ing.Category)
                {
                    CmbIngCategory.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private async void BtnSaveStockAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIngredient == null)
        {
            MessageBox.Show("กรุณาเลือกวัตถุดิบในตารางก่อนทำการปรับสต๊อก", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtStockChangeQty.Text.Trim(), out var qty) || qty <= 0)
        {
            MessageBox.Show("กรุณาระบุจำนวนที่ถูกต้องและมากกว่า 0", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var adjustType = (CmbStockAdjustType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "in";
        decimal finalChange = qty;

        if (adjustType == "out" || adjustType == "scrap")
        {
            finalChange = -qty;
        }
        else if (adjustType == "set")
        {
            finalChange = qty - _selectedIngredient.Quantity;
        }

        var reason = TxtStockReason.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "ปรับปรุงสต๊อกวัตถุดิบ";
        }

        try
        {
            await _api.AdjustIngredientStockAsync(_selectedIngredient.Id, finalChange, reason, _currentUser?.Username);
            MessageBox.Show($"ปรับสต๊อกวัตถุดิบ '{_selectedIngredient.Name}' เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadIngredientsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ไม่สามารถปรับสต๊อกได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnSaveIngredient_Click(object sender, RoutedEventArgs e)
    {
        var code = TxtIngCode.Text.Trim();
        var name = TxtIngName.Text.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("กรุณากรอกรหัสและชื่อวัตถุดิบ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtIngQty.Text.Trim(), out var qty);
        decimal.TryParse(TxtIngMinAlert.Text.Trim(), out var minAlert);
        decimal.TryParse(TxtIngCost.Text.Trim(), out var cost);
        var unit = TxtIngUnit.Text.Trim();
        var category = (CmbIngCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "ของสดและเบ็ดเตล็ด";

        try
        {
            var dto = new IngredientDto
            {
                Code = code.ToUpperInvariant(),
                Name = name,
                Category = category,
                Unit = string.IsNullOrWhiteSpace(unit) ? "กก." : unit,
                Quantity = qty,
                MinQuantityAlert = minAlert > 0 ? minAlert : 5,
                CostPrice = cost
            };

            if (_selectedIngredient != null && _selectedIngredient.Code == dto.Code)
            {
                dto.Id = _selectedIngredient.Id;
                await _api.UpdateIngredientAsync(_selectedIngredient.Id, dto);
                MessageBox.Show("แก้ไขข้อมูลวัตถุดิบเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _api.CreateIngredientAsync(dto);
                MessageBox.Show("เพิ่มวัตถุดิบใหม่เข้าสู่ระบบสำเร็จ", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LoadIngredientsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("เกิดข้อผิดพลาด: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIngredient == null)
        {
            MessageBox.Show("กรุณาเลือกวัตถุดิบที่ต้องการลบ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"ต้องการลบวัตถุดิบ '{_selectedIngredient.Name}' หรือไม่?", "ยืนยันการลบ", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            await _api.DeleteIngredientAsync(_selectedIngredient.Id);
            MessageBox.Show("ลบวัตถุดิบเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            _selectedIngredient = null;
            TxtSelectedStockProduct.Text = "(ยังไม่ได้เลือกวัตถุดิบ)";
            await LoadIngredientsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ไม่สามารถลบวัตถุดิบได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -------------------------------------------------------------
    // TAB 5: SALES REPORTS
    // -------------------------------------------------------------
    private async Task RefreshReportsAsync()
    {
        try
        {
            var report = await _api.GetDailyReportAsync();
            if (report != null)
            {
                TxtRptTotalSales.Text = $"{report.TotalSales:N2} บาท";
                TxtRptCashSales.Text = $"{report.CashSales:N2} บาท";
                TxtRptQrSales.Text = $"{report.QrSales:N2} บาท";
                TxtRptTotalOrders.Text = $"{report.TotalOrders} บิล";

                GridTopProducts.ItemsSource = null;
                GridTopProducts.ItemsSource = report.TopProducts;
            }
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Reports] Failed to refresh reports: " + ex.Message, ex);
        }
    }

    private async void BtnRefreshReports_Click(object sender, RoutedEventArgs e)
    {
        await RefreshReportsAsync();
    }

    // -------------------------------------------------------------
    // TAB 6: SETTINGS & SOUND ALERTS
    // -------------------------------------------------------------
    private void SliderSoundVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSoundVolumeLabel != null)
        {
            TxtSoundVolumeLabel.Text = $"{(int)SliderSoundVolume.Value}%";
        }
    }

    private void BtnTestSound_Click(object sender, RoutedEventArgs e)
    {
        var vol = (int)SliderSoundVolume.Value;
        AudioAlertService.TestAlert(vol);
    }

    private void BtnTestEscalatedSound_Click(object sender, RoutedEventArgs e)
    {
        var vol = (int)SliderSoundVolume.Value;
        AudioAlertService.TestEscalatedAlert(2, vol);
    }

    private void BtnTestUrgentSound_Click(object sender, RoutedEventArgs e)
    {
        var vol = (int)SliderSoundVolume.Value;
        AudioAlertService.TestEscalatedAlert(4, vol);
    }

    private void BtnSaveSound_Click(object sender, RoutedEventArgs e)
    {
        _config.SoundEnabled = ChkSoundEnabled.IsChecked == true;
        _config.RepeatAlertForUnacceptedOrders = ChkRepeatAlert.IsChecked == true;
        _config.EscalateAlertVolumeAndPitch = ChkEscalateSound.IsChecked == true;
        _config.SoundVolume = (int)SliderSoundVolume.Value;
        _config.Save();
        MessageBox.Show("บันทึกการตั้งค่าเสียงแจ้งเตือนเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSavePrinterSettings_Click(object sender, RoutedEventArgs e)
    {
        _config.ReceiptPrinterName = (CmbReceiptPrinter.SelectedItem as string)?.Contains("ไม่ใช้งาน") == true
            ? ""
            : CmbReceiptPrinter.SelectedItem as string ?? "";

        _config.KitchenPrinterName = (CmbKitchenPrinter.SelectedItem as string)?.Contains("ไม่ใช้งาน") == true
            ? ""
            : CmbKitchenPrinter.SelectedItem as string ?? "";

        _config.AutoPrintReceipt = ChkAutoReceipt.IsChecked == true;
        _config.AutoPrintKitchenSlip = ChkAutoKitchen.IsChecked == true;
        _config.Save();

        MessageBox.Show("บันทึกการตั้งค่าเครื่องพิมพ์เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSaveStoreSettings_Click(object sender, RoutedEventArgs e)
    {
        _config.StoreName = TxtStoreName.Text.Trim();
        _config.StoreAddress = TxtStoreAddress.Text.Trim();
        _config.StorePhone = TxtStorePhone.Text.Trim();
        _config.TaxId = TxtTaxId.Text.Trim();

        decimal.TryParse(TxtVatPercent.Text.Trim(), out var vat);
        _config.VatPercent = vat;

        decimal.TryParse(TxtServiceChargePercent.Text.Trim(), out var sc);
        _config.ServiceChargePercent = sc;

        _config.ReceiptHeader = TxtReceiptHeader.Text.Trim();
        _config.ReceiptFooter = TxtReceiptFooter.Text.Trim();

        _config.Save();
        MessageBox.Show("บันทึกข้อมูลร้านค้าและภาษีเรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnServerConfig_Click(object sender, RoutedEventArgs e)
    {
        var win = new ServerConfigDialog(_config) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _config = PosConfig.Load();
            _api.UpdateConnection(_config.GetBaseUrl(), _config.StoreCode);
            if (_realtime != null)
            {
                await _realtime.DisposeAsync();
            }
            _realtime = new RealtimeClient(_config.GetBaseUrl(), _config.StoreCode);
            SetupRealtime();
            _ = InitializeSystemAsync();
        }
    }

    // -------------------------------------------------------------
    // TAB 6: SETTINGS EXTENSIONS (Tables, Audit Logs, Advanced Sync)
    // -------------------------------------------------------------
    private async Task LoadTablesSettingsAsync()
    {
        try
        {
            _tablesList = await _api.GetTablesAsync();
            GridTablesManager.ItemsSource = null;
            GridTablesManager.ItemsSource = _tablesList;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Settings] Failed to load tables: " + ex.Message, ex);
        }
    }

    private async void BtnRefreshTables_Click(object sender, RoutedEventArgs e)
    {
        await LoadTablesSettingsAsync();
    }

    private async void BtnAddNewTable_Click(object sender, RoutedEventArgs e)
    {
        var num = TxtNewTableNumber.Text.Trim();
        if (string.IsNullOrWhiteSpace(num))
        {
            MessageBox.Show("กรุณากรอกเลขที่โต๊ะ", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var name = TxtNewTableName.Text.Trim();
        if (!int.TryParse(TxtNewTableCapacity.Text.Trim(), out var cap) || cap <= 0)
        {
            cap = 4;
        }

        try
        {
            await _api.CreateTableAsync(new CreateTableDto
            {
                TableNumber = num,
                Name = string.IsNullOrWhiteSpace(name) ? $"โต๊ะ {num}" : name,
                Capacity = cap
            });

            TxtNewTableNumber.Clear();
            TxtNewTableName.Clear();
            TxtNewTableCapacity.Text = "4";

            await LoadTablesSettingsAsync();
            UpdateTableButtonsVisuals();
            MessageBox.Show($"เพิ่มโต๊ะ {num} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Settings] Failed to create table: " + ex.Message, ex);
            MessageBox.Show("ไม่สามารถเพิ่มโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnToggleTableStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int tableId)
        {
            var table = _tablesList.FirstOrDefault(t => t.Id == tableId);
            if (table == null) return;

            var targetStatus = table.Status == TableStatus.Occupied ? TableStatus.Available : TableStatus.Occupied;
            try
            {
                await _api.UpdateTableStatusAsync(tableId, targetStatus);
                await LoadTablesSettingsAsync();
                UpdateTableButtonsVisuals();
            }
            catch (Exception ex)
            {
                PosLogger.Error($"[Settings] Failed to update table {tableId} status: " + ex.Message, ex);
                MessageBox.Show("ไม่สามารถเปลี่ยนสถานะโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnDeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int tableId)
        {
            var table = _tablesList.FirstOrDefault(t => t.Id == tableId);
            if (table == null) return;

            var confirm = MessageBox.Show($"ต้องการลบโต๊ะ {table.TableNumber} ใช่หรือไม่?", "ยืนยันการลบ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var success = await _api.DeleteTableAsync(tableId);
                if (success)
                {
                    await LoadTablesSettingsAsync();
                    UpdateTableButtonsVisuals();
                    MessageBox.Show($"ลบโต๊ะ {table.TableNumber} เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                PosLogger.Error($"[Settings] Failed to delete table {tableId}: " + ex.Message, ex);
                MessageBox.Show("ไม่สามารถลบโต๊ะได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task LoadAuditLogsAsync(string? search = null)
    {
        try
        {
            var logs = await _api.GetAuditLogsAsync(100, search);
            GridAuditLogs.ItemsSource = null;
            GridAuditLogs.ItemsSource = logs;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[Settings] Failed to load audit logs: " + ex.Message, ex);
        }
    }

    private async void BtnSearchAudit_Click(object sender, RoutedEventArgs e)
    {
        await LoadAuditLogsAsync(TxtSearchAudit.Text.Trim());
    }

    private async void BtnRefreshAudit_Click(object sender, RoutedEventArgs e)
    {
        TxtSearchAudit.Clear();
        await LoadAuditLogsAsync();
    }

    private void LoadAdvancedSettings()
    {
        ChkShowActivityBanner.IsChecked = _config.ShowActivityNotifications;
        ChkConfirmStatusChange.IsChecked = _config.ConfirmStatusChange;
        ChkAutoPrintOnPreparing.IsChecked = _config.AutoPrintKitchenOnPreparing;

        switch (_config.OrderAutoRefreshSeconds)
        {
            case 5: CmbAutoRefreshInterval.SelectedIndex = 1; break;
            case 10: CmbAutoRefreshInterval.SelectedIndex = 2; break;
            case 30: CmbAutoRefreshInterval.SelectedIndex = 3; break;
            default: CmbAutoRefreshInterval.SelectedIndex = 0; break;
        }

        SetupAutoRefreshTimer();
    }

    private void SetupAutoRefreshTimer()
    {
        _autoRefreshTimer?.Stop();
        if (_config.OrderAutoRefreshSeconds > 0)
        {
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_config.OrderAutoRefreshSeconds) };
            _autoRefreshTimer.Tick += (s, e) =>
            {
                if (TabOrders.IsChecked == true)
                {
                    _ = RefreshLiveOrdersAsync();
                }
            };
            _autoRefreshTimer.Start();
        }
    }

    private void BtnSaveAdvancedSettings_Click(object sender, RoutedEventArgs e)
    {
        _config.ShowActivityNotifications = ChkShowActivityBanner.IsChecked == true;
        _config.ConfirmStatusChange = ChkConfirmStatusChange.IsChecked == true;
        _config.AutoPrintKitchenOnPreparing = ChkAutoPrintOnPreparing.IsChecked == true;

        _config.OrderAutoRefreshSeconds = CmbAutoRefreshInterval.SelectedIndex switch
        {
            1 => 5,
            2 => 10,
            3 => 30,
            _ => 0
        };

        _config.Save();
        SetupAutoRefreshTimer();
        MessageBox.Show("บันทึกการตั้งค่าระบบ Sync & Workflow เรียบร้อยแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}