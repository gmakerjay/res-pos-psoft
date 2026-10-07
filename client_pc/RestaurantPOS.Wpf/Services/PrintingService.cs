using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using RestaurantPOS.Shared.DTOs;
using RestaurantPOS.Shared.Enums;
using RestaurantPOS.Wpf.Views;

namespace RestaurantPOS.Wpf.Services;

public class PrintingService
{
    public static void PrintReceipt(OrderDto order, string printerName = "")
    {
        try
        {
            PosLogger.Info($"[Printer] Printing receipt for order: {order.OrderNumber}");

            var printDoc = new PrintDocument();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                printDoc.PrinterSettings.PrinterName = printerName;
            }

            printDoc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;

                var fontTitle = new Font("Courier New", 12, FontStyle.Bold);
                var fontHeader = new Font("Courier New", 10, FontStyle.Bold);
                var fontRegular = new Font("Courier New", 9, FontStyle.Regular);
                var fontSmall = new Font("Courier New", 8, FontStyle.Regular);

                float y = 10;
                float left = 10;
                float width = 260; // 80mm standard printable width

                // Header
                g.DrawString("RESTAURANT POS", fontTitle, Brushes.Black, new RectangleF(left, y, width, 20), new StringFormat { Alignment = StringAlignment.Center });
                y += 20;
                g.DrawString("TAX INVOICE / RECEIPT", fontSmall, Brushes.Black, new RectangleF(left, y, width, 15), new StringFormat { Alignment = StringAlignment.Center });
                y += 18;
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;

                // Order metadata
                g.DrawString($"Bill No : {order.OrderNumber}", fontRegular, Brushes.Black, left, y); y += 14;
                g.DrawString($"Table   : {order.TableNumber ?? "Take Away"}", fontRegular, Brushes.Black, left, y); y += 14;
                g.DrawString($"Date    : {order.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}", fontRegular, Brushes.Black, left, y); y += 14;
                g.DrawString($"Cashier : {order.CreatedBy ?? "Staff"}", fontRegular, Brushes.Black, left, y); y += 16;
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;

                // Items header
                g.DrawString("Item", fontHeader, Brushes.Black, left, y);
                g.DrawString("Qty", fontHeader, Brushes.Black, left + 150, y);
                g.DrawString("Total", fontHeader, Brushes.Black, left + 200, y);
                y += 16;
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 14;

                // Items
                foreach (var item in order.Items)
                {
                    g.DrawString(item.ProductName, fontRegular, Brushes.Black, left, y);
                    g.DrawString(item.Quantity.ToString(), fontRegular, Brushes.Black, left + 155, y);
                    g.DrawString(item.Subtotal.ToString("N2"), fontRegular, Brushes.Black, left + 195, y);
                    y += 14;

                    if (!string.IsNullOrWhiteSpace(item.SpecialNotes))
                    {
                        g.DrawString($"  * {item.SpecialNotes}", fontSmall, Brushes.DarkGray, left, y);
                        y += 12;
                    }
                }

                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;

                // Totals
                g.DrawString("Subtotal:", fontRegular, Brushes.Black, left + 80, y);
                g.DrawString(order.Subtotal.ToString("N2"), fontRegular, Brushes.Black, left + 195, y);
                y += 14;

                if (order.DiscountAmount > 0)
                {
                    g.DrawString("Discount:", fontRegular, Brushes.Black, left + 80, y);
                    g.DrawString($"-{order.DiscountAmount:N2}", fontRegular, Brushes.Black, left + 195, y);
                    y += 14;
                }

                g.DrawString("TOTAL AMOUNT:", fontHeader, Brushes.Black, left + 60, y);
                g.DrawString($"{order.TotalAmount:N2} B", fontHeader, Brushes.Black, left + 185, y);
                y += 18;

                g.DrawString($"Paid ({order.PaymentMethod}):", fontRegular, Brushes.Black, left + 80, y);
                g.DrawString(order.PaidAmount.ToString("N2"), fontRegular, Brushes.Black, left + 195, y);
                y += 14;

                g.DrawString("Change:", fontRegular, Brushes.Black, left + 80, y);
                g.DrawString(order.ChangeAmount.ToString("N2"), fontRegular, Brushes.Black, left + 195, y);
                y += 18;

                // Footer
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;
                g.DrawString("THANK YOU / PLEASE COME AGAIN", fontSmall, Brushes.Black, new RectangleF(left, y, width, 15), new StringFormat { Alignment = StringAlignment.Center });
            };

