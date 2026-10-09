---
name: restaurantpos-guide
description: >-
  Essential knowledge, architectural reference, strict rules, and deployment policies for RestaurantPOS (.NET 10 WPF Desktop POS + ASP.NET Core Linux Server + Vite React Web App). Use whenever the user asks to modify, develop, build, run, test, print, review, or deploy RestaurantPOS.
---

# RestaurantPOS Master Developer & Agent Guide

คู่มือสถาปัตยกรรม, กฎเหล็ก, สองโมเดลธุรกิจ, โครงสร้างฐานข้อมูล, และแนวทางการพัฒนาโปรแกรม **RestaurantPOS System (Enterprise Edition)** สำหรับ AI Agent และทีมพัฒนา เพื่อให้การทำงานในทุกเซสชันเป็นไปในทิศทางเดียวกันอย่างสมบูรณ์แบบ 100%

---

## 1. กฎเหล็กประจำโครงการ (Strict Rules — ห้ามละเมิดเด็ดขาด)

1. **ห้ามใช้อิโมจิในโปรแกรมโดยเด็ดขาด (Strictly ZERO Emojis)**:
   - ห้ามใส่อิโมจิใดๆ ในส่วนของ User Interface (UI), ปุ่มกด (Buttons), หัวข้อเมนู (Navigation), ข้อความแจ้งเตือน (Dialogs/Alerts), สลิปใบเสร็จ (Thermal Receipts), รายงาน (Reports) หรือข้อความในโค้ดที่แสดงผลต่อผู้ใช้งาน
   - ให้ใช้ข้อความทางการที่สุภาพและ Color Badges (เช่น `[ว่าง]`, `[มีลูกค้า]`, `[รอชำระเงิน]`, `[Online]`, `[Offline]`) แทนเสมอ

2. **กฎเหล็กการ Deploy (Strict Deployment Gate Policy)**:
   - **ห้ามทำการ Deploy อัตโนมัติหลังแก้โค้ดเด็ดขาด:** รันเฉพาะ Automated Tests (`dotnet test`) หรือ Build เพื่อตรวจสอบความถูกต้อง ห้ามปล่อยตัวโปรแกรมขึ้น Production โดยพลการ ต้องหยุดการทำงานทันทีและสรุปผลเพื่อให้ผู้ใช้เป็นผู้ทดสอบและสั่ง Deploy เสมอ
   - เงื่อนไขเดียวที่จะ Deploy ได้คือ ผู้ใช้เป็นฝ่ายพิมพ์คำสั่งสั่งการอย่างชัดเจน เช่น *"Deploy"*, *"สั่ง Deploy ได้"*, *"Deploy ขึ้น production เลย"*
   - **ห้ามสร้างไฟล์บีบอัด (.zip/.rar) เด็ดขาด:** ส่งมอบเฉพาะโฟลเดอร์ตัวเต็มที่พร้อมใช้งาน (Uncompressed Folders)

3. **การบันทึก Progress การทำงานอย่างกระชับ (Lean Progress Logging Policy)**:
   - ทุกครั้งที่มีการพัฒนาหรือปรับปรุงระบบ ต้องบันทึกความคืบหน้า (Progress Log) ลงใน `README.md`
   - **อย่าให้โปรเกรสบวม (Keep it Lean & Structured):** สรุปเป็นตารางกระชับ ระบุเฉพาะสาระสำคัญ วันที่/เวอร์ชัน โมดูล และผลการทดสอบ ไม่บันทึกเยิ่นเย้อหรือแปะโค้ดยาว

4. **รูปแบบการระบุเวอร์ชันในเอกสาร (Versioning Policy)**:
   - ให้ระบุเป็น `เวอร์ชัน 1.0 (แก้ไขครั้งที่ ...)` โดยเริ่มที่เวอร์ชัน 1.0 เสมอ
   - ห้ามเปลี่ยนเลขเวอร์ชันหลัก (ห้ามขึ้นเป็น v1.1, v2.0 เอง) จนกว่าผู้ใช้จะสั่งเปลี่ยนเด็ดขาด

5. **การพิมพ์สลิปและใบสั่งครัว (Printing Policy)**:
   - การสั่งพิมพ์ทั้งหมดต้องผ่านเครื่องคอมพิวเตอร์ Windows PC หน้าร้านโดยตรง (Windows Spooler / ESC/POS)
   - ห้ามให้ Web App สั่งเครื่องพิมพ์โดยตรง

---

