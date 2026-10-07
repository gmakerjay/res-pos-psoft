# Restaurant POS System (Enterprise Edition)

ระบบบริหารจัดการร้านอาหารแบบครบวงจร (Restaurant Point of Sale System) สถาปัตยกรรม Client-Server สมัยใหม่ รองรับทั้งเครื่องลูกข่ายหน้าร้าน Windows Desktop POS, เครื่องแม่ข่าย Linux/Windows Server, และระบบสั่งอาหารผ่าน QR Code / ระบบจัดการร้านผ่าน Web App

---

## กฏเหล็กประจำโครงการ (Strict Rules)

1. **ห้ามใช้อิโมจิในโปรแกรมโดยเด็ดขาด (Strictly NO Emojis):**
   - ห้ามใส่อิโมจิใดๆ ในส่วนของ User Interface (UI), ปุ่มกด (Buttons), หัวข้อเมนู (Navigation), ข้อความแจ้งเตือน (Dialogs/Alerts), สลิปใบเสร็จ (Thermal Receipts), รายงาน (Reports) หรือข้อความในโค้ดที่แสดงผลต่อผู้ใช้งาน
   - ใช้ข้อความทางการและ Color Badges (เช่น `[ว่าง]`, `[มีลูกค้า]`, `[รอชำระเงิน]`, `[Online]`, `[Offline]`) แทน

2. **ระบบ Server Connection & IP Configuration (Client-Server แบบเซิร์ฟเวอร์เกม):**
   - เครื่องแม่ข่าย **Linux Server** รัน ASP.NET Core Web API + SignalR ที่ Port `5000` ทำหน้าที่เป็นศูนย์กลางฐานข้อมูลและกระจายข้อมูล Real-time
   - เครื่องลูกข่าย **Windows Desktop POS** และ **Web App** สามารถตั้งค่า **Server IP Address** (เช่น `192.168.1.100:5000`, `10.0.0.5:5000`, หรือ `127.0.0.1:5000`) ได้อย่างอิสระ
   - มีปุ่ม **"ทดสอบการเชื่อมต่อ (Test Connection)"** เพื่อ Ping ไปยัง `/api/health` ทันที หากเชื่อมต่อได้จะขึ้นสีเขียวพร้อมบันทึกลง `pos_config.json` เปิดโปรแกรมใหม่ไม่ต้องตั้งค่าซ้ำ
   - หากโปรแกรมเปิดขึ้นมาแล้วไม่พบเซิร์ฟเวอร์ จะแสดงหน้าต่างตั้งค่า IP ขึ้นมาให้ผู้ใช้กรอกทันที

3. **ระบบ Logger & Error Handler ทุกจุด (Full-Stack Error Resilience):**
   - **Central Server (ASP.NET Core):** `GlobalExceptionHandlingMiddleware` ดักจับทุก Unhandled Exception ในระบบ พร้อม Log Request Path, Method, Client IP, TraceId และแปลงเป็น JSON มาตรฐาน `ApiResponse.Fail`, Serilog บันทึกลง `logs/server-YYYYMMDD.log` หมุนเวียนรายวัน และรองรับ `POST /api/logs/client`
   - **Windows Desktop POS (WPF):** Global Exception Handlers ใน `App.xaml.cs` ดักจับ `DispatcherUnhandledException`, `AppDomain.UnhandledException`, และ `TaskScheduler.UnobservedTaskException` ไม่ให้โปรแกรม Crash ดับกลางคัน พร้อมหน้าต่างแจ้งเตือน `ErrorDialog.xaml` สไตล์ Classic XP
   - **Web App (React SPA):** `ErrorBoundary.tsx` ดักจับ React Component crash และส่ง error log กลับมายังเซิร์ฟเวอร์

4. **การพิมพ์สลิปและใบสั่งครัว (Printing Policy):**
   - การสั่งพิมพ์ทั้งหมดต้องผ่านเครื่องคอมพิวเตอร์ Windows PC หน้าร้านโดยตรง (Windows Spooler / ESC/POS)
   - ห้ามให้ Web App สั่งเครื่องพิมพ์โดยตรง

