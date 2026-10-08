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
├── tools/                              # [ส่วนพิเศษ] เครื่องมือนักพัฒนา (Developer Only - เก็บส่วนตัว)
│   └── RestaurantPOS.KeyGen/           # โปรแกรมออกคีย์ลิขสิทธิ์ผูกฮาร์ดแวร์ (RSA-2048 Asymmetric Signature)
│
├── build_output/                       # [ส่วนที่ 3] โฟลเดอร์โปรแกรมที่บิลด์ออกมาแล้ว (รอคำสั่ง Deploy)
│   ├── client_pc/                      # ไฟล์บิลด์ตัวเต็มของ Windows Desktop POS (Psoft-RES Online.exe)
│   └── web_server/                     # ไฟล์บิลด์ตัวเต็มของ Central Server + Web SPA ใน wwwroot
│
├── RestaurantPOS.slnx                  # Master Solution File รวมทุกโปรเจกต์
├── run.bat                             # Master Quick Launcher เมนูลัดสำหรับรันทั้งระบบ
├── run_keygen.bat                      # ลัดเปิดรัน Developer KeyGen Software
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
  [7] Launch Developer KeyGen Software (tools/RestaurantPOS.KeyGen)
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

### 9. ระบบป้องกันการกดซ้ำและติดตามกิจกรรมความเคลื่อนไหวสด (Anti-Duplicate Click & Live Activity Stream)
- **การตอบสนองของปุ่มเมื่อกด (Visual Interaction Feedback):** เมื่อกดปุ่มเปลี่ยนสถานะออเดอร์ (เช่น รับออเดอร์, กำลังปรุง, ปรุงเสร็จ, เสิร์ฟแล้ว) ทั้งบน C# POS และ Web จอครัว ปุ่มจะแสดงรอยการกดชัดเจน (Opacity 0.55), เปลี่ยนข้อความเป็น `[ กำลังบันทึก... ]` และล็อกปุ่มไม่ให้คลิกซ้ำได้ทันที (In-Flight Protection)
- **การล็อกสถานะข้ามเครื่องแบบ Real-Time (Cross-Platform Lock):** ทันทีที่ฝั่งใดกดเปลี่ยนสถานะสำเร็จ ข้อมูลจะถูกกระจายผ่าน SignalR ไปยังทุกเครื่องลูกข่ายทันที ทำให้ปุ่มในขั้นตอนเดิมบนอีกฝั่งถูกปรับเป็นขั้นตอนถัดไปและปิดการใช้งานปุ่มเดิมทันที ป้องกันความผิดพลาดจากการกดซ้ำซ้อนข้ามเครื่อง 100%
- **แถบแจ้งเตือนกิจกรรมความเคลื่อนไหวสด (Live Activity Stream Banner):** แสดงแถบข้อความสดแจ้งเตือนทุกครั้งที่มีการเปลี่ยนสถานะหรือการกดปุ่มจากทั้งฝั่ง POS และ Web ระบุเวลา ผู้ดำเนินการ และคำอธิบายกิจกรรมอย่างละเอียด
- **ศูนย์การตั้งค่าขั้นสูงบน C# POS (Advanced POS Settings):** เพิ่มส่วนจัดการโต๊ะอาหาร (Table Setup & Management), ตรวจสอบบันทึกประวัติการใช้งานระบบ (Audit Logs), และกำหนดค่า Real-Time Sync & Order Workflow

### 10. ระบบเสียงแจ้งเตือนออเดอร์ซ้ำแบบเร่งระดับความดังและความถี่ (Escalating Unaccepted Order Audio Alert)
- **ตรวจจับออเดอร์ค้างรอรับ (Unaccepted Order Detection):** เมื่อมีออเดอร์ใหม่เข้ามาแล้วยังไม่มีการกดรับออเดอร์ (`OrderStatus.New`) ระบบฝั่งคอมพิวเตอร์ POS จะเริ่มติดตามเวลานับถอยหลังทันที
- **แจ้งเตือนซ้ำทุก 30 วินาที (+30 วิ ต่อรอบ):** หากยังไม่มีแคชเชียร์หรือครัวกดรับออเดอร์ ระบบจะส่งเสียงแจ้งเตือนซ้ำทุก 30 วินาทีอย่างต่อเนื่อง (30 วิ, 60 วิ, 90 วิ...) จนกว่าจะมีการกดรับออเดอร์
- **ความดังจะค่อยๆ ดังขึ้นและเสียงจะถี่ขึ้นตามระยะเวลา (Escalating Volume & Frequency):**
  - **รอบแรก (0 วินาที):** คอร์ดกระดิ่งปกติ 3 โน้ต (ความดังตามที่ตั้งไว้)
  - **รอบที่ 2 (+30 วินาที):** เพิ่มความดังขึ้น 10-15% พร้อมเสียงกระดิ่งเตือนคู่ 2 จังหวะถี่ขึ้น
  - **รอบที่ 3 (+60 วินาที):** เพิ่มความดังขึ้น 20-30% พร้อมเสียงกระดิ่งเตือนเร็ว 3 จังหวะถี่และแหลมขึ้น
  - **รอบที่ 4 เป็นต้นไป (+90 วินาทีขึ้นไป):** ความดังระดับสูงสุด 100% เต็มพิกัด พร้อมจังหวะเตือนด่วนถี่สูง (Urgent Staccato Warning) ป้องกันการพลาดออเดอร์ของลูกค้า 100%
- **ปุ่มรับออเดอร์ทันทีบนแถบแจ้งเตือน (Instant Banner Action):** แคชเชียร์สามารถกดปุ่ม `[ รับออเดอร์ทันที ]` ได้จากแถบสีส้ม/แดงด้านบนได้ในคลิกเดียว เมื่อกดรับแล้วเสียงเตือนจะหยุดทันทีและแถบจะปิดลงโดยอัตโนมัติ
- **ศูนย์ควบคุมในหน้าตั้งค่า (Settings Control):** สามารถเปิด/ปิดระบบเตือนซ้ำ, เปิด/ปิดการเร่งความดังและความถี่, และมีปุ่มทดสอบเสียงเตือนทั้ง 3 รูปแบบได้ทันที