## 2. สถาปัตยกรรม 2 โมเดลธุรกิจ (Dual Commercial Architecture)

ระบบถูกออกแบบให้รองรับ 2 โมเดลธุรกิจบน Production Server เดียวกันอย่างชัดเจน:

### 2.1 ส่วนที่ 1: ส่วนที่เรารับดูแล (Platform SaaS Hub: `https://spk.p-services.net/`)
- **กลุ่มเป้าหมาย:** ลูกค้าทั่วไปที่ต้องการให้เราดูแลเซิร์ฟเวอร์ คลาวด์ และฐานข้อมูลให้
- **คุณลักษณะและสิทธิ์:**
  - มีปุ่ม `[+ ลงทะเบียนร้านค้าใหม่]` (`/?page=register`) สำหรับเปิดร้านใหม่แยกฐานข้อมูลอัตโนมัติ (`POST /api/stores/register`)
  - มีศูนย์ติดตามเซสชัน (Active Sessions Monitor) และเครื่องมือวิศวกร (DEV Action Panel) สำหรับทีมวิศวกรเข้าดูแลระบบผ่าน `X-Dev-Key: rpos_dev_master_2026` หรือ SuperAdmin
  - ซิงค์ข้อมูลข้ามเครื่องเฉพาะในกลุ่มร้านค้าตนเองผ่าน SignalR Tenant Room

### 2.2 ส่วนที่ 2: ส่วนสำหรับขายเดี่ยวให้ลูกค้านำไปลง VPS เอง (Standalone Turnkey Edition: `https://spk.p-services.net/standalone`)
- **กลุ่มเป้าหมาย:** ลูกค้าที่ซื้อขาดโปรเจกต์เพื่อนำไปติดตั้งบน VPS ส่วนตัว
- **การล็อกและระบบป้องกันเชิงพาณิชย์ (Commercial Protection Guards):**
  - **ล็อกร้านค้าประจำระบบ:** ล็อกรหัสร้านค้าเป็น `RPOS-DEMO-0001` (ร้านอาหารรสเด็ด ชวนชิม) เป็น Single-Store Mode อัตโนมัติ
  - **ปิดระบบลงทะเบียนร้านใหม่ 100%:** ไม่มีปุ่มลงทะเบียนใน UI/Header และบล็อก `POST /api/stores/register` ด้วย `403 Forbidden` (ป้องกันไม่ให้ผู้ซื้อนำโค้ดไปเปิดเป็นแพลตฟอร์มรับสมัครร้านค้าแข่งกับเรา)
  - **ปิดตายเครื่องมือ DEV Panel 100%:** บล็อก `/api/dev/*` ด้วย `403 Forbidden` เสมอ เมื่อเข้าใช้งานผ่าน standalone, ปิดคีย์ลัด `Ctrl+Alt+D`, และซ่อน Floating DEV Toolbar
  - **ตัดคำศัพท์เทคนิคและไร้ปุ่มสลับระบบ 100%:** หน้าเว็บมีเฉพาะชื่อระบบ, ปุ่มสั่งอาหาร QR โต๊ะ, และปุ่มเข้าสู่ระบบแคชเชียร์ เสมือนเว็บไซต์ของร้านอาหารจริงๆ โดยไม่มีคำอย่าง `Standalone Turnkey Edition`, `Platform SaaS Hub` หรือปุ่มสลับระบบกลับมายัง SaaS ของเรา
  - **ระบบ Auto-Online Presence:** `PosPresenceTracker` ตั้งค่าสถานะออนไลน์จำลองให้อัตโนมัติสำหรับ `RPOS-DEMO-0001` เพื่อให้ผู้สนใจสามารถทดลองสั่งอาหารผ่านโต๊ะ T01-T12 ได้จริง 24 ชั่วโมง โดยไม่ต้องเปิดโปรแกรมแคชเชียร์ Windows ทิ้งไว้

---

## 3. สถาปัตยกรรมฐานข้อมูลและกฎเหล็กการ Deploy (Database & Server Safety)

### 3.1 สถาปัตยกรรมฐานข้อมูล
- **โฟลเดอร์ฐานข้อมูลบน Linux:** `/home/pon/restaurantpos/tenants/`
- **Master Catalog:** `master.db` เก็บรายชื่อร้านค้าในตาราง `Tenants`
- **Tenant Databases:** แยกไฟล์ฐานข้อมูล 1 ร้าน = 1 ไฟล์ (`DEFAULT.db`, `RPOS-DEMO-0001.db`)