5. **กฎเหล็กการ Deploy (Strict Deployment Gate Policy):**
   - **ห้ามทำการ Deploy อัตโนมัติหลังแก้โค้ดเด็ดขาด:** รันเฉพาะ Automated Tests หรือ Build เพื่อตรวจสอบความถูกต้อง ห้ามปล่อยตัวโปรแกรมขึ้น Production โดยพลการ ต้องหยุดและสรุปผลให้ผู้ใช้เป็นผู้ทดสอบและสั่งการ Deploy เสมอ
   - **ห้ามสร้างไฟล์บีบอัด (.zip/.rar) เด็ดขาด:** ส่งมอบเฉพาะโฟลเดอร์ตัวเต็มที่พร้อมใช้งาน (Uncompressed Folders)

6. **การบันทึก Progress การทำงานอย่างกระชับ (Lean Progress Logging Policy):**
   - ต้องบันทึกความคืบหน้าการทำงาน (Progress Log) ลงใน `README.md` ทุกครั้งที่มีการพัฒนาหรือปรับปรุงระบบ
   - **อย่าให้โปรเกรสบวม (Keep it Lean & Structured):** สรุปเป็นตารางกระชับ ระบุเฉพาะสาระสำคัญ วันที่/เวอร์ชัน โมดูล และผลการทดสอบ ไม่บันทึกเยิ่นเย้อหรือใส่โค้ดยาว

---

## ผังโครงสร้างโครงการ (Directory Structure)

```text
res-pos_psoft/
├── web_server/                         # [ส่วนที่ 1] ซอสฝั่งเว็บและเซิร์ฟเวอร์กลาง (พร้อม Deploy บน Linux)
│   ├── RestaurantPOS.Server/           # ASP.NET Core 10 Web API + SignalR Hubs + EF Core
│   ├── RestaurantPOS.Web/              # Vite + React 19 + TypeScript (Customer QR & Management)
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10 DTOs, Enums, Models)
│   ├── deploy_linux/                   # เครื่องมือและคอนฟิกสำหรับ Deploy บน Linux (Systemd, Nginx, Docker)
│   ├── RestaurantPOS.WebServer.slnx    # Solution แยกสำหรับงาน Web & Server
│   ├── run_server.bat                  # รัน Central Server บน Windows
│   ├── run_web.bat                     # รัน Vite Web Dev Server
│   └── run_all.bat                     # รันทั้ง Server และ Web พร้อมกัน
│
├── client_pc/                          # [ส่วนที่ 2] ซอสฝั่ง Client PC Software (เครื่องแคชเชียร์หน้าร้าน)
│   ├── RestaurantPOS.Wpf/              # C# .NET 10 WPF Desktop POS (Classic Windows XP Theme)
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10)
│   ├── RestaurantPOS.Client.slnx       # Solution แยกสำหรับงาน Windows Desktop POS
│   ├── run_pos.bat                     # รันหรือบิลด์โปรแกรม Desktop POS ทันที
│   └── build_client.bat                # คอมไพล์โหมด Release และส่งออกไปยัง build_output
│
├── build_output/                       # [ส่วนที่ 3] โฟลเดอร์โปรแกรมที่บิลด์ออกมาแล้ว (รอคำสั่ง Deploy)
│   ├── client_pc/                      # ไฟล์บิลด์ตัวเต็มของ Windows Desktop POS (มี RestaurantPOS.Wpf.exe)
│   └── web_server/                     # ไฟล์บิลด์ตัวเต็มของ Central Server + Web SPA ใน wwwroot
│
├── RestaurantPOS.slnx                  # Master Solution File รวมทุกโปรเจกต์
├── run.bat                             # Master Quick Launcher เมนูลัดสำหรับรันทั้งระบบ
├── run_pos.bat                         # ลัดเปิดรัน Client POS หน้าร้าน
├── run_server.bat                      # ลัดเปิดรัน Central Server
├── run_web.bat                         # ลัดเปิดรัน Web App Dev Server
└── AGENTS.md                           # กฎและระเบียบสถาปัตยกรรมสำหรับ AI Agent
```

---

## การเริ่มต้นใช้งานด่วน (Quick Start)