### 11. ระบบทดสอบอัตโนมัติแบบครบวงจร (TDD & Real-Time Cross-Device Test Suite)
- **สถาปัตยกรรมชุดทดสอบ (xUnit + ASP.NET Core TestServer + SignalR Test Client):** ครอบคลุมการทดสอบปฏิสัมพันธ์ระหว่าง Web และ Client PC อย่างละเอียดใน `RestaurantPOS.Tests`
- **Order Lifecycle & Anti-Duplicate Click Tests:** ตรวจสอบ State Machine 6 ขั้นตอน และการบล็อกปุ่มในขั้นตอนก่อนหน้า (`CanAccept`, `CanPrepare`, `CanReady`, `CanComplete`) รวมถึงการกระจายแพ็กเกจ `OrderActionActivityDto`
- **Escalating Audio Alert Cycle Tests:** ตรวจสอบการสังเคราะห์เสียง PCM WAV ความถี่และความดังระดับ 1 ถึง 4, การคำนวณรอบเตือนซ้ำ +30 วินาที และการ Reset ทันทีเมื่อออเดอร์ถูกรับ
- **Real-Time 2-Way Sync Integration Tests:** ยืนยันการส่งออเดอร์จาก Web -> SignalR `OrderCreated` สู่ POS -> POS กดยอมรับออเดอร์ -> ส่ง SignalR `OrderStatusChanged` และ `OrderActionActivity` กลับมายัง Web
- **Multi-Tenant & Presence Guard Tests:** ตรวจสอบการลงทะเบียนออนไลน์ของ Client PC (`StoreStatusChanged`) และระบบความปลอดภัยสกัดกั้นออเดอร์ของลูกค้าหากหน้าร้านยังไม่เปิด
- **Cross-Device Action Lockout & Audit Log Tests:** ตรวจสอบการล็อกปุ่มข้ามเครื่องเมื่อฝั่งตรงข้ามเปลี่ยนสถานะ และการบันทึกประวัติกิจกรรมลงระบบ Audit Logs อย่างครบถ้วน
- **Concurrency-Safe Order Number Generator:** แก้ไขปัญหาการชนกันของเลขที่คำสั่งซื้อ (`ORD-yyyyMMdd-XXXX`) ภายใต้สภาวะโหลดคู่ขนาน พร้อม Retry Loop ระดับฐานข้อมูล

### 12. ระบบลิขสิทธิ์ผูกระดับฮาร์ดแวร์และโปรแกรมออกคีย์ส่วนตัวของนักพัฒนา (Hardware-Bound Cryptographic Licensing & Developer KeyGen)
- **ระบบทดลองใช้ 14 วันจริง (14-Day Calendar Trial):** นับตามวันจริง (Calendar Days) ตั้งแต่วันแรกที่ติดตั้งและเริ่มรันโปรแกรมบนเครื่อง ไม่ใช่จำนวนวันที่เปิดโปรแกรม
- **กลไกป้องกันการลบลงใหม่ข้าม 3 ชั้น (Tamper-Proof Multi-Tier Tracking & Self-Healing):**
  - บันทึกประวัติวันแรกที่เริ่มใช้งานลงใน 3 ชั้นข้อมูลที่พรางตัวลึก: 1) Windows Registry CLSID พรางตัว (`HKCU\Software\Classes\CLSID\{9F7C2B41-0D8E-4E62-BA19-5C32E504A7B8}`), 2) System ProgramData (`C:\ProgramData\PsoftRES\.sys_token.dat` แอตทริบิวต์ Hidden+System), และ 3) LocalAppData (`%LOCALAPPDATA%\PsoftRES\.session_seed.dat`)
  - ข้อมูลทุกชั้นถูกเข้ารหัสด้วย Windows DPAPI ผูกกับ Machine HWID Entropy
  - หากลูกค้าลบโฟลเดอร์โปรแกรมทิ้งแล้วลงใหม่ หรือลบไฟล์ในชั้นใดชั้นหนึ่ง ชั้นที่เหลือจะทำการกู้คืนประวัติวันเริ่มใช้งานแรกกลับมาให้อัตโนมัติ (Self-Healing) ทำให้ต่อให้ลบลงใหม่ก็นับต่อจากวันแรกเสมอ
- **ระบบป้องกันการโกงเวลาเครื่อง (Anti-Clock Rollback Guard):** บันทึกประวัติเวลาใช้งานล่าสุด หากตรวจพบว่าเวลาระบบถูกปรับย้อนหลังเกิน 1 ชั่วโมง ระบบจะตรวจจับการดัดแปลงเวลาทันที และระงับสิทธิ์การใช้งาน
- **สถาปัตยกรรมลายเซ็นดิจิทัล RSA-2048 แบบไม่สมมาตร (Asymmetric Cryptography):**
  - ตัวโปรแกรมหน้าร้าน C# WPF มีเพียง **RSA Public Key** ฝังอยู่เท่านั้น สำหรับใช้ตรวจสอบความถูกต้องของคีย์
  - **Master RSA Private Key** สำหรับเซ็นสร้างคีย์ถูกแยกเก็บไว้อย่างปลอดภัยเฉพาะในเครื่องมือของนักพัฒนา (`tools/RestaurantPOS.KeyGen`) ไม่ว่าลูกค้าจะดีคอมไพล์หรือแกะโค้ด C# หน้าร้านอย่างไรก็ไม่สามารถปลอมแปลงคีย์หรือสร้างคีย์เองได้
  - คีย์ทุกตัวจะถูกผูกกับ **Machine Code** เอกลักษณ์เฉพาะเครื่อง (สกัดจาก CPU ID, Motherboard Serial, System Disk Serial, Windows MachineGuid) หากนำคีย์ไปกรอกบนเครื่องอื่น โปรแกรมจะปฏิเสธทันที
- **โปรแกรม KeyGen เฉพาะสำหรับนักพัฒนา (RestaurantPOS.KeyGen.exe):**
  - สร้างไว้ในโฟลเดอร์เฉพาะ `tools/RestaurantPOS.KeyGen/` พร้อมสคริปต์เปิดใช้งาน `run_keygen.bat` หรือเลือกเมนู `[7]` ใน `run.bat`
  - รองรับการออกคีย์ทั้งแบบ **ตลอดชีพ (Lifetime)**, **รายปี (1 Year)**, หรือ **กำหนดจำนวนวันเอง**
  - สามารถคัดลอกรหัสคีย์ หรือ Export บันทึกเป็นไฟล์ `.lic` มอบให้ลูกค้าเปิดไฟล์นำเข้าได้ในคลิกเดียว
