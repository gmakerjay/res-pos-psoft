# Restaurant POS — Windows Desktop Client (WPF)

โฟลเดอร์นี้รวบรวมซอร์สโค้ดและเครื่องมือรันสำหรับ **โปรแกรมเครื่องลูกข่ายหน้าร้าน (Windows Desktop POS Client)** ออกแบบด้วยสไตล์ Classic Windows XP สำหรับใช้งานบนเครื่องคอมพิวเตอร์แคชเชียร์

---

## 1. องค์ประกอบภายในโฟลเดอร์ (Components)

- **`RestaurantPOS.Wpf/`**: C# .NET 10 WPF Application
  - **Classic XP Theme**: ปุ่มนูน 3D, Gradient Header สีน้ำเงินเข้ม, แถบสถานะสีเทาคลาสสิก สบายตา ใช้งานง่าย
  - **การพิมพ์โดยตรง (Direct Printing)**: สั่งพิมพ์สลิปและใบสั่งครัวผ่าน Windows Spooler หรือ Raw ESC/POS โดยตรงจากเครื่อง Windows หน้าร้าน
  - **ระบบเชื่อมต่อเซิร์ฟเวอร์แบบเกม**: รองรับการระบุ Server IP Address (เช่น `192.168.1.100:5000`) พร้อมปุ่ม "ทดสอบการเชื่อมต่อ" (Ping `/api/health`) และบันทึกลง `pos_config.json`
  - **Global Exception Handlers**: ดักจับทุก Unhandled Exception และส่ง Crash Logs ไปยังเซิร์ฟเวอร์กลางพร้อมหน้าต่างแจ้งเตือน `ErrorDialog.xaml`
- **`RestaurantPOS.Shared/`**: Shared Class Library (.NET 10)
  - DTOs, Enums, Models, ErrorCodes
- **`RestaurantPOS.Client.slnx`**: Solution File แยกสำหรับเปิดและพัฒนาเฉพาะฝั่ง Client POS บน Visual Studio / Rider

---

## 2. การรันและบิลด์โปรแกรม (Run & Build Scripts)

| สคริปต์ | หน้าที่ |
| :--- | :--- |
| **`run_pos.bat`** | รันโปรแกรม RestaurantPOS.Wpf บนเครื่อง Windows ทันที (หากยังไม่ได้บิลด์จะสั่งบิลด์ให้อัตโนมัติ) |
| **`build_client.bat`** | คอมไพล์โหมด Release และส่งไฟล์บิลด์ที่พร้อมใช้งานไปยัง `build_output/client_pc/` |

---

## 3. การกำหนดค่า (Configuration)

เมื่อรันโปรแกรม ไฟล์การตั้งค่าจะอยู่ที่ `pos_config.json`:
```json
{
  "ServerBaseUrl": "http://localhost:5000",
  "PrinterName": "XP-80C",
  "KitchenPrinterName": "KitchenPrinter",
  "AutoPrintReceipt": true,
  "AutoPrintKitchenTicket": true
}
```
หากเครื่องเปิดขึ้นมาแล้วติดต่อเซิร์ฟเวอร์ไม่ได้ โปรแกรมจะเปิดหน้าต่างตั้งค่า Server IP ให้โดยอัตโนมัติ