ดับเบิลคลิกไฟล์ **`run.bat`** ที่โฟลเดอร์หลัก เพื่อเปิดเมนูลัด:
```text
======================================================================
          RESTAURANT POS SYSTEM - QUICK LAUNCHER (v1.0.0)
======================================================================

  [1] Run All Components (Server + Web App + Windows POS)
  [2] Launch Windows Desktop POS (Classic XP Client)
  [3] Start Central Server (ASP.NET Core Port 5000)
  [4] Start Web App Dev Server (Vite Port 5173)
  [5] Build Entire Solution (RestaurantPOS.slnx)
  [6] Launch Published Client POS (build_output)
  [0] Exit
```

---

## บัญชีผู้ใช้งานเริ่มต้น (Default Credentials)

ระบบกำหนดรหัสผ่านตั้งต้นไว้สำหรับเข้าใช้งานทั้งบน Windows Desktop POS และ Web Application:
- **ผู้ดูแลระบบ (SuperAdmin):** `admin` / `psoft123`
- **พนักงานแคชเชียร์ (Cashier):** `cashier` / `psoft123`

---

## คุณสมบัติเด่นของระบบ (Core Capabilities)

### 1. ระบบ Real-Time 2-Way Synchronization (SignalR WebSocket)
- **การซิงค์สองทิศทางระหว่าง Web และ PC:**
  - เมื่อลูกค้าสั่งอาหารผ่านมือถือ (Web QR) ➔ เครื่อง PC POS มีเสียงเตือน, ปุ่มโต๊ะเปลี่ยนสีเป็นสีส้ม `[มีลูกค้า]`, แสดงแถบแจ้งเตือนบิลใหม่ และรายการออเดอร์ในโต๊ะอัปเดตสดทันที
  - เมื่อแคชเชียร์หรือครัวปรับสถานะออเดอร์ (`[รับออเดอร์แล้ว]`, `[กำลังปรุง]`, `[พร้อมเสิร์ฟ]`) ➔ จอมือถือลูกค้าอัปเดต Timeline ทันทีแบบไม่ต้องกดรีเฟรช
  - เมื่อแคชเชียร์เช็คบิลปิดโต๊ะ (F10) ➔ จอลูกค้าแสดงสถานะเสร็จสิ้น, จอครัว (KDS) ตัดรายการออก, แดชบอร์ดสรุปยอดขายอัปเดตทันที

### 2. ระบบจัดการโต๊ะและการสั่งอาหารแบบต่อเนื่อง (Multi-Round Table Ordering)
- **ปุ่มโต๊ะพร้อม Hover & Visual States:** ปุ่มโต๊ะ (T1-T8 + กลับบ้าน) มีเอฟเฟกต์ Hover เมื่อชี้เมาส์, ไฮไลต์สีน้ำเงินเข้มเมื่อเลือกโต๊ะนั้น (Selected State), และเปลี่ยนเป็นสีส้มอำพันเมื่อโต๊ะมีออเดอร์ค้างอยู่ (Occupied State)
- **พาเนลแสดงรายการที่สั่งไปแล้วของแต่ละโต๊ะ (Existing Orders Panel):** แคชเชียร์สามารถกดเลือกโต๊ะเพื่อดูว่าโต๊ะนั้นสั่งอะไรไปแล้วบ้าง, จำนวนกี่รายการ, สถานะของแต่ละจาน, และยอดรวมสะสมของโต๊ะ
- **สั่งอาหารเพิ่มเข้าโต๊ะเดิมโดยไม่เขียนทับ (No Overwriting):** รองรับการสั่งอาหารรอบที่ 2 หรือ 3 โดยระบบจะสร้างรอบใหม่ของโต๊ะนั้น และนำมารวมยอดสะสมให้อัตโนมัติ
- **เช็คบิลรวมโต๊ะในคราวเดียว (Consolidated Payment F10):** สามารถชำระเงินและปิดบิลรวมทุกรอบของโต๊ะนั้นในครั้งเดียว

