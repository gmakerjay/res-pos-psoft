using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Wpf.Services;

namespace RestaurantPOS.Wpf.Views;

public partial class TableQrDialog : Window
{
    private readonly ApiClient _api;
    private readonly PosConfig _config;
    private List<TableDto> _tables = new();
    private TableDto? _selectedTable;
    private bool _isTakeawaySelected = false;
    private byte[]? _currentQrBytes;

    public TableQrDialog(ApiClient api, PosConfig config)
    {
        InitializeComponent();
        _api = api;
        _config = config;

        TxtStoreHeader.Text = $"ร้าน: {_config.StoreName} (รหัสร้าน: {_config.StoreCode}) | เครื่อง: {_config.StationName}";
        Loaded += async (s, e) => await LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        try
        {
            TxtStatus.Text = "กำลังโหลดข้อมูลโต๊ะจากเซิร์ฟเวอร์...";
            _tables = await _api.GetTablesAsync();

            // Prepare list with Takeaway item at the top
            var displayList = new List<TableDto>();
            displayList.Add(new TableDto
            {
                Id = -1,
                TableNumber = "กลับบ้าน",
                Name = "สั่งกลับบ้าน / ทางบ้าน (Takeaway)",
                Capacity = 0
            });
            displayList.AddRange(_tables);

            ListTables.ItemsSource = displayList;
            if (displayList.Any())
            {
                ListTables.SelectedIndex = displayList.Count > 1 ? 1 : 0;
            }
            TxtStatus.Text = $"โหลดรายการโต๊ะเรียบร้อย ({_tables.Count} โต๊ะ)";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "ข้อผิดพลาด: " + ex.Message;
            PosLogger.Error("[TableQrDialog] Failed to load tables: " + ex.Message, ex);
        }
    }