### 3.2 กฎความปลอดภัยเซิร์ฟเวอร์ (Zero-Damage Server Safety)
1. **ห้ามลบ RPOS-DEMO-0001.db เด็ดขาด:**
   - ในการรีเซ็ตฐานข้อมูลหรือ Deploy ต้องรักษาทั้ง `DEFAULT.db`, `RPOS-DEMO-0001.db`, และ `master.db` เสมอ
   - สคริปต์ทำความสะอาดต้องใช้เงื่อนไข:
     `if base not in ('DEFAULT.db', 'RPOS-DEMO-0001.db', 'master.db'): os.remove(f)`
   - ใน `master.db` ต้องรักษาทั้ง `DEFAULT` และ `RPOS-DEMO-0001` ไว้เสมอ
2. **ห้ามแตะต้องบริการ PM2 บนเซิร์ฟเวอร์เด็ดขาด:**
   - เซิร์ฟเวอร์ `192.168.1.247` มีบริการสำคัญอื่นๆ ของระบบกำลังทำงานอยู่:
     `pcom-web`, `supon_keawsri`, `pcom-spk`, `pcom-finance`, `pcom-storage`
   - ห้ามรันคำสั่ง `pm2 stop all` หรือแตะต้องบริการของ pcom โดยเด็ดขาด
3. **การบริการของ RestaurantPOS บนเซิร์ฟเวอร์:**
   - Systemd Service: `restaurantpos.service` (Active Running บนพอร์ต 3000)
   - Cloudflare Tunnel Forward: `spk.p-services.net` ➔ `http://127.0.0.1:3000`

---

## 4. โครงสร้างซอร์สโค้ดและหน้าที่ของโมดูล (Directory Layout)

```text
res-pos_psoft/
├── web_server/                         # [ส่วนที่ 1] ซอสฝั่งเว็บและเซิร์ฟเวอร์กลาง (.NET 10 + React 19)
│   ├── RestaurantPOS.Server/           # ASP.NET Core 10 Web API + SignalR Hubs + EF Core
│   │   ├── Controllers/                # StoresController (โหมด Platform/Standalone), DevController, OrdersController
│   │   ├── Hubs/                       # PosHub (SignalR Room-based Real-time Sync)
│   │   ├── Tenancy/                    # TenantService, PosPresenceTracker, TenantNotifier
│   │   └── wwwroot/                    # Web SPA บิลด์แล้วพร้อม assets และรูปภาพอาหาร
│   ├── RestaurantPOS.Web/              # React 19 + TypeScript + Vite (Customer QR & Management)
│   │   ├── src/components/             # SoftwareLandingView, RegisterStorePage, TableManager, KDS, DevActionPanel
│   │   └── src/services/               # api.ts (Header X-Request-Mode: standalone), realtime.ts, logger.ts
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10 DTOs, Enums, Models)
│   ├── deploy_linux/                   # สคริปต์อัตโนมัติ deploy_remote.py, คอนฟิก systemd, Docker, nginx
│   └── RestaurantPOS.WebServer.slnx    # Solution แยกสำหรับงาน Web & Server
│
├── client_pc/                          # [ส่วนที่ 2] ซอสฝั่ง Client PC Software (เครื่องแคชเชียร์หน้าร้าน)
│   ├── RestaurantPOS.Wpf/              # C# .NET 10 WPF Desktop POS (Classic Windows XP Theme)
│   │   ├── Views/                      # MainWindow, OrderDetailsDialog, ServerConfigDialog, ErrorDialog
│   │   ├── Services/                   # ApiClient, RealtimeClient, SoundPlayer, AssetSyncService
│   │   └── Licensing/                  # Hardware-Bound RSA-2048 Verification Engine
│   ├── RestaurantPOS.Shared/           # Shared Class Library (.NET 10)
│   └── RestaurantPOS.Client.slnx       # Solution แยกสำหรับงาน Windows Desktop POS
│
├── tools/                              # [ส่วนพิเศษ] เครื่องมือนักพัฒนา (Developer Only - เก็บส่วนตัว)
│   └── RestaurantPOS.KeyGen/           # โปรแกรมออกคีย์ลิขสิทธิ์ผูกฮาร์ดแวร์ RSA-2048 (Lifetime, 1 Year, Days)
│
├── tests/                              # [ส่วนที่ 4] ชุดทดสอบอัตโนมัติ TDD (27/27 Tests Passed)
│   └── RestaurantPOS.Tests/            # xUnit + ASP.NET Core TestServer + SignalR Test Clients
│
├── build_output/                       # [ส่วนที่ 5] โฟลเดอร์โปรแกรมที่บิลด์แล้ว (พร้อมส่งมอบเมื่อสั่ง Deploy)
│   ├── client_pc/                      # Psoft-RES Online.exe พร้อมไฟล์คอนฟิกและสคริปต์รัน
│   └── web_server/                     # RestaurantPOS.Server.dll พร้อม wwwroot
│
├── RestaurantPOS.slnx                  # Master Solution File รวมทุกโปรเจกต์
├── run.bat                             # Master Quick Launcher เมนูลัดสำหรับรันทั้งระบบ
└── AGENTS.md                           # กฎและระเบียบสถาปัตยกรรมสำหรับ AI Agent
```