### 3. ระบบสต๊อกวัตถุดิบแยกจากรายการอาหาร (Raw Material Inventory & Menu Management)
- **จัดการสต๊อกเป็นวัตถุดิบแท้จริง:** สต๊อกคือการบันทึกวัตถุดิบ (เช่น เนื้อหมู, ไข่ไก่, ข้าวสาร) พร้อมหน่วยนับและจุดสั่งซื้อเตือนหมด มีระบบปรับยอดสต๊อก (Adjust Stock) พร้อมระบุเหตุผล
- **ระบบเมนูอาหาร (Menu & Options):** จัดการรายการอาหาร หมวดหมู่ และตัวเลือกพิเศษ (เช่น ระดับความหวาน, เพิ่มท็อปปิ้ง)
- **การจัดการของหมด (Out of Stock Handling):** หากสินค้าหรือวัตถุดิบหมด สามารถระบุหมายเหตุสาเหตุที่หมดได้ และบนหน้าเว็บ/POS จะแสดงเป็นสีจาง กดสั่งไม่ได้ พร้อมป้ายคำว่า "หมด" และระบุสาเหตุ
- **ดูรูปภาพเมนูขนาดเต็ม (Full Size Image Modal):** ลูกค้าสามารถกดดูรูปภาพขนาดใหญ่และรายละเอียดอาหารได้จากหน้าเว็บ

### 4. ระบบสั่งอาหารผ่าน QR Code พร้อมการยืนยันเบอร์โทร (Customer QR Ordering)
- **บังคับยืนยันเบอร์โทรศัพท์:** ก่อนส่งออเดอร์ ลูกค้าต้องกรอกเบอร์โทรศัพท์ (อย่างน้อย 9-10 หลัก) เพื่อให้ทางร้านสามารถโทรยืนยันได้ ป้องกันการสั่งเล่น
- **หน้าต่างยืนยันออเดอร์ (Confirmation Modal):** มีป๊อปอัปให้ลูกค้ายืนยันเบอร์โทรศัพท์และหมายเหตุของโต๊ะก่อนส่งเข้าครัวจริง

### 5. ระบบความปลอดภัยและการแยกส่วนเจ้าหน้าที่ (Role Security & Staff Isolation)
- ซ่อนปุ่มตั้งค่าเซิร์ฟเวอร์ และเมนูจัดการร้าน (จอครัว, แดชบอร์ด, จัดการเมนู, สต๊อกวัตถุดิบ, Audit Logs) จากมุมมองลูกค้าทั่วไป
- จะแสดงเมนูจัดการก็ต่อเมื่อเจ้าหน้าที่ล็อกอินด้วยบัญชีที่ถูกต้องเท่านั้น

### 6. การปรับระดับเสียงและการพิมพ์ (Sound & Printing Control)
- สามารถปรับระดับเสียงเตือนออเดอร์เข้า (Volume Slider) และกดปุ่มทดสอบเสียง (Test Sound) ได้ทั้งบน Web และ WPF POS
- ระบบพิมพ์สลิปใบเสร็จและใบสั่งครัวผ่าน Windows Spooler / ESC/POS บนเครื่อง PC หน้าร้าน

### 7. ระบบหลายสาขา/ร้านค้าแยกฐานข้อมูลเด็ดขาด (Database-per-Tenant Multi-Store)
- **แยกฐานข้อมูลเป็นเอกเทศน์ 100% (Physical Database Isolation):** แต่ละร้านมีไฟล์ฐานข้อมูลของตนเอง (`tenants/{StoreCode}.db`) แยกขาดจาก Master Catalog (`tenants/master.db`) ข้อมูลไม่ปะปนกัน สำรองและกู้คืนข้อมูลเฉพาะร้านได้อิสระ
- **ระบบสมัครเปิดร้านใหม่ (Store Registration):** รองรับการลงทะเบียนเปิดร้านใหม่ผ่าน Web App พร้อมสร้างฐานข้อมูลร้านและบัญชีผู้ดูแลร้านให้อัตโนมัติ
- **การเชื่อมต่อผ่านรหัสร้าน (Store Code Binding):** เครื่องลูกข่าย Windows Desktop POS และ Web App ระบุ Server IP และรหัสร้านค้า (Store Code) พร้อมระบบตรวจสอบความถูกต้องก่อนเชื่อมต่อ และแยกห้อง Real-Time SignalR เฉพาะร้านค้า