    private void ListTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListTables.SelectedItem is not TableDto table) return;

        _selectedTable = table;
        _isTakeawaySelected = (table.Id == -1 || table.TableNumber == "กลับบ้าน");

        string serverBase = _config.GetBaseUrl().TrimEnd('/');
        // Normalize server base to https if spk domain
        if (serverBase.Contains("spk.p-services.net") && serverBase.StartsWith("http://"))
        {
            serverBase = "https://" + serverBase.Substring(7);
        }

        string qrUrl;
        if (_isTakeawaySelected)
        {
            TxtSelectedTitle.Text = "[ ป้าย QR ร้าน: สั่งกลับบ้าน / ทางบ้าน ]";
            TxtSelectedSubtitle.Text = "ลูกค้าสแกนสั่งจากที่บ้าน หรือสั่งล่วงหน้าแล้วมารับที่ร้าน";
            qrUrl = $"{serverBase}/?store={Uri.EscapeDataString(_config.StoreCode)}&type=takeaway";
            BtnDeleteTable.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtSelectedTitle.Text = $"[ โต๊ะอาหาร: {table.TableNumber} ]";
            TxtSelectedSubtitle.Text = $"{table.Name} (สแกนสั่งอาหารประจำโต๊ะนี้)";
            qrUrl = $"{serverBase}/?store={Uri.EscapeDataString(_config.StoreCode)}&table={Uri.EscapeDataString(table.TableNumber)}";
            BtnDeleteTable.Visibility = Visibility.Visible;
        }

        TxtQrUrl.Text = qrUrl;
        RenderQrCode(qrUrl);
    }

    private void RenderQrCode(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            _currentQrBytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(_currentQrBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            ImgQrCode.Source = bitmap;
        }
        catch (Exception ex)
        {
            PosLogger.Error("[TableQrDialog] Failed to generate QR: " + ex.Message, ex);
        }
    }

    private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtQrUrl.Text))
        {
            Clipboard.SetText(TxtQrUrl.Text);
            TxtStatus.Text = "คัดลอกลิงก์ไปยังคลิปบอร์ดแล้ว!";
        }
    }

    private void BtnPrintThermalSlip_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null || string.IsNullOrWhiteSpace(TxtQrUrl.Text)) return;

        try
        {
            PrintingService.PrintTableQrSlip(
                storeName: _config.StoreName,
                storeCode: _config.StoreCode,
                tableNumber: _isTakeawaySelected ? "กลับบ้าน (Takeaway)" : _selectedTable.TableNumber,
                qrUrl: TxtQrUrl.Text,
                printerName: _config.ReceiptPrinterName
            );

            TxtStatus.Text = $"สั่งพิมพ์สลิป QR {_selectedTable.TableNumber} เรียบร้อยแล้ว";
            MessageBox.Show($"สั่งพิมพ์สลิป QR เรียบร้อยแล้ว!\nโต๊ะ: {_selectedTable.TableNumber}", "การพิมพ์สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "เกิดข้อผิดพลาดในการพิมพ์: " + ex.Message;
            MessageBox.Show("ไม่สามารถพิมพ์สลิปได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSaveQrPng_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQrBytes == null || _selectedTable == null) return;

        var sfd = new SaveFileDialog
        {
            FileName = $"QR_{_config.StoreCode}_{_selectedTable.TableNumber}.png",
            Filter = "PNG Image (*.png)|*.png"
        };

        if (sfd.ShowDialog() == true)
        {
            File.WriteAllBytes(sfd.FileName, _currentQrBytes);
            TxtStatus.Text = "บันทึกไฟล์รูปภาพ QR สำเร็จ: " + Path.GetFileName(sfd.FileName);
        }
    }

    private void BtnOpenWebSheet_Click(object sender, RoutedEventArgs e)
    {
        string serverBase = _config.GetBaseUrl().TrimEnd('/');
        if (serverBase.Contains("spk.p-services.net") && serverBase.StartsWith("http://"))
        {
            serverBase = "https://" + serverBase.Substring(7);
        }

        string printUrl = $"{serverBase}/?store={Uri.EscapeDataString(_config.StoreCode)}&view=qr";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = printUrl,
                UseShellExecute = true
            });
            TxtStatus.Text = "เปิดหน้าพิมพ์สติ๊กเกอร์บนเว็บเบราว์เซอร์แล้ว";
        }
        catch (Exception ex)
        {
            MessageBox.Show("ไม่สามารถเปิดเบราว์เซอร์ได้: " + ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnAddTable_Click(object sender, RoutedEventArgs e)
    {
        string newNo = TxtNewTableNo.Text.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(newNo))
        {
            MessageBox.Show("กรุณากรอกรหัสโต๊ะ เช่น T13 หรือ VIP1", "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TxtStatus.Text = "กำลังเพิ่มโต๊ะใหม่...";
            await _api.CreateTableAsync(new CreateTableDto
            {
                TableNumber = newNo,
                Name = $"โต๊ะ {newNo}",
                Capacity = 4
            });

            TxtNewTableNo.Clear();
            await LoadTablesAsync();
            TxtStatus.Text = $"เพิ่มโต๊ะ {newNo} สำเร็จแล้ว!";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = "ข้อผิดพลาด: " + ex.Message;
        }
    }

    private async void BtnDeleteTable_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTable == null || _isTakeawaySelected) return;

        var confirm = MessageBox.Show(
            $"คุณแน่ใจหรือไม่ว่าต้องการลบ {_selectedTable.TableNumber} ({_selectedTable.Name})?",
            "ยืนยันการลบโต๊ะ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            TxtStatus.Text = "กำลังลบโต๊ะ...";
            await _api.DeleteTableAsync(_selectedTable.Id);
            await LoadTablesAsync();
            TxtStatus.Text = $"ลบโต๊ะ {_selectedTable.TableNumber} สำเร็จแล้ว";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ข้อผิดพลาด", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = "ข้อผิดพลาด: " + ex.Message;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
