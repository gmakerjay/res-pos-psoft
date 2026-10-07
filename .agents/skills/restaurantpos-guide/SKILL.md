---
name: restaurantpos-guide
description: >-
  Essential knowledge, architectural reference, strict rules, and deployment policies for RestaurantPOS (.NET 10 WPF Desktop POS + ASP.NET Core Linux Server + Vite React Web App). Use whenever the user asks to modify, develop, build, run, test, print, review, or deploy RestaurantPOS.
---

# RestaurantPOS Developer & Agent Guide

คู่มือสถาปัตยกรรม, กฎเหล็ก, โครงสร้างโฟลเดอร์ และแนวทางการพัฒนาโปรแกรม **RestaurantPOS System (Enterprise Edition)** สำหรับ AI Agent และนักพัฒนา เพื่อให้ทำงานไปในทิศทางเดียวกันเสมอ

---

## 1. กฎเหล็กประจำโครงการที่ต้องปฏิบัติตามอย่างเคร่งครัด (Strict Rules)

1. **ห้ามใช้อิโมจิในโปรแกรมโดยเด็ดขาด (Strictly ZERO Emojis)**:
   - ห้ามใส่อิโมจิใดๆ ในส่วนของ User Interface (UI), ปุ่มกด (Buttons), หัวข้อเมนู (Navigation), ข้อความแจ้งเตือน (Dialogs/Alerts), สลิปใบเสร็จ (Thermal Receipts), รายงาน (Reports) หรือข้อความในโค้ดที่แสดงผลต่อผู้ใช้งาน
   - ใช้ข้อความทางการและ Color Badges (เช่น `[ว่าง]`, `[มีลูกค้า]`, `[รอชำระเงิน]`, `[Online]`, `[Offline]`) แทน

2. **ระบบ Server Connection & IP Configuration (Client-Server แบบเซิร์ฟเวอร์เกม)**:
   - เครื่องแม่ข่าย **Linux Server** รัน ASP.NET Core Web API + SignalR ที่ Port `5000` ทำหน้าที่เป็นศูนย์กลางฐานข้อมูลและกระจายข้อมูล Real-time
   - เครื่องลูกข่าย **Windows Desktop POS** และ **Web App** สามารถตั้งค่า **Server IP Address** (เช่น `192.168.1.100:5000`, `10.0.0.5:5000`, หรือ `127.0.0.1:5000`) ได้อย่างอิสระ
   - มีปุ่ม **"ทดสอบการเชื่อมต่อ (Test Connection)"** เพื่อ Ping ไปยัง `/api/health` ทันที หากเชื่อมต่อได้จะขึ้นสีเขียวพร้อมบันทึกลง `pos_config.json` เปิดโปรแกรมใหม่ไม่ต้องตั้งค่าซ้ำ
   - หากโปรแกรมเปิดขึ้นมาแล้วไม่พบเซิร์ฟเวอร์ จะแสดงหน้าต่างตั้งค่า IP ขึ้นมาให้ผู้ใช้กรอกทันที