### 8. ระบบสร้างและพิมพ์ป้าย QR Code ประจำโต๊ะและสั่งกลับบ้าน (Table QR & Takeaway System)
- **ป้าย QR ติดโต๊ะเฉพาะร้าน (Table QR Stickers):** ผูกรหัสร้านและเลขโต๊ะ (`?store=...&table=...`) ออเดอร์ส่งตรงเข้าโต๊ะนั้นๆ บน POS ทันที
- **ป้าย QR ร้านสั่งกลับบ้าน (Store QR Takeaway):** สำหรับลูกค้าสั่งจากทางบ้านหรือสั่งล่วงหน้า (`?store=...&type=takeaway`)
- **พิมพ์สติ๊กเกอร์กระดาษ A4 หรือ สลิปความร้อน 80mm:** สั่งพิมพ์สติ๊กเกอร์ A4 Grid ทางเว็บ หรือพิมพ์สลิป QR ติดโต๊ะผ่าน C# WPF POS (ESC/POS)

---


## บันทึกความคืบหน้าการพัฒนา (Project Progress Log)

| เวอร์ชัน / แก้ไขครั้งที่ / วันที่ | โมดูล / ส่วนงาน | รายละเอียดการพัฒนาหลัก | สถานะการทดสอบ |
|---|---|---|---|
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 1)** (2026-10-07) | Architecture & Foundation | วางโครงสร้าง Solution 3 ส่วน (`web_server`, `client_pc`, `build_output`), ระบบ Client-Server IP Config, Full-Stack Exception Handling, Serilog และ Master Launcher `run.bat` | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 2)** (2026-10-07) | Inventory & Menu Split | แยกสต๊อกเป็นวัตถุดิบ (Ingredients) ออกจากรายการอาหาร (Products), ระบบแจ้งของหมดพร้อมระบุสาเหตุ, ป๊อปอัปดูรูปเมนูขนาดเต็ม | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 3)** (2026-10-07) | Security & Staff Isolation | ซ่อนการตั้งค่าเซิร์ฟเวอร์และเมนูจัดการจากลูกค้า, แสดงเฉพาะเมื่อล็อกอินเจ้าหน้าที่สำเร็จ, ปรับขนาดฟอนต์/UI บน WPF POS สไตล์ Classic XP ให้ชัดเจน | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 4)** (2026-10-07) | Customer Confirmation & Audio | เพิ่มระบบบังคับกรอกเบอร์โทรและป๊อปอัปยืนยันก่อนสั่งอาหารป้องกันการสั่งเล่น, ระบบปรับระดับเสียงเตือนออเดอร์เข้าและปุ่มทดสอบเสียง | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 5)** (2026-10-07) | Table Management & Multi-Round | ปุ่มเลือกโต๊ะพร้อม Hover Effect, Active/Selected State, Occupied State, พาเนลดูรายการที่สั่งไปแล้วของโต๊ะ, รองรับการสั่งอาหารเพิ่มโดยไม่เขียนทับ, เช็คบิลรวมโต๊ะ (Consolidated Pay F10) | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 6)** (2026-10-07) | Real-Time 2-Way Sync | เชื่อมต่อ SignalR แบบสองทิศทางเต็มรูปแบบระหว่าง Web และ PC (เตือนออเดอร์สด, อัปเดตสถานะปรุง/เสิร์ฟสด, อัปเดตยอดโต๊ะและปิดบิลสด) | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 7)** (2026-10-07) | Multi-Tenant (DB-per-Tenant) | สถาปัตยกรรมแยกฐานข้อมูลเด็ดขาดรายร้าน (1 ร้าน 1 ฐานข้อมูล), ระบบสมัครเปิดร้านใหม่ผ่านเว็บ, แยกช่องสัญญาณ Real-Time SignalR รายร้าน, และระบบเชื่อมต่อรหัสร้าน (Store Code) บน PC POS และ Web | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 8)** (2026-10-07) | Production Deployment & Re-Audit | Re-Audit ทุกฟีเจอร์ผ่าน 100%, สำรองรูปอาหารทั้งหมด (13 รูป) ไว้ที่ `food_images_backup/`, Deploy ออนไลน์ขึ้น Linux Server (`192.168.1.247`) ให้บริการผ่าน `https://spk.p-services.net/` สำเร็จ โดยไม่แตะต้องระบบ pcom และ Cloudflare เดิม | ผ่านการทดสอบใช้งานจริง 100% |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 9)** (2026-10-07) | Software Landing & Mobile Responsive | ปรับหน้าเว็บหลักเป็นหน้านำเสนอซอฟต์แวร์ Restaurant POS และจุดเชื่อมต่อระบบ, ระบบลงทะเบียนขอรับรหัสความปลอดภัย (Store Code) เพื่อทดลองใช้ฟรีบนโปรแกรม Windows POS, แยกโฟลวสั่งอาหารของลูกค้าผ่าน QR ประจำร้าน (`?store=...`), และปรับปรุงการแสดงผลบนสมาร์ทโฟนให้ Full Responsive ไม่ล้นจอ | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 10)** (2026-10-07) | Table QR Generator & Takeaway Sync | พัฒนาระบบสร้างและพิมพ์ป้าย QR Code ติดโต๊ะอาหารเฉพาะร้าน (Table QR Manager) ทั้งบน Web และ C# WPF POS: รองรับการสั่งพิมพ์สติ๊กเกอร์ A4 Sheet, สั่งพิมพ์สลิปความร้อน 80mm ผ่าน ESC/POS, สร้าง QR ป้ายร้านสำหรับสั่งจากบ้าน (Takeaway `?type=takeaway`), แยกโหมดการสั่งอาหารบน Web และระบบเพิ่ม/ลบโต๊ะใหม่แบบไดนามิก | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 11)** (2026-10-07) | High-Entropy Codes & Licensing System | 1) ระบบสร้างรหัสร้านค้าความยาวสูงแบบถอดยาก `RPOS-XXXX-XXXX-XXXX` (Crockford Base32 CSPRNG) ป้องกันการสุ่มเดา, 2) ระบบทดลองใช้ฟรี 14 วัน (Full-feature Trial) พร้อมนับถอยหลังและระงับการสั่งขายเมื่อหมดอายุ, 3) ศูนย์จัดการสิทธิ์นักพัฒนา (Developer License Portal) บนเว็บ สำหรับ SuperAdmin ปลดล็อกตลอดชีพ (+1 ปี, +14 วัน), ระงับสิทธิ์, และสร้าง Activation Key (`ACT-LFE-XXXX-XXXX` ด้วย HMAC-SHA256), 4) หน้าต่างเปิดใช้งานคีย์ (Activate License) ทั้งบน C# WPF POS และ Web | ผ่านการทดสอบ (Build & Publish OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 12)** (2026-10-07) | Real-Time POS Presence & View-Only Mode | 1) ระบบติดตามการเชื่อมต่อของเครื่องหน้าร้าน (Client PC Presence Tracker In-Memory Singleton) เชื่อมต่อ SignalR Event `StoreStatusChanged` อัตโนมัติ, 2) ป้องกันการสั่งอาหารเมื่อเครื่องหน้าร้านยังไม่เปิด: Server-Side Guard บล็อก `POST /api/orders` ด้วย `ERR_STORE_OFFLINE` และ Web ล็อกปุ่มสั่งอาหาร, 3) โหมดเลือกชมเมนูล่วงหน้า (View-Only Menu): ลูกค้าเปิดดูเมนูอาหาร รูปภาพ ราคา และท็อปปิ้งได้ตามปกติ 100%, 4) บัตรข้อมูลและช่องทางติดต่อร้าน (Store Contact Card): แสดงชื่อร้าน, เบอร์โทรติดต่อ (กดโทรออกได้ทันที), ที่อยู่ร้าน, เวลาทำการ, และแถบแจ้งเตือนสถานะเรียลไทม์, 5) ระบบสลับสถานะจำลอง (Live Pitch Switcher): สวิตช์จำลองสถานะร้านเปิด/ปิดสำหรับนำเสนองานขายแบบสด | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |


