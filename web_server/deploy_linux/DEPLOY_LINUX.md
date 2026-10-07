# Restaurant POS — Linux Server Deployment Guide

คู่มือการติดตั้งและ Deploy เครื่องแม่ข่าย **Restaurant POS Linux Server** (ASP.NET Core 10 Web API + SignalR + Vite React Frontend)

---

## 1. ข้อกำหนดเบื้องต้นของระบบ (System Requirements)
- **ระบบปฏิบัติการ:** Linux (Ubuntu 22.04 LTS+, Debian 12+, หรือ Rocky Linux 9+)
- **Runtime:** .NET 10.0 Runtime (`dotnet-runtime-10.0` หรือ `aspnetcore-runtime-10.0`)
- **Web Server (ทางเลือก):** Nginx (สำหรับ Reverse Proxy และ SSL/TLS)

---

## 2. ขั้นตอนการติดตั้ง .NET 10 Runtime บน Ubuntu / Debian
```bash
# อัปเดตแพ็กเกจระบบ
sudo apt update && sudo apt upgrade -y

# ติดตั้ง .NET 10 ASP.NET Core Runtime
sudo apt install -y dotnet-runtime-10.0 aspnetcore-runtime-10.0
```

ตรวจสอบการติดตั้ง:
```bash
dotnet --info
```

---

## 3. การนำไฟล์ขึ้นเซิร์ฟเวอร์ (Deploy Files)
1. คัดลอกโฟลเดอร์จากเครื่องพัฒนา:
   - ปลายทางบน Linux: `/var/www/restaurantpos/server/`
   - แหล่งต้นทาง: โฟลเดอร์ `build_output/web_server/`
2. ตรวจสอบว่าใน `/var/www/restaurantpos/server/` มีโครงสร้างดังนี้:
   - `RestaurantPOS.Server.dll`
   - `appsettings.json`
   - โฟลเดอร์ `wwwroot/` (มีไฟล์เว็บ `index.html`, `assets/`, `favicon.svg`)
   - โฟลเดอร์ `logs/`

---

## 4. ติดตั้งเป็น Linux Background Service (Systemd)
คัดลอกไฟล์ `restaurantpos.service`:
```bash
sudo cp restaurantpos.service /etc/systemd/system/restaurantpos.service
sudo systemctl daemon-reload
sudo systemctl enable restaurantpos.service
sudo systemctl start restaurantpos.service
```

ตรวจสอบสถานะการทำงาน:
```bash
sudo systemctl status restaurantpos.service
```

ดู Log การทำงานแบบ Real-time:
```bash
sudo journalctl -u restaurantpos.service -f
```

---

## 5. การตั้งค่า Nginx Reverse Proxy (พอร์ต 80 / 443)
คัดลอกการตั้งค่า Nginx:
```bash
sudo cp nginx.conf /etc/nginx/sites-available/restaurantpos
sudo ln -s /etc/nginx/sites-available/restaurantpos /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

## 6. การทดสอบการเชื่อมต่อ (Verification)
- ตรวจสอบ Health Check:
  ```bash
  curl http://localhost:5000/api/health
  ```
  ผลลัพธ์ที่คาดหวัง:
  ```json
  {"status":"Healthy","timestamp":"...","version":"1.0.0","server":"RestaurantPOS Linux/Windows Server"}
  ```
- ทดสอบเปิดเบราว์เซอร์: `http://<IP-เครื่องเซิร์ฟเวอร์>:5000/` เพื่อใช้งาน Customer QR Ordering & Management SPA
- ในเครื่อง Client Windows POS: กดปุ่ม **"ทดสอบการเชื่อมต่อ (Test Connection)"** โดยระบุ IP ของ Linux Server นี้
