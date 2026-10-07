# Restaurant POS — Web & Central Server

โฟลเดอร์นี้รวบรวมซอร์สโค้ดและเครื่องมือรันสำหรับ **ฝั่งเว็บและเซิร์ฟเวอร์กลาง (Web & Central Server)** พร้อมโครงสร้างที่เตรียมไว้สำหรับการ Deploy ขึ้น **Linux Server** หรือรันบนเครื่อง Windows

---

## 1. องค์ประกอบภายในโฟลเดอร์ (Components)

- **`RestaurantPOS.Server/`**: ASP.NET Core 10 Web API + SignalR Hubs (`/hubs/pos`) + EF Core (SQLite / PostgreSQL)
  - ทำหน้าที่เป็นศูนย์กลางฐานข้อมูล กระจาย Event คำสั่งซื้อแบบ Real-time และให้บริการ REST API
  - มี `GlobalExceptionHandlingMiddleware` ดักจับ Error ทุกจุดและ Serilog บันทึกลง `logs/server-YYYYMMDD.log`
  - รองรับการเสิร์ฟไฟล์ SPA (Vite React) จากโฟลเดอร์ `wwwroot` ในโหมด Production โดยตรง
- **`RestaurantPOS.Web/`**: React 19 + TypeScript + Vite Web Application
  - สำหรับลูกค้าสแกน QR Code สั่งอาหารจากโต๊ะ (Customer QR Ordering) และแผงควบคุมระบบ (Management Dashboard)
- **`RestaurantPOS.Shared/`**: Shared Class Library (.NET 10)
  - DTOs, Enums, Models, ErrorCodes, HubEvents ที่ใช้งานร่วมกัน
- **`deploy_linux/`**: เครื่องมือและคอนฟิกสำหรับ Deploy ขึ้นระบบปฏิบัติการ Linux
  - `restaurantpos.service`: Systemd Service Unit สำหรับรันเป็น Background Service
  - `nginx.conf`: Nginx Reverse Proxy พร้อม WebSocket Support สำหรับ SignalR
  - `Dockerfile` & `docker-compose.yml`: สำหรับรันผ่าน Docker Container
  - `deploy_linux.sh`: สคริปต์ติดตั้งอัตโนมัติบน Ubuntu / Debian / Rocky Linux
  - `DEPLOY_LINUX.md`: คู่มือการติดตั้งและ Deploy บน Linux อย่างละเอียด
- **`RestaurantPOS.WebServer.slnx`**: Solution File แยกสำหรับเปิดและพัฒนาเฉพาะฝั่ง Web & Server

---

## 2. การรันในโหมดพัฒนา (Development Run Scripts)

| สคริปต์ | ระบบ | หน้าที่ |
| :--- | :--- | :--- |
| **`run_server.bat`** | Windows | รันเฉพาะ Central API Server (พอร์ต `5000`) |
| **`run_web.bat`** | Windows | รัน Vite Web Dev Server (พอร์ต `5173`) |
| **`run_all.bat`** | Windows | รันทั้ง Central Server และ Web App พร้อมกันใน 2 หน้าต่าง |
| **`run_server.sh`** | Linux | รัน Central API Server บน Linux ด้วย `dotnet run` |
| **`build_web_server.bat`** | Windows | บิลด์ Web SPA และ Publish Server ไปยัง `build_output/web_server/` |

---

## 3. ขั้นตอนการ Deploy ขึ้น Linux Server

โปรดดูรายละเอียดใน [deploy_linux/DEPLOY_LINUX.md](file:///c:/Users/admin/Documents/res-pos_psoft/web_server/deploy_linux/DEPLOY_LINUX.md)