            printDoc.Print();
            PosLogger.Info($"[Printer] Receipt printed successfully for {order.OrderNumber}");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[Printer Error] Failed to print receipt: {ex.Message}", ex);
            ErrorDialog.Show($"เครื่องพิมพ์มีปัญหา: {ex.Message}\nข้อมูลบิลถูกบันทึกในระบบเรียบร้อยแล้ว ไม่สูญหาย", "ข้อผิดพลาดเครื่องพิมพ์", ex.ToString());
        }
    }

    public static void PrintKitchenSlip(OrderDto order, string printerName = "")
    {
        try
        {
            PosLogger.Info($"[Printer] Printing kitchen slip for order: {order.OrderNumber}");

            var printDoc = new PrintDocument();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                printDoc.PrinterSettings.PrinterName = printerName;
            }

            printDoc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;

                var fontTitle = new Font("Courier New", 14, FontStyle.Bold);
                var fontHeader = new Font("Courier New", 11, FontStyle.Bold);
                var fontRegular = new Font("Courier New", 10, FontStyle.Regular);
                var fontNotes = new Font("Courier New", 9, FontStyle.Bold);

                float y = 10;
                float left = 10;
                float width = 260;

                g.DrawString("KITCHEN ORDER SLIP", fontTitle, Brushes.Black, new RectangleF(left, y, width, 22), new StringFormat { Alignment = StringAlignment.Center });
                y += 24;
                g.DrawString($"TABLE: {order.TableNumber ?? "TAKE AWAY"}", fontTitle, Brushes.Black, left, y);
                y += 22;
                g.DrawString($"Order : {order.OrderNumber}", fontRegular, Brushes.Black, left, y); y += 16;
                g.DrawString($"Time  : {order.CreatedAt.ToLocalTime():HH:mm:ss}", fontRegular, Brushes.Black, left, y); y += 18;
                g.DrawString("========================================", fontRegular, Brushes.Black, left, y);
                y += 16;

                foreach (var item in order.Items)
                {
                    g.DrawString($"[ {item.Quantity}x ]  {item.ProductName}", fontHeader, Brushes.Black, left, y);
                    y += 18;

                    if (!string.IsNullOrWhiteSpace(item.SpecialNotes))
                    {
                        g.DrawString($"   >>> หมายเหตุ: {item.SpecialNotes}", fontNotes, Brushes.Black, left, y);
                        y += 16;
                    }

                    if (item.Options.Any())
                    {
                        foreach (var opt in item.Options)
                        {
                            g.DrawString($"   + {opt.GroupName}: {opt.OptionName}", fontRegular, Brushes.Black, left, y);
                            y += 14;
                        }
                    }
                    y += 4;
                }

                g.DrawString("========================================", fontRegular, Brushes.Black, left, y);
            };

            printDoc.Print();
            PosLogger.Info($"[Printer] Kitchen slip printed successfully for {order.OrderNumber}");
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[Printer Error] Failed to print kitchen slip: {ex.Message}", ex);
            // Non-blocking error for kitchen print
        }
    }

    public static void PrintTestReceipt(PosConfig config, string printerName = "")
    {
        var dummyOrder = new OrderDto
        {
            OrderNumber = "TEST-0001",
            TableNumber = "T01",
            CustomerName = "ทดสอบการพิมพ์",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin",
            Subtotal = 150.00m,
            TotalAmount = 150.00m,
            PaidAmount = 200.00m,
            ChangeAmount = 50.00m,
            PaymentMethod = PaymentMethod.Cash,
            Items = new List<OrderItemDto>
            {
                new() { ProductName = "ทดสอบรายการที่ 1 (ชาไทย)", Quantity = 1, UnitPrice = 45.00m },
                new() { ProductName = "ทดสอบรายการที่ 2 (ผัดไทย)", Quantity = 1, UnitPrice = 105.00m, SpecialNotes = "ไม่ใส่ถั่วงอก" }
            }
        };
        PrintReceipt(dummyOrder, printerName);
    }

    public static void PrintTestKitchen(string printerName = "")
    {
        var dummyOrder = new OrderDto
        {
            OrderNumber = "KITCHEN-TEST",
            TableNumber = "T01",
            CustomerName = "ทดสอบครัว",
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItemDto>
            {
                new() { ProductName = "ต้มยำกุ้งน้ำข้น", Quantity = 2, SpecialNotes = "เผ็ดน้อย ไม่ใส่ผักชี" },
                new() { ProductName = "ข้าวผัดหมูกรอบ", Quantity = 1, SpecialNotes = "ขอพริกน้ำปลาแยก" }
            }
        };
        PrintKitchenSlip(dummyOrder, printerName);
    }

    public static void PrintTableQrSlip(string storeName, string storeCode, string tableNumber, string qrUrl, string printerName = "")
    {
        try
        {
            PosLogger.Info($"[Printer] Printing Table QR Slip for Table: {tableNumber} (Store: {storeCode})");

            var printDoc = new PrintDocument();
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                printDoc.PrinterSettings.PrinterName = printerName;
            }

            printDoc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;

                var fontTitle = new Font("Tahoma", 12, FontStyle.Bold);
                var fontHeader = new Font("Tahoma", 14, FontStyle.Bold);
                var fontRegular = new Font("Tahoma", 9, FontStyle.Regular);
                var fontSmall = new Font("Tahoma", 8, FontStyle.Regular);

                float y = 10;
                float left = 10;
                float width = 260; // 80mm printable width

                // Header
                g.DrawString(string.IsNullOrWhiteSpace(storeName) ? "RESTAURANT POS" : storeName, fontTitle, Brushes.Black, new RectangleF(left, y, width, 22), new StringFormat { Alignment = StringAlignment.Center });
                y += 22;
                g.DrawString($"[รหัสร้าน: {storeCode}]", fontSmall, Brushes.Black, new RectangleF(left, y, width, 16), new StringFormat { Alignment = StringAlignment.Center });
                y += 18;
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;

                // Table Title
                string tableLabel = tableNumber.Contains("กลับบ้าน") ? "[ สั่งกลับบ้าน / TAKEAWAY ]" : $"[ โต๊ะ: {tableNumber} ]";
                g.DrawString(tableLabel, fontHeader, Brushes.Black, new RectangleF(left, y, width, 26), new StringFormat { Alignment = StringAlignment.Center });
                y += 28;

                // QR Code Rendering using QRCoder
                try
                {
                    using var qrGenerator = new QRCoder.QRCodeGenerator();
                    using var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
                    using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
                    byte[] qrBytes = qrCode.GetGraphic(5);
                    using var ms = new MemoryStream(qrBytes);
                    using var qrImg = System.Drawing.Image.FromStream(ms);
                    
                    float qrSize = 160;
                    float qrLeft = left + (width - qrSize) / 2;
                    g.DrawImage(qrImg, qrLeft, y, qrSize, qrSize);
                    y += qrSize + 10;
                }
                catch (Exception qrEx)
                {
                    PosLogger.Warn($"[Printer] QR Render Warning: {qrEx.Message}");
                    g.DrawString($"Scan URL: {qrUrl}", fontSmall, Brushes.Black, new RectangleF(left, y, width, 30), new StringFormat { Alignment = StringAlignment.Center });
                    y += 35;
                }

                // Instructions
                g.DrawString("[ สแกนสั่งอาหารประจำโต๊ะนี้ ]", fontRegular, Brushes.Black, new RectangleF(left, y, width, 18), new StringFormat { Alignment = StringAlignment.Center });
                y += 18;
                g.DrawString("ออเดอร์ส่งตรงเข้าห้องครัวและแคชเชียร์ทันที", fontSmall, Brushes.Black, new RectangleF(left, y, width, 16), new StringFormat { Alignment = StringAlignment.Center });
                y += 18;
                g.DrawString("----------------------------------------", fontRegular, Brushes.Black, left, y);
                y += 15;
                g.DrawString($"พิมพ์เมื่อ: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", fontSmall, Brushes.Gray, new RectangleF(left, y, width, 16), new StringFormat { Alignment = StringAlignment.Center });
            };

            printDoc.Print();
        }
        catch (Exception ex)
        {
            PosLogger.Error($"[Printer] Failed to print table QR slip: {ex.Message}", ex);
            throw;
        }
    }
}

