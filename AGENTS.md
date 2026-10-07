# Restaurant POS System — Guidelines & Architecture Rules

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
   - **Linux / Windows Server (ASP.NET Core):**
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

4. **การพิมพ์สลิปและใบสั่งครัว (Printing Policy):**
   - การสั่งพิมพ์ทั้งหมดต้องผ่านเครื่องคอมพิวเตอร์ Windows PC หน้าร้านโดยตรง (Windows Spooler / ESC/POS)
   - ห้ามให้ Web App สั่งเครื่องพิมพ์โดยตรง

5. **โครงสร้างโครงการ (Solution Structure):**
   โครงการถูกแบ่งออกเป็น 2 ส่วนซอร์สโค้ด และ 1 โฟลเดอร์บิลด์:
   - **`web_server/`**: ซอสฝั่งเว็บและเซิร์ฟเวอร์กลาง (เตรียมไว้เผื่อ Deploy ขึ้น Linux)
     - `RestaurantPOS.Server`: ASP.NET Core 10 Web API + SignalR + EF Core
     - `RestaurantPOS.Web`: React + Vite + TypeScript (Customer QR & Management SPA)
     - `RestaurantPOS.Shared`: Shared Class Library
     - `deploy_linux/`: Systemd service, Nginx config, Dockerfile, `deploy_linux.sh`
     - `RestaurantPOS.WebServer.slnx`: Solution แยกฝั่ง Web & Server
   - **`client_pc/`**: ซอสฝั่ง Client PC Software หน้าร้าน
     - `RestaurantPOS.Wpf`: .NET 10 WPF Desktop POS สไตล์ Classic XP
     - `RestaurantPOS.Shared`: Shared Class Library
     - `RestaurantPOS.Client.slnx`: Solution แยกฝั่ง Client POS
   - **`build_output/`**: โฟลเดอร์โปรแกรมที่ build ออกมาแล้ว (รอคำสั่ง Deploy จากผู้ใช้)
     - `client_pc/`: ไฟล์บิลด์พร้อมรันของ Windows Desktop POS
     - `web_server/`: ไฟล์บิลด์พร้อม deploy ของ Server และ Web SPA (`wwwroot/`)
   - **`RestaurantPOS.slnx`**: Master Solution รวมทุกโปรเจกต์
   - **`run.bat`**: เมนูลัดสำหรับรันทั้งระบบ

6. **นโยบายการ Deploy (Strict Deployment Gate):**
   - ห้ามทำการ Deploy อัตโนมัติหลังแก้โค้ดเด็ดขาด ให้หยุดรอคำสั่งจากผู้ใช้ก่อนเสมอ
   - ห้ามสร้างไฟล์บีบอัด (.zip/.rar) เด็ดขาด ให้เตรียมโฟลเดอร์เต็มที่พร้อมใช้งาน

7. **การบันทึก Progress การทำงานอย่างกระชับ (Lean Progress Logging Policy):**
   - ทุกครั้งที่มีการพัฒนา ปรับปรุง หรือแก้ไขฟีเจอร์สำคัญ ต้องบันทึก Progress การทำงานลงใน `README.md`
   - **อย่าให้โปรเกรสบวม (Keep it Lean):** สรุปเป็นตารางหรือประเด็นกระชับ ชัดเจน ระบุเฉพาะสาระสำคัญ วันที่/เวอร์ชัน โมดูล และผลการทดสอบ ไม่เขียนเวิ่นเว้อหรือแปะโค้ดยาวๆ เพื่อให้เอกสารอ่านง่ายและคงประสิทธิภาพสูงสุด

8. **รูปแบบการระบุเวอร์ชันในเอกสาร (Versioning Policy):**
   - ให้ระบุเป็น `เวอร์ชัน 1.0 (แก้ไขครั้งที่ ...)` โดยเริ่มที่เวอร์ชัน 1.0 เสมอ
   - ห้ามเปลี่ยนเลขเวอร์ชันหลัก (ห้ามขึ้นเป็น v1.1, v2.0 เอง) จนกว่าผู้ใช้จะสั่งเปลี่ยนเด็ดขาด