3. **ระบบ Logger & Error Handler ทุกจุด (Full-Stack Error Resilience)**:
   - **Central Server (ASP.NET Core):**
     - `GlobalExceptionHandlingMiddleware`: ดักจับทุก Unhandled Exception ในระบบ พร้อม Log Request Path, Method, Client IP, TraceId และแปลงเป็น JSON มาตรฐาน `ApiResponse.Fail`
     - Serilog Rolling Logs: บันทึกลง `logs/server-YYYYMMDD.log` หมุนเวียนรายวัน
     - Central Client Log Ingestion: รองรับ `POST /api/logs/client` เพื่อรับ Error และ Crash จากเครื่อง POS และ Web App มาบันทึกรวมที่เซิร์ฟเวอร์
   - **Windows Desktop POS (C# WPF):**
     - Global Exception Handlers ใน `App.xaml.cs`: ดักจับ `DispatcherUnhandledException`, `AppDomain.UnhandledException`, และ `TaskScheduler.UnobservedTaskException` ไม่ให้โปรแกรม Crash ดับกลางคัน
     - `PosLogger`: บันทึกลง `logs/pos-client-YYYYMMDD.log` และส่ง Error ขึ้น Linux Server อัตโนมัติในพื้นหลัง
     - `ErrorDialog.xaml`: หน้าต่างแจ้งเตือนข้อผิดพลาดสไตล์ Classic XP พร้อมปุ่ม "ดูรายละเอียด..." ขยาย StackTrace ทางเทคนิค
   - **Web App (Customer QR & Management):**
     - `ErrorBoundary.tsx`: ดักจับ React Component crash ป้องกันหน้าจอดำ/ขาว
     - `initGlobalErrorHandlers`: ดักจับ `window.onerror` และ `window.onunhandledrejection`
     - `logError`: ส่งบันทึกข้อผิดพลาดกลับมายังเซิร์ฟเวอร์

4. **การพิมพ์สลิปและใบสั่งครัว (Printing Policy)**:
   - การสั่งพิมพ์ทั้งหมดต้องผ่านเครื่องคอมพิวเตอร์ Windows PC หน้าร้านโดยตรง (Windows Spooler / ESC/POS)
   - ห้ามให้ Web App สั่งเครื่องพิมพ์โดยตรง

5. **กฎเหล็กการ Deploy (Strict Deployment Gate Policy)**:
   - **ห้ามทำการ Deploy อัตโนมัติหลังแก้โค้ดเด็ดขาด:** รันเฉพาะ Automated Tests หรือ Build เพื่อตรวจสอบความถูกต้อง ห้ามปล่อยตัวโปรแกรมขึ้น Production โดยพลการ ต้องหยุดการทำงานทันทีและสรุปผลเพื่อให้ผู้ใช้เป็นผู้ทดสอบและสั่ง Deploy เสมอ
   - เงื่อนไขเดียวที่จะ Deploy ได้คือ ผู้ใช้เป็นฝ่ายพิมพ์คำสั่งสั่งการอย่างชัดเจน เช่น *"Deploy"*, *"สั่ง Deploy ได้"*, *"บิลด์ส่งมอบ"*
   - **ห้ามสร้างไฟล์บีบอัด (.zip/.rar) เด็ดขาด:** ส่งมอบเฉพาะโฟลเดอร์ตัวเต็มที่พร้อมใช้งาน (Uncompressed Folders)

6. **การบันทึก Progress การทำงานอย่างกระชับ (Lean Progress Logging Policy)**:
   - ต้องบันทึกความคืบหน้าการทำงาน (Progress Log) ลงใน `README.md` ทุกครั้งที่มีการพัฒนาหรือปรับปรุงระบบ
   - **อย่าให้โปรเกรสบวม (Keep it Lean & Structured):** สรุปเป็นตารางกระชับ ระบุเฉพาะสาระสำคัญ วันที่/เวอร์ชัน โมดูล และผลการทดสอบ ไม่บันทึกเยิ่นเย้อหรือใส่โค้ดยาว เพื่อรักษาประสิทธิภาพและความกระชับของเอกสาร

7. **รูปแบบการระบุเวอร์ชันในเอกสาร (Versioning Policy)**:
   - ให้ระบุเป็น `เวอร์ชัน 1.0 (แก้ไขครั้งที่ ...)` โดยเริ่มที่เวอร์ชัน 1.0 เสมอ
   - ห้ามเปลี่ยนเลขเวอร์ชันหลัก (ห้ามขึ้นเป็น v1.1, v2.0 หรือ v1.9.0 เอง) จนกว่าผู้ใช้จะสั่งเปลี่ยนเด็ดขาด

---

## 2. โครงสร้างโฟลเดอร์และการแบ่งซอร์สโค้ด (Directory Architecture)

```text
res-pos_psoft/
├── web_server/                         # [ส่วนที่ 1] ซอสฝั่งเว็บและเซิร์ฟเวอร์กลาง (เตรียมไว้เผื่อ Deploy ขึ้น Linux)
│   ├── RestaurantPOS.Server/           # ASP.NET Core 10 Web API + SignalR Hubs + EF Core
│   ├── RestaurantPOS.Web/              # Vite + React 19 + TypeScript (Customer QR & Management)
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10 DTOs, Enums, Models)
│   ├── deploy_linux/                   # เครื่องมือและคอนฟิกสำหรับ Deploy บน Linux
│   │   ├── restaurantpos.service       # Systemd background service unit
│   │   ├── nginx.conf                  # Nginx reverse proxy configuration (WebSocket support)
│   │   ├── Dockerfile                  # Multi-stage Docker build
│   │   ├── docker-compose.yml          # Docker Compose specification
│   │   ├── deploy_linux.sh             # Linux automated setup script
│   │   └── DEPLOY_LINUX.md             # คู่มือการติดตั้งบน Linux (Ubuntu/Debian/Rocky)
│   ├── RestaurantPOS.WebServer.slnx    # Solution แยกสำหรับงาน Web & Server
│   ├── run_server.bat                  # รัน Central Server บน Windows
│   ├── run_web.bat                     # รัน Vite Web Dev Server
│   ├── run_all.bat                     # รันทั้ง Server และ Web พร้อมกัน
│   ├── run_server.sh                   # รัน Central Server บน Linux
│   ├── build_web_server.bat            # บิลด์ Web SPA และ Publish Server ไปยัง build_output
│   └── README.md                       # เอกสารแนะนำฝั่ง Web & Server
│
├── client_pc/                          # [ส่วนที่ 2] ซอสฝั่ง Client PC Software (เครื่องแคชเชียร์หน้าร้าน)
│   ├── RestaurantPOS.Wpf/              # C# .NET 10 WPF Desktop POS (Classic Windows XP Theme)
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10)
│   ├── RestaurantPOS.Client.slnx       # Solution แยกสำหรับงาน Windows Desktop POS
│   ├── run_pos.bat                     # รันหรือบิลด์โปรแกรม Desktop POS ทันที
│   ├── build_client.bat                # คอมไพล์โหมด Release และส่งออกไปยัง build_output
│   └── README.md                       # เอกสารแนะนำฝั่ง Client PC
│
├── build_output/                       # [ส่วนที่ 3] โฟลเดอร์โปรแกรมที่บิลด์ออกมาแล้ว (รอคำสั่ง Deploy)
│   ├── client_pc/                      # ไฟล์บิลด์ตัวเต็มของ Windows Desktop POS (มี RestaurantPOS.Wpf.exe)
│   │   ├── RestaurantPOS.Wpf.exe
│   │   ├── pos_config.json
│   │   └── run_pos.bat
│   ├── web_server/                     # ไฟล์บิลด์ตัวเต็มของ Central Server + Web SPA ใน wwwroot
│   │   ├── RestaurantPOS.Server.dll
│   │   ├── appsettings.json
│   │   ├── wwwroot/                    # ไฟล์ที่บิลด์แล้วของ Web SPA
│   │   ├── run_server.bat
│   │   └── run_server.sh
│   └── README.md                       # รายละเอียดไฟล์บิลด์และนโยบายการส่งมอบ
│
├── RestaurantPOS.slnx                  # Master Solution File รวมทุกโปรเจกต์
├── run.bat                             # Master Quick Launcher เมนูลัดสำหรับรันทั้งระบบ
└── AGENTS.md                           # กฎและระเบียบสถาปัตยกรรมสำหรับ AI Agent
```

---

## 3. ขั้นตอนการรันและการบิลด์ (Developer Workflows)

### 3.1 การรันในโหมดพัฒนา (Development)
- **รันทั้งหมด:** ดับเบิลคลิก `run.bat` ที่โฟลเดอร์หลัก แล้วเลือกตัวเลือก `[1]`
- **รันเฉพาะ Web & Server:** เข้าไปที่ `web_server/run_all.bat` หรือ `run_server.bat` / `run_web.bat`
- **รันเฉพาะ Windows POS Client:** เข้าไปที่ `client_pc/run_pos.bat` หรือเปิด `RestaurantPOS.Client.slnx` ใน Visual Studio

### 3.2 การบิลด์ผลลัพธ์ (Building Output)
- บิลด์ฝั่ง Server + Web: รัน `web_server/build_web_server.bat` ผลลัพธ์จะไปอยู่ที่ `build_output/web_server/`
- บิลด์ฝั่ง Client POS: รัน `client_pc/build_client.bat` ผลลัพธ์จะไปอยู่ที่ `build_output/client_pc/`
- บิลด์ทั้งหมด: รันตัวเลือก `[5]` ใน `run.bat` หรือ `dotnet build RestaurantPOS.slnx`

---

## 4. ข้อพึงระวังและข้อปฏิบัติ (Best Practices)
1. เมื่อมีการปรับปรุง DTO หรือ Model ใน `RestaurantPOS.Shared` ต้องอัปเดตทั้งใน `web_server/RestaurantPOS.Shared` และ `client_pc/RestaurantPOS.Shared` ให้สอดคล้องกันเสมอ
2. การปรับปรุง UI ของ Windows POS ต้องรักษาสไตล์ **Classic Windows XP Theme** โดยใช้ฟอนต์ Tahoma / Segoe UI, ขอบนูน 3D, Gradient สีน้ำเงินคลาสสิก และสีสถานะแบบ Color Badges
3. ห้ามใช้ TailwindCSS ใน Web App ตามกฎระบบ ให้ใช้ CSS มาตรฐานที่จัดระเบียบไว้อย่างสวยงาม
4. ทดสอบ SignalR Reconnection ให้คงความทนทานต่อการตัดการเชื่อมต่อชั่วคราวเสมอ
5. บัญชีผู้ใช้งานเริ่มต้น: `admin` / `psoft123` (SuperAdmin) และ `cashier` / `psoft123` (Cashier) รหัสผ่านเก็บแบบ SHA256 Salted Hash และผ่านการยืนยันตัวตนด้วย JWT Token
6. **ระบบการสั่งอาหารเข้าโต๊ะ (Multi-Round Table Ordering):** ห้ามเขียนทับออเดอร์เดิมของโต๊ะเด็ดขาด การสั่งอาหารเพิ่มต้องสร้างเป็นออเดอร์รอบใหม่ที่อ้างอิง `TableNumber` เดียวกัน และรองรับการปิดบิลรวมยอดผ่าน `POST /api/orders/table/{tableRef}/pay`
7. **ระบบ Real-Time Synchronization (SignalR):** เมื่อมีการเปลี่ยนแปลงสถานะออเดอร์ หรือสร้างออเดอร์ใหม่ เซิร์ฟเวอร์ต้องบรอดแคสต์ `OrderCreated`, `OrderStatusChanged`, `TableStatusChanged`, หรือ `BillClosed` ให้ทุกเครื่องลูกข่าย (ทั้ง Web และ PC POS) อัปเดตข้อมูลสดเสมอ
8. **การบันทึก Progress:** สรุปความก้าวหน้าลงใน `README.md` เป็นตารางกระชับทุกครั้ง อย่าปล่อยให้ข้อมูลบวมเกินจำเป็น