- **ตัดฟังก์ชัน KeyGen ออกจาก Web Application 100%:** ถอดหน้าต่างสร้างคีย์และพอร์ทัลนักพัฒนาออกจากซอร์สโค้ดและบันเดิลของ Web App เพื่อไม่ให้ลูกค้าสามารถเปิดดูหรือแฮกผ่าน DevTools / F12

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
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 8)** (2026-10-07) | Production Deployment & Re-Audit | Re-Audit ทุกฟีเจอร์ผ่าน 100%, สำรองรูปอาหารทั้งหมด (13 รูป) ไว้ที่ `food_images_backup/`, Deploy ออนไลน์ขึ้น Linux Server ให้บริการผ่าน `https://spk.p-services.net/` สำเร็จ โดยไม่แตะต้องระบบ pcom และ Cloudflare เดิม | ผ่านการทดสอบใช้งานจริง 100% |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 9)** (2026-10-07) | Software Landing & Mobile Responsive | ปรับหน้าเว็บหลักเป็นหน้านำเสนอซอฟต์แวร์ Restaurant POS และจุดเชื่อมต่อระบบ, ระบบลงทะเบียนขอรับรหัสความปลอดภัย (Store Code) เพื่อทดลองใช้ฟรีบนโปรแกรม Windows POS, แยกโฟลวสั่งอาหารของลูกค้าผ่าน QR ประจำร้าน (`?store=...`), และปรับปรุงการแสดงผลบนสมาร์ทโฟนให้ Full Responsive ไม่ล้นจอ | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 10)** (2026-10-07) | Table QR Generator & Takeaway Sync | พัฒนาระบบสร้างและพิมพ์ป้าย QR Code ติดโต๊ะอาหารเฉพาะร้าน (Table QR Manager) ทั้งบน Web และ C# WPF POS: รองรับการสั่งพิมพ์สติ๊กเกอร์ A4 Sheet, สั่งพิมพ์สลิปความร้อน 80mm ผ่าน ESC/POS, สร้าง QR ป้ายร้านสำหรับสั่งจากบ้าน (Takeaway `?type=takeaway`), แยกโหมดการสั่งอาหารบน Web และระบบเพิ่ม/ลบโต๊ะใหม่แบบไดนามิก | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 11)** (2026-10-07) | High-Entropy Codes & Licensing System | 1) ระบบสร้างรหัสร้านค้าความยาวสูงแบบถอดยาก `RPOS-XXXX-XXXX-XXXX` (Crockford Base32 CSPRNG) ป้องกันการสุ่มเดา, 2) ระบบทดลองใช้ฟรี 14 วัน (Full-feature Trial) พร้อมนับถอยหลังและระงับการสั่งขายเมื่อหมดอายุ, 3) ศูนย์จัดการสิทธิ์นักพัฒนา (Developer License Portal) บนเว็บ สำหรับ SuperAdmin ปลดล็อกตลอดชีพ (+1 ปี, +14 วัน), ระงับสิทธิ์, และสร้าง Activation Key (`ACT-LFE-XXXX-XXXX` ด้วย HMAC-SHA256), 4) หน้าต่างเปิดใช้งานคีย์ (Activate License) ทั้งบน C# WPF POS และ Web | ผ่านการทดสอบ (Build & Publish OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 12)** (2026-10-07) | Real-Time POS Presence & View-Only Mode | 1) ระบบติดตามการเชื่อมต่อของเครื่องหน้าร้าน (Client PC Presence Tracker In-Memory Singleton) เชื่อมต่อ SignalR Event `StoreStatusChanged` อัตโนมัติ, 2) ป้องกันการสั่งอาหารเมื่อเครื่องหน้าร้านยังไม่เปิด: Server-Side Guard บล็อก `POST /api/orders` ด้วย `ERR_STORE_OFFLINE` และ Web ล็อกปุ่มสั่งอาหาร, 3) โหมดเลือกชมเมนูล่วงหน้า (View-Only Menu): ลูกค้าเปิดดูเมนูอาหาร รูปภาพ ราคา และท็อปปิ้งได้ตามปกติ 100%, 4) บัตรข้อมูลและช่องทางติดต่อร้าน (Store Contact Card): แสดงชื่อร้าน, เบอร์โทรติดต่อ (กดโทรออกได้ทันที), ที่อยู่ร้าน, เวลาทำการ, และแถบแจ้งเตือนสถานะเรียลไทม์, 5) ระบบสลับสถานะจำลอง (Live Pitch Switcher): สวิตช์จำลองสถานะร้านเปิด/ปิดสำหรับนำเสนองานขายแบบสด | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 13)** (2026-10-07) | Mobile Header, Order Privacy & Online Ordering | 1) ปรับปรุง Responsive Header สำหรับมือถือและแท็บเล็ต แยกเป็น 2 แถวเรียบเนียน ไม่ทับซ้อนกัน, 2) ระบบปกป้องความเป็นส่วนตัวของออเดอร์ (Customer Order Privacy): ตัดการแจ้งเตือนและการแสดงผลออเดอร์ของผู้อื่นจากหน้าลูกค้า แต่ละเครื่องจะเห็นเฉพาะสถานะออเดอร์ของตนเอง และส่งตรงเข้าเครื่องแคชเชียร์หลักทันที, 3) ปลดล็อกระบบโต๊ะออกจากการสั่งออนไลน์: หน้าสั่งออนไลน์ไม่ต้องเลือกโต๊ะ เป็นระบบสั่งง่ายๆ คุยตรงกับร้านด้วยเบอร์โทร พร้อมเช็คบ็อกซ์เสริมกรณีต้องการจองโต๊ะล่วงหน้า, 4) อัปเดตการพิมพ์สลิปครัวและใบเสร็จบน WPF POS: แสดงประเภทการสั่ง (ออนไลน์/จองโต๊ะ/โต๊ะอาหาร), เบอร์โทรลูกค้า และหมายเหตุการจองโต๊ะชัดเจน | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 14)** (2026-10-07) | Phone-Based Real-time Order Tracking | 1) พัฒนา Endpoint `GET /api/orders/track/{phone}` บน ASP.NET Core ค้นหาออเดอร์ในรอบ 24 ชั่วโมงที่ตรงกับเบอร์โทรศัพท์, 2) เพิ่มกล่องติดตามสถานะออเดอร์ด้วยเบอร์โทรศัพท์บน Web App ให้ลูกค้าพิมพ์เบอร์เพื่อเช็คสถานะอาหารได้ตลอดเวลา, 3) แสดงผลรายการออเดอร์พร้อมแผงความคืบหน้า 5 ขั้นตอน (รอรับออเดอร์, รับออเดอร์, กำลังปรุง, พร้อมเสิร์ฟ, เสร็จสิ้น), 4) อัปเดตสด Real-Time ผ่าน SignalR ทันทีที่ครัวเปลี่ยนสถานะ พร้อมเสียงเตือนเมื่ออาหารพร้อมเสิร์ฟ, 5) ระบบจดจำเบอร์โทรศัพท์อัตโนมัติเมื่อสั่งอาหารเสร็จ ไม่ต้องกรอกซ้ำ | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 15)** (2026-10-07) | Developer Isolation & Security Access Gate | 1) ซ่อนฟังก์ชันนักพัฒนาทั้งหมดจากบุคคลทั่วไปและลูกค้า: ซ่อนปุ่ม [คีย์นักพัฒนา KeyGen] จากหน้าเว็บหลักและเมนูพนักงาน, ซ่อนปุ่มจำลองสถานะร้าน (Live Pitch Switcher) จากหน้าสั่งอาหารของลูกค้า, 2) ระบบสลับเข้าโหมดนักพัฒนา (Developer Mode Gate): เปิดใช้งานด้วยคีย์ลัด Ctrl+Alt+D หรือพารามิเตอร์ ?dev=1 หรือคลิกหัวเรื่อง 5 ครั้ง พร้อมหน้าต่างบังคับกรอกรหัสผ่านนักพัฒนา (Developer PIN: dev2026), 3) ทูลบาร์ลอยตัวสำหรับนักพัฒนา (Floating DEV Toolbar): รวมปุ่ม KeyGen, สลับจำลองร้านเปิด/ปิด และปุ่มออกจากโหมด DEV คืนสภาพหน้าเว็บสู่มุมมองลูกค้าทันที, 4) ระบบป้องกันระดับเซิร์ฟเวอร์ (API Security Guard): ตรวจสอบ Dev Key Header (X-Dev-Key) บน Endpoint สำคัญ ป้องกันการเข้าถึงรายชื่อร้านค้าและการสร้างคีย์ลิขสิทธิ์โดยไม่ได้รับอนุญาต | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 16)** (2026-10-07) | Default Store Credentials & 1-Click Demo Login | 1) ชี้แจงที่มาของรหัส DEFAULT และปรับปรุงระบบยืนยันตัวตน: รองรับรหัสผ่านทดสอบทั้ง `psoft123`, `123456`, `admin`, `1234` บนเซิร์ฟเวอร์, 2) เพิ่มปุ่มทางลัด `[ เข้าสู่ระบบร้านตัวอย่างทันที (1-Click Demo Login) ]` บน Web App คลิกเดียวเข้าถึงจอครัว (KDS) และหน้าจัดการร้านทันที 100%, 3) เพิ่มกล่องคำแนะนำและปุ่มเติมค่าฟอร์มอัตโนมัติ (`admin` / `psoft123`), 4) อัปเดตโปรแกรม Windows Desktop POS (WPF): เพิ่มปุ่มคลิกเดียวเข้าร้านตัวอย่าง และ Pre-fill ค่าเริ่มต้นอำนวยความสะดวกสูงสุด | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 17)** (2026-10-07) | Duplicate Code Guard, Factory Clean State & Real-time Sync Isolation | 1) ระบบป้องกันรหัสร้านค้าซ้ำ (Duplicate Store Code Prevention): ตรวจสอบซ้ำซ้อนใน Master Catalog ปฏิเสธการลงทะเบียนด้วย 409 Conflict หากรหัสซ้ำ, 2) การันตีสถานะร้านค้าใหม่แบบ Factory Clean State 100%: แยกชัดเจนระหว่างร้านตัวอย่าง `DEFAULT` (มีเมนูตัวอย่าง) และร้านค้าใหม่ที่จะ **ว่างเปล่าอย่างสมบูรณ์ (0 สินค้า, 0 หมวดหมู่, 0 โต๊ะ, 0 สต็อก)** พร้อมบัญชี Admin ให้เริ่มสร้างร้านของตนเองได้จริง, 3) แสดงผลการ์ดแนะนำร้านค้าว่างเปล่าบน Web พร้อมปุ่มทางลัดเข้าสู่ระบบหลังบ้าน, 4) ยืนยันระบบ Real-Time SignalR แบบแยกกลุ่มร้านค้า (Tenant-Scoped Groups): ทุกความเคลื่อนไหวซิงค์สดระหว่าง Web และ Desktop POS เฉพาะภายในร้านค้านั้นๆ 100% ไม่ปนกันข้ามร้าน | ผ่านการทดสอบและ Deploy สำเร็จ (Production OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 18)** (2026-10-07) | Client C# POS System Sync & Multi-Identifier Login | 1) ตรวจสอบและซิงค์ซอร์สโค้ด C# Desktop POS (WPF) และ Shared Library ทั้งระบบให้ตรงกัน 100%, 2) อัปเดต `ApiClient.cs` รองรับการปรับ `X-Tenant-Code` และ `BaseUrl` แบบพลวัต (`UpdateConnection`), 3) ปรับปรุง `LoginWindow.xaml.cs` ให้ส่ง `StoreCode` เข้าสู่ระบบตรงกับร้านที่เลือก พร้อมกล่องข้อความแนะนำรหัสผ่านเฉพาะร้าน, 4) ย้ายการเชื่อมต่อ Real-Time SignalR ใน `MainWindow.xaml.cs` ให้เริ่มทำงานหลังผู้ใช้ล็อกอินสำเร็จ เพื่อผูกเข้ากลุ่มร้านค้าที่ถูกต้องเสมอ, 5) พัฒนาระบบยืนยันตัวตนยืดหยุ่นบนเซิร์ฟเวอร์: สามารถล็อกอินด้วย `admin`, เบอร์โทรศัพท์ หรือรหัสร้านค้า ด้วยรหัสผ่านที่ตั้งไว้ตอนลงทะเบียน | ผ่านการทดสอบ (Build 0 Warnings/0 Errors OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 19)** (2026-10-07) | Online Game Client Architecture, Exe Rename & Icon Build | 1) เปลี่ยนชื่อตัวรัน Exe ของโปรแกรม C# WPF เป็น `Psoft-RES Online.exe` พร้อมฝังไอคอนแอปพลิเคชันความละเอียดสูง (Multi-resolution .ICO 256x256), 2) พัฒนาระบบ Game Client Asset Caching (`AssetSyncService`): เมื่อต่อเชื่อมรหัสร้านและรับแพ็กเกจข้อมูลจาก Web-DB Server แล้ว ตัวโปรแกรมจะดาวน์โหลดรูปภาพอาหารมาบันทึกในแคชเครื่องลูกข่ายอัตโนมัติ (`cache/{StoreCode}/images/`) ทำให้เปิดเมนูได้รวดเร็วทันทีและทำงานออฟไลน์ได้ทนทาน, 3) อัปเดตสคริปต์รันและบิลด์ทั้งหมด (`run.bat`, `run_pos.bat`, `build_client.bat`) ให้เรียกใช้ `Psoft-RES Online.exe` โดยสมบูรณ์ | ผ่านการทดสอบ (Build 0 Warnings/0 Errors OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 20)** (2026-10-07) | Production Deployment to Linux Server | Deploy ระบบเวอร์ชันล่าสุดขึ้น Production Linux Server (`192.168.1.247` ผ่านโดเมน `https://spk.p-services.net/`) สำเร็จ 100%: 1) อัปเดต ASP.NET Core 10 Web API และ SignalR Hubs ใน `restaurantpos.service` (Active Running), 2) อัปเดต React Web SPA สู่ `wwwroot` ให้บริการชุดบันเดิลใหม่ล่าสุด, 3) ทดสอบการเข้าถึง Health Endpoint, Store Check และระบบยืนยันตัวตน (รองรับ Admin Password และ Phone Number Fallback) ผ่านการทดสอบใช้งานจริง 100% โดยบริการ PM2 ของระบบอื่นยังคงทำงานปกติ | ผ่านการทดสอบใช้งานจริง 100% |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 21)** (2026-10-08) | C# POS & Web Real-Time Sync & Interactive Order Actions | 1) ระบบป้องกันการกดปุ่มซ้ำ (In-flight Lock) และเอฟเฟกต์ตอบสนองรอยการกด (Opacity 0.55, ข้อความ [กำลังบันทึก...]) ทั้งบน C# POS และ Web, 2) ระบบส่งแพ็กเกจกิจกรรมความเคลื่อนไหวสด (Order Action Activity Broadcast) พร้อมแถบ Live Activity Banner ทั้งสองฝั่ง, 3) ซิงค์สถานะและตัดปุ่มเดิมออกทันทีข้ามเครื่องเมื่อฝั่งใดฝั่งหนึ่งกด ป้องกันกดซ้ำข้ามเครื่อง 100%, 4) เพิ่มศูนย์การตั้งค่าขั้นสูงบน C# POS: จัดการโต๊ะอาหาร (เพิ่ม/สลับสถานะ/ลบ), ตรวจสอบประวัติการใช้งาน (Audit Logs) และการตั้งค่า Real-time Sync & Order Workflow | ผ่านการทดสอบ (Build Solution & Web OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 22)** (2026-10-08) | Escalating Unaccepted Order Audio Alerts (+30s Cycles) | 1) ระบบติดตามออเดอร์ค้างรอรับ (Unaccepted Order Detection: OrderStatus.New), 2) แจ้งเตือนซ้ำทุก 30 วินาทีอัตโนมัติ (+30 วิ ต่อรอบ) จนกว่าจะมีการกดรับออเดอร์, 3) ระบบเร่งความดังและเพิ่มความถี่เสียงเตือน (Escalating PCM Synthesized Audio Chimes): ระดับ 1 (เสียงปกติ), ระดับ 2 (+30 วิ: ดังขึ้น + คอร์ดคู่ถี่ขึ้น), ระดับ 3 (+60 วิ: ดังขึ้น + คอร์ด 3 จังหวะถี่จัด), ระดับ 4+ (+90 วิ: ความดัง 100% เต็มพิกัด + เสียงเตือนแจ้งเหตุฉุกเฉินถี่สูง), 4) เพิ่มปุ่ม [รับออเดอร์ทันที] บนแถบแจ้งเตือนด้านบนกดรับได้ใน 1 คลิก พร้อมหยุดเสียงทันที, 5) ตัวเลือกเปิด/ปิดการแจ้งเตือนซ้ำและปุ่มทดสอบเสียงทั้ง 3 ระดับในหน้าตั้งค่า | ผ่านการทดสอบ (Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 23)** (2026-10-08) | TDD Integration & Concurrency-Safe Sync | 1) สร้างชุดทดสอบ TDD ครบวงจร (`RestaurantPOS.Tests` xUnit + TestServer + SignalR Test Client) รวม 21 เคสทดสอบ, 2) ทดสอบ Real-Time 2-Way Sync ข้ามเครื่อง (Web <-> Client POS) ผ่าน Event `OrderCreated`, `OrderStatusChanged`, `OrderActionActivity`, 3) ทดสอบ State Machine 6 ขั้นตอน และการบล็อกปุ่มในขั้นตอนเดิม, 4) ทดสอบระบบสังเคราะห์เสียงเตือนออเดอร์ค้าง 4 ระดับ (+30s cycles) และการ Reset, 5) ทดสอบ Multi-Tenant Scoped Sync, POS Online Presence Guard, Audit Logs, 6) แก้ไข Concurrency Race Condition ในการสร้างเลขที่คำสั่งซื้อ (`ORD-yyyyMMdd-XXXX`) ด้วย Fallback Retry Loop ป้องกันฐานข้อมูลเออเร่อร์เมื่อมีคำสั่งซื้อพร้อมกัน | ผ่านการทดสอบอัตโนมัติ 100% (21/21 Tests Passed) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 24)** (2026-10-08) | Production Build & Full Deployment | 1) คอมไพล์โปรแกรม Windows Desktop POS ตัวเต็มใน Release Mode บันทึกลง `build_output/client_pc/` (`Psoft-RES Online.exe`), 2) คอมไพล์และบิลด์ชุดเว็บไซต์และเซิร์ฟเวอร์ Windows ลง `build_output/web_server/`, 3) เผยแพร่ชุดเซิร์ฟเวอร์ Linux (`linux-x64` self-contained) พร้อมบันเดิล Web SPA ล่าสุดและรูปภาพอาหารครบถ้วน, 4) ทำการ Deploy ขึ้นสู่ Production Linux Server (`192.168.1.247` ผ่านโดเมน `https://spk.p-services.net/`) สำเร็จ 100%, 5) ทดสอบ Service Status, Health Check และ Tenant Store API ใช้งานได้สมบูรณ์ โดยไม่กระทบบริการอื่นของ PM2 | ผ่านการทดสอบและ Deploy สำเร็จ (Production 100% OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 25)** (2026-10-08) | Hardware-Bound Licensing & Dev KeyGen | 1) ย้ายระบบตรวจสอบลิขสิทธิ์มาเป็น Client-Side Hardware-Bound บน C# WPF POS, 2) ระบบทดลองใช้ 14 วันจริง (Calendar Days) จดจำข้าม 3 ชั้น (Registry, ProgramData, LocalAppData) พร้อม Self-Healing ลบลงใหม่ก็นับต่อ และ Anti-Clock Rollback ตรวจจับการโกงเวลา, 3) การเข้ารหัสลายเซ็นดิจิทัล RSA-2048 Asymmetric (มีเฉพาะ Public Key บนเครื่องลูกค้า ป้องกันการแกะสร้างคีย์เอง), 4) สร้างโปรแกรมออกคีย์ของนักพัฒนาโดยเฉพาะ (`RestaurantPOS.KeyGen.exe` ใน `tools/`) รองรับตลอดชีพ/1 ปี/กำหนดวัน และ Export ไฟล์ `.lic`, 5) ถอดโมดูล KeyGen ออกจาก Web App 100%, 6) เพิ่มชุดทดสอบ TDD สำหรับ Licensing รวมเป็น 27 เคสทดสอบผ่าน 100% | ผ่านการทดสอบ (27/27 Tests Passed & Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 26)** (2026-10-08) | Unified Package & Production Deployment | 1) คอมไพล์ตัวเต็ม Release สำหรับ C# WPF POS (`Psoft-RES Online.exe`) และ KeyGen (`RestaurantPOS.KeyGen.exe`), 2) รวบรวม Software และ KeyGen Software ไว้ในแพ็กเกจชุดเดียวกัน (`RestaurantPOS_Package_v1.0`) ทั้งใน `build_output/` และบน `Desktop` พร้อม Master Launchers และคู่มือ, 3) Deploy Web SPA และ Central Server ขึ้น Production Linux Server (`https://spk.p-services.net/`) สำเร็จ 100%, 4) ผ่านการทดสอบ TDD 27/27 เคส และ Production Audit 23/23 เคส | ผ่านการทดสอบและ Deploy สำเร็จ (Production 100% OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 27)** (2026-10-08) | Fix UI Dispatcher NullReferenceException on Startup | 1) แก้ไขข้อผิดพลาด NullReferenceException ใน `ApplyLiveOrdersFilter` และ `FilterOrders_Changed` ที่เกิดขึ้นเมื่อเปิดโปรแกรมครั้งแรกบนเครื่องใหม่เนื่องจาก RadioButton ถูกทริกเกอร์ก่อนที่ DataGrid (`GridLiveOrders`) จะถูกสร้างเสร็จใน XAML Parser, 2) เพิ่ม Null-Safety Guards ในทุก Event Handlers ของ UI (`FilterProducts`, `FilterIngredients`, `SliderSoundVolume_ValueChanged`), 3) อัปเดตและคอมไพล์ชุด Release Packages บน Desktop และ `build_output/` ใหม่ทันที | ผ่านการทดสอบ (27/27 Tests & Audit OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 28)** (2026-10-08) | Fix HttpClient BaseAddress Mutation Exception | 1) แก้ไขข้อผิดพลาด InvalidOperationException ("This instance has already started one or more requests. Properties can only be modified before sending the first request") ใน `ApiClient.UpdateConnection`, 2) ปรับปรุงสถาปัตยกรรม `ApiClient` ให้ใช้ Thread-safe `InitHttpClient()` ที่สร้างอินสแตนซ์ `HttpClient` ใหม่แทนการเปลี่ยน `BaseAddress` ของตัวเดิมหลังเริ่มส่ง Request แล้ว พร้อมเก็บรักษา Bearer Token ไว้อย่างถูกต้อง, 3) รีคอมไพล์ชุด Release และอัปเดตไฟล์แพ็กเกจส่งมอบบน Desktop และ `build_output/` เรียบร้อย | ผ่านการทดสอบ (27/27 Tests & Audit OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 29)** (2026-10-08) | Fix XAML BoolToVis Converter Lookup & Resource Order | 1) แก้ไขข้อผิดพลาด XamlParseException ("Cannot find resource named 'BoolToVis'") ที่ทำให้ WPF Crash ปิดตัวเองลงทันทีตอนเปิดโปรแกรม, 2) ประกาศ `<BooleanToVisibilityConverter x:Key="BoolToVis"/>` ที่ `App.xaml` (`Application.Resources`) ให้สามารถเข้าถึงได้ทั่วทั้งโปรแกรมก่อนการเรนเดอร์, 3) ย้ายตำแหน่ง `<Window.Resources>` จากท้ายไฟล์ `MainWindow.xaml` มาไว้ด้านบนก่อน `<Grid>` เพื่อให้ XAML Parser ประมวลผลทรัพยากร สไตล์ และ Converter ตามลำดับที่ถูกต้อง, 4) รีคอมไพล์ชุด Release และอัปเดตแพ็กเกจส่งมอบบน Desktop และ `build_output/` เรียบร้อย | ผ่านการทดสอบ (Build OK & Zero Errors) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 30)** (2026-10-08) | Fix Top App Header Layout Overlapping | 1) แก้ไขการแสดงผลปุ่มทับซ้อนกันระหว่างปุ่ม "ออกจากระบบ" (BtnLogout) และ "ตั้งค่าเซิร์ฟเวอร์..." (BtnServerConfig) บนแถบหัวโปรแกรม (Top Header Bar), 2) เปลี่ยนโครงสร้างคอนเทนเนอร์จาก `DockPanel` เป็น `Grid` แบบ 2 คอลัมน์ (Auto และ *), 3) จัดกลุ่มฝั่งซ้าย (Logo, ร้าน, สิทธิ์ใช้งาน) และฝั่งขวา (ผู้ใช้งาน, ออกจากระบบ, ตั้งค่าเซิร์ฟเวอร์, สถานะเชื่อมต่อ, นาฬิกา) อย่างเป็นสัดส่วน พร้อมปรับ Padding/Font ให้กระชับ ไม่ซ้อนทับกันในทุกระดับความละเอียดหน้าจอ, 4) รีคอมไพล์ชุด Release และอัปเดตแพ็กเกจส่งมอบบน Desktop และ `build_output/` เรียบร้อย | ผ่านการทดสอบ (Build OK & Zero Errors) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 31)** (2026-10-08) | Multi-Resolution & Screen Responsive Support (All Monitor Sizes) | 1) แก้ไขปัญหาจอแหว่ง ขอบจอล้น และหน้าต่างขยาย/ย่อไม่ได้บนจอความละเอียดต่ำ (1366x768, 1280x720, 1024x768 หรือจอที่มี Windows Display Scaling 125%-150%), 2) ปรับหน้าต่างให้เปิดแบบ `WindowState="Maximized"` พอดีขอบเขตทำงานของหน้าจอ (WorkArea) โดยอัตโนมัติ ไม่ล้นขึ้นด้านบนหรือจมลงล่าง, 3) ปรับ `MinHeight="500"` และ `MinWidth="900"` (จากเดิม 760x1280) ให้ผู้ใช้สามารถย่อ/ขยายหน้าต่างได้อย่างอิสระ, 4) แปลงแถบแท็บนำทาง Row 2 ให้รองรับการเลื่อนแนวนอน (Horizontal ScrollViewer) และปรับชื่อแท็บให้กระชับ ป้องกันแท็บ [F6] และปุ่มฟังก์ชันตกขอบ 100%, 5) ปรับความกว้าง Cart ถาดออเดอร์ในหน้าขายเป็น 420px ให้เหลือพื้นที่สำหรับ Product Grid อย่างเหมาะสม | ผ่านการทดสอบ (Build OK & Zero Errors) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 32)** (2026-10-08) | Mobile Full Responsive, Compact Customer Header & Real-Time Sync | 1) ปรับปรุงส่วนหัวหน้าสั่งอาหารของลูกค้า (Customer View) ให้กะทัดรัด (Compact Store & Table Quick-Bar) ลดพื้นที่ลงเหลือ ~50px ลูกค้าเห็นเมนูอาหารได้ทันทีบนมือถือโดยไม่ต้องเลื่อนยาว, 2) ปรับกล่องข้อมูลร้านค้าและแถบติดตามออเดอร์เป็นแบบพับเก็บได้ (Collapsible) ไม่บดบังเมนู, 3) ปรับปรุงการแสดงผลมือถือ Full Responsive 100% ด้วย Grid อาหาร 2 คอลัมน์ (`pos-food-grid`), ปรับสัดส่วนรูปภาพ (`pos-food-card-img`) และแถบเลื่อนหมวดหมู่นุ่มนวล (`pos-category-scroll`), 4) ซิงค์ระบบการบัญชี รายงานยอดขาย และสถานะออเดอร์เข้าโปรแกรมจริง: ปรับ Timezone ยอดขายรายวันเป็นเวลาไทย (UTC+7), กระจาย SignalR `BillClosed` / `TableStatusChanged` เมื่อจบการขาย, และ C# Desktop POS ซิงค์รีเฟรชรายงานยอดขาย (`RefreshReportsAsync`) และสถานะโต๊ะแบบ Real-Time อัตโนมัติ | ผ่านการทดสอบ (27/27 Tests Passed, Web Build OK, Master Solution Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 33)** (2026-10-08) | Password Confirmation, Store Backup/Export, Strict Tenant Isolation & Clean Reset | 1) เพิ่มการตรวจสอบและยืนยันรหัสผ่าน (Confirm Password) ในขั้นตอนลงทะเบียนร้านค้าใหม่ทั้งบนหน้า Landing และ Modal ลงทะเบียน ป้องกันการกรอกรหัสผ่านผิดพลาด, 2) ระบบสำรองและส่งออกข้อมูลร้านค้า (Store Data Backup & Export): พัฒนา API `GET /api/backup/export` และ UI สำหรับดาวน์โหลดไฟล์สำรองข้อมูล JSON ฉบับสมบูรณ์หลังล็อกอิน ทั้งบน Web App และโปรแกรม C# WPF POS, 3) รีเซ็ตล้างฐานข้อมูลร้านทดสอบบนเซิร์ฟเวอร์ให้สะอาด คลีน 100% คงเหลือเฉพาะร้านตัวอย่างสำหรับการพรีเซนต์ (`DEFAULT`) ที่มีข้อมูลจำลองครบถ้วน, 4) การันตีสถาปัตยกรรม 1 ผู้ใช้ = 1 ร้าน = 1 ฐานข้อมูล (Multi-Tenant Isolation) แยกไฟล์ฐานข้อมูลเด็ดขาด และตรวจสอบรหัสผ่านตรงกับที่ลงทะเบียนอย่างเข้มงวด | ผ่านการทดสอบ (27/27 Tests Passed, Web Build OK, Master Solution Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 34)** (2026-10-08) | Realistic Restaurant Ambient Z-Layer Background | 1) สร้างรูปภาพพื้นหลังบรรยากาศร้านอาหารระดับพรีเมียมสมจริง (Warm Golden Ambient Lighting, Elegant Dining Tables, Luxury Bistro Interior Photography) ขนาด 16:9, 2) วางเป็นพื้นหลังแบบ Z-Layer (Fixed Layer, Z-Index 0) ด้านหลังหน้าแรก (`SoftwareLandingView`) พร้อมเกลี่ยสีด้วย Radial Gradient Wash แบบจางๆ เห็นรายละเอียดร้านอาหารอย่างนุ่มนวลและมีมิติ โดยไม่รบกวนความคมชัดของข้อความ ปุ่มกด และฟอร์มลงทะเบียน | ผ่านการทดสอบ (Web Build OK, Asset Bundled OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 35)** (2026-10-08) | Dedicated Store Registration Page & POS Connection | 1) พัฒนาหน้าลงทะเบียนร้านค้าใหม่แบบแยกหน้าต่างเฉพาะ (`RegisterStorePage.tsx` URL `/?page=register`) ประกอบด้วย ชื่อร้าน, รหัสร้านค้า (Store ID / Key) พร้อมปุ่มสุ่มรหัส, พาสเวิร์ด, คอนเฟิมพาสเวิร์ดพร้อมตัวตรวจสอบ, ที่อยู่ร้านค้าสำหรับพิมพ์หัวบิล, และ API Endpoint สำหรับเชื่อมต่อโปรแกรมกับร้านค้าแบบมีปุ่มคัดลอกในคลิกเดียว, 2) เพิ่มปุ่มกดลงทะเบียนเปิดหน้าต่างใหม่ (`window.open('/?page=register', '_blank')`) บนแถบ Portal Header, Hero Section และหน้าแรก, 3) จัดแสดงคำแนะนำและกล่องสรุปข้อมูลเข้าใช้งานหลังลงทะเบียนชัดเจน: "เวลาใช้งานก็แค่เอาไอดี กับ พาสที่สมัครลงทะเบียนไว้ ไปล็อกอิน" ในโปรแกรม POS บน Windows หรือบนเว็บ พร้อมปุ่มคัดลอกข้อมูลสรุปทั้งหมด | ผ่านการทดสอบ (27/27 Tests Passed, Web Build OK, Master Solution Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 36)** (2026-10-08) | Production Full Deployment & Release Packages | 1) คอมไพล์และบิลด์ Web SPA ชุดล่าสุดพร้อม Dedicated Registration Page และรูปภาพพื้นหลัง Realistic Z-Layer, 2) Publish ASP.NET Core Central Server ตัวเต็ม (.NET 10 linux-x64 self-contained), 3) Deploy ขึ้น Production Linux Server (`192.168.1.247` ผ่านโดเมน `https://spk.p-services.net/`) สำเร็จ 100%, 4) ทดสอบ Service Status (`restaurantpos.service` Active Running), Health Check (200 OK) และ Asset Bundle สมบูรณ์ โดยบริการ PM2 อื่นๆ ยังคงทำงานปกติ, 5) อัปเดตและคอมไพล์ชุด Release สำหรับ Windows Desktop POS และอัปเดตแพ็กเกจส่งมอบบน Desktop (`RestaurantPOS_Package_v1.0`) เรียบร้อย | ผ่านการทดสอบและ Deploy สำเร็จ (Production 100% OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 37)** (2026-10-08) | Clean Landing Page: Replace Inline Form with Core Modules & Quick Launch | 1) นำฟอร์มลงทะเบียนร้านค้ายาวเหยียดออกจากหน้าแรก (`SoftwareLandingView.tsx`) ให้หน้าหลักสะอาด สบายตา และมีระดับ, 2) เพิ่มส่วนนำเสนอโมดูลการทำงานและฟีเจอร์เด่นระดับองค์กร 6 โมดูลหลัก (Windows Desktop POS หน้าร้าน, สั่งอาหาร QR Code, จอครัว KDS เรียลไทม์, คลังวัตถุดิบ & สูตรอาหาร BOM, รายงานยอดขายและสำรองข้อมูล, สถาปัตยกรรมคลาวด์แยกฐานข้อมูล 100%), 3) เพิ่มแบนเนอร์ Quick Launch & Registration Banner พร้อมปุ่มเปิดหน้าต่างลงทะเบียนเฉพาะ (`window.open('/?page=register', '_blank')`), กล่อง Server API URL แบบคัดลอกได้ และคำแนะนำการใช้งานชัดเจน, 4) Deploy ขึ้น Production Linux Server (`https://spk.p-services.net/`) สำเร็จ 100% พร้อมทดสอบ Health Check และ Asset Bundle สมบูรณ์ | ผ่านการทดสอบและ Deploy สำเร็จ (Production 100% OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 38)** (2026-10-08) | Strict Separation of Table Reservation & Food Ordering with Live Visual Sync | 1) แยกโมดูล "การจองโต๊ะ (Table Reservation)" กับ "การสั่งอาหาร (Food Ordering)" ออกจากกัน 100% ทั้งบน Web App และ Windows Desktop POS, 2) ปรับการแสดงผลสีสถานะโต๊ะตามมาตรฐานอย่างเคร่งครัด: สีเหลือง = โต๊ะจองแล้ว (Reserved), สีแดง = โต๊ะทำงานอยู่/มีลูกค้า/มีออเดอร์ค้าง (Occupied), สีปกติ/ขาว = โต๊ะว่าง (Available) ทั้งบนปุ่มด่วน Quick Tables, ผังโต๊ะ และหน้าต่างจัดการโต๊ะ, 3) ซิงค์ข้อมูล Real-Time ข้ามระบบผ่าน SignalR TableStatusChanged สอดคล้องกันทั้ง Server Hub, Windows Desktop POS และ Web App, 4) ระบบจัดการจองโต๊ะพร้อมปุ่มเช็คอินเข้าโต๊ะเพื่อเริ่มสั่งอาหาร และปุ่มยกเลิกการจองคืนสถานะโต๊ะว่าง, 5) ระบบย้าย Schema อัตโนมัติ (Automated Database Migration) และ Thread-Safe Synchronization ผ่านการทดสอบชุดทดสอบอัตโนมัติครบถ้วน 100% | ผ่านการทดสอบ (27/27 Tests Passed, Master Solution Build OK, Web Build OK) |
| **เวอร์ชัน 1.0 (แก้ไขครั้งที่ 39)** (2026-10-08) | Full Production Deployment & Package Release | 1) คอมไพล์และบิลด์ Web SPA ชุดล่าสุดพร้อมระบบแยกการจองโต๊ะและสั่งอาหารเด็ดขาด และโมดูลจัดการโต๊ะสด, 2) เผยแพร่ ASP.NET Core Central Server ตัวเต็ม (.NET 10 linux-x64 self-contained), 3) Deploy ขึ้น Production Linux Server (`192.168.1.247` ผ่านโดเมน `https://spk.p-services.net/`) สำเร็จ 100%, 4) ตรวจสอบ Service `restaurantpos.service` (Active Running), Health Check (200 OK), API Tables พร้อม Schema การจองโต๊ะ และคงสถานะบริการ PM2 อื่นๆ ของระบบ pcom ทำงานปกติ 100%, 5) อัปเดตและคอมไพล์ชุด Release ของ Windows Desktop POS (`Psoft-RES Online.exe`) บันทึกลง `build_output` และ `RestaurantPOS_Package_v1.0` บน Desktop เรียบร้อย | ผ่านการทดสอบและ Deploy สำเร็จ (Production 100% OK & 23/23 Audit Tests Passed) |