---

## 5. การตรวจสอบความถูกต้องและการทดสอบ (Testing & Verification)

ทุกครั้งที่มีการแก้ไขซอร์สโค้ด:
1. **รัน Unit & Integration Tests เสมอ:**
   ```powershell
   dotnet test tests/RestaurantPOS.Tests/RestaurantPOS.Tests.csproj
   ```
   *ต้องผ่านครบ 27 จาก 27 เคสทดสอบ (27/27 Tests Passed, 0 Failures)*
2. **คอมไพล์ TypeScript & Vite Web App:**
   ```powershell
   cd web_server/RestaurantPOS.Web
   npm run build
   ```
   *ต้องคอมไพล์ผ่าน 0 Errors และได้บันเดิลใน `dist/`*
3. **ตรวจสอบความสอดคล้องของ Shared Library:**
   เมื่อแก้ DTO/Model ใน `RestaurantPOS.Shared` ต้องอัปเดตทั้งใน `web_server/RestaurantPOS.Shared` และ `client_pc/RestaurantPOS.Shared` ให้ตรงกันเสมอ

---

## 6. ลำดับขั้นตอนการ Deploy ขึ้น Production (เมื่อผู้ใช้สั่งการเท่านั้น)

เมื่อผู้ใช้พิมพ์คำสั่งอนุญาตให้ Deploy (เช่น *"Deploy ขึ้น production เลย"*):
1. **คอมไพล์และบิลด์ Web App:**
   ```powershell
   cd web_server/RestaurantPOS.Web && npm run build
   ```
2. **คัดลอกไฟล์ Web ไปยัง wwwroot:**
   ```powershell
   robocopy web_server\RestaurantPOS.Web\dist web_server\RestaurantPOS.Server\wwwroot /MIR
   robocopy web_server\RestaurantPOS.Server\wwwroot deploy_output\linux_server\wwwroot /MIR
   # ลบไฟล์บีบอัด .gz / .br เก่าออกป้องกันการเสิร์ฟไฟล์แคชเดิม
   Remove-Item deploy_output\linux_server\wwwroot\index.html.gz -ErrorAction SilentlyContinue
   Remove-Item deploy_output\linux_server\wwwroot\index.html.br -ErrorAction SilentlyContinue
   ```
3. **Publish ไบนารี Linux Server (linux-x64):**
   ```powershell
   dotnet publish web_server/RestaurantPOS.Server/RestaurantPOS.Server.csproj -c Release -r linux-x64 --self-contained true -o deploy_output/linux_server
   ```
4. **รันสคริปต์ Remote Deploy ไปยัง Linux Server:**
   ```powershell
   python web_server/deploy_linux/deploy_remote.py
   ```
5. **ตรวจสอบ Endpoint หลังการ Deploy:**
   - Health: `curl -s https://spk.p-services.net/api/health` ➔ `{"status":"Healthy"}`
   - Platform Mode: `curl -s https://spk.p-services.net/api/stores/mode` ➔ `serverMode: "Platform"`
   - Standalone Mode: `curl -s https://spk.p-services.net/api/stores/mode?mode=standalone` ➔ `serverMode: "Standalone"`
   - Products RPOS-DEMO-0001: `curl -s -H "X-Tenant-Code: RPOS-DEMO-0001" https://spk.p-services.net/api/products` ➔ 13 รายการ
6. **บันทึกความคืบหน้าลงใน `README.md`** ใต้ตาราง `บันทึกความคืบหน้าการพัฒนา` ด้วย `เวอร์ชัน 1.0 (แก้ไขครั้งที่ ...)` อย่างกระชับ
