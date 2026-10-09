import os
import shutil

base_dir = r"c:\Users\admin\Documents\res-pos_psoft"
pkg_local = os.path.join(base_dir, "build_output", "RestaurantPOS_Package_v1.0")
pkg_desktop = r"C:\Users\admin\Desktop\RestaurantPOS_Package_v1.0"

# Directories
pos_dir = os.path.join(pkg_local, "Psoft-RES_Full")
keygen_dir = os.path.join(pkg_local, "Tools", "KeyGen")

os.makedirs(pos_dir, exist_ok=True)
os.makedirs(keygen_dir, exist_ok=True)

# 1. Copy POS Client
client_source = os.path.join(base_dir, "build_output", "client_pc")
for item in os.listdir(client_source):
    s = os.path.join(client_source, item)
    d = os.path.join(pos_dir, item)
    if os.path.isdir(s):
        if os.path.exists(d):
            shutil.rmtree(d)
        shutil.copytree(s, d)
    else:
        shutil.copy2(s, d)

# 2. Copy KeyGen
keygen_source = os.path.join(base_dir, "tools", "RestaurantPOS.KeyGen", "bin", "Release", "net10.0-windows")
for item in os.listdir(keygen_source):
    s = os.path.join(keygen_source, item)
    d = os.path.join(keygen_dir, item)
    if os.path.isdir(s):
        if os.path.exists(d):
            shutil.rmtree(d)
        shutil.copytree(s, d)
    else:
        shutil.copy2(s, d)

# 3. Create run scripts
keygen_bat_content = """@echo off
title RestaurantPOS KeyGen (Developer Tool)
chcp 65001 >nul
cd /d "%~dp0"
start "" "RestaurantPOS.KeyGen.exe"
exit /b 0
"""
with open(os.path.join(keygen_dir, "run_keygen.bat"), "w", encoding="utf-8") as f:
    f.write(keygen_bat_content)

root_pos_bat = """@echo off
title Psoft-RES Online - Master Launcher
chcp 65001 >nul
cd /d "%~dp0"
start "" "Psoft-RES_Full\\Psoft-RES Online.exe"
exit /b 0
"""
with open(os.path.join(pkg_local, "run_pos.bat"), "w", encoding="utf-8") as f:
    f.write(root_pos_bat)

root_keygen_bat = """@echo off
title RestaurantPOS KeyGen - Master Launcher
chcp 65001 >nul
cd /d "%~dp0"
start "" "Tools\\KeyGen\\RestaurantPOS.KeyGen.exe"
exit /b 0
"""
with open(os.path.join(pkg_local, "run_keygen.bat"), "w", encoding="utf-8") as f:
    f.write(root_keygen_bat)

readme_content = """======================================================================
  RESTAURANT POS SYSTEM (ENTERPRISE EDITION) - SOFTWARE PACKAGE
  Version 1.0 (Revision 26) - Production Ready Release
======================================================================

โครงสร้างชุดโปรแกรมในโฟลเดอร์นี้:
1. Psoft-RES_Full/
   - ตัวโปรแกรมขายหน้าร้าน Windows Desktop POS (Classic XP Theme)
   - ไฟล์รันหลัก: Psoft-RES Online.exe
   - สามารถดับเบิลคลิกไฟล์ "run_pos.bat" ที่โฟลเดอร์หลักเพื่อเริ่มทำงานได้ทันที

2. Tools/KeyGen/
   - เครื่องมือออกคีย์ลิขสิทธิ์สำหรับนักพัฒนา (Developer Key Generator)
   - ไฟล์รันหลัก: RestaurantPOS.KeyGen.exe
   - สามารถดับเบิลคลิกไฟล์ "run_keygen.bat" ที่โฟลเดอร์หลักเพื่อเปิดโปรแกรมได้ทันที

======================================================================
ขั้นตอนการทดสอบและเปิดใช้งานระบบ:
======================================================================

[ขั้นตอนที่ 1: การเปิดโปรแกรมขายหน้าร้าน]
1. ดับเบิลคลิก "run_pos.bat" ที่โฟลเดอร์หลัก
2. โปรแกรมจะเชื่อมต่อไปยัง Cloud Server (https://spk.p-services.net) อัตโนมัติ
3. ข้อมูลเข้าสู่ระบบร้านตัวอย่าง:
   - ร้านค้า: DEFAULT (ร้านอาหาร Restaurant POS สาขาหลัก)
   - รหัสผู้ใช้: admin
   - รหัสผ่าน: psoft123 (หรือ 123456)
   - หรือกดปุ่ม "[เข้าสู่ระบบร้านตัวอย่าง (1-Click Demo Login)]" เพื่อเข้าใช้งานทันที

[ขั้นตอนที่ 2: ระบบทดลองใช้งาน 14 วันจริง (14-Day Calendar Trial)]
- เมื่อเริ่มเปิดโปรแกรมครั้งแรกบนเครื่องใหม่ ระบบจะเปิดให้ทดลองใช้ฟรี 14 วันจริง
- นับถอยหลังตามวันปฏิทินจริง ป้องกันการลบโฟลเดอร์ลงใหม่ข้าม 3 ชั้นข้อมูล
- หากครบกำหนด 14 วัน ระบบจะระงับการสั่งขายและแสดงหน้าต่างขอเปิดใช้งานลิขสิทธิ์

[ขั้นตอนที่ 3: การออกคีย์และปลดล็อกลิขสิทธิ์ (Hardware-Bound Activation)]
1. ในโปรแกรม POS ให้คลิกที่แถบสถานะหรือปุ่ม "[เปิดใช้งานสิทธิ์ (Activate License)]"
2. โปรแกรมจะแสดง "รหัสประจำเครื่อง (Machine Code)" ให้กดปุ่ม [คัดลอกรหัสเครื่อง]
3. สลับมาเปิดโปรแกรมออกคีย์โดยดับเบิลคลิก "run_keygen.bat"
4. วางรหัสประจำเครื่องของลูกค้าลงในช่อง Machine Code
5. เลือกระยะเวลาลิขสิทธิ์:
   - ตลอดชีพ (Lifetime License)
   - รายปี (1 Year License)
   - กำหนดจำนวนวันเอง (Custom Days)
6. กดปุ่ม "[สร้างคีย์ลิขสิทธิ์]"
7. นำคีย์ที่ได้มากรอกในโปรแกรม POS แล้วกด [ยืนยันการเปิดใช้งาน] 
   หรือใช้ปุ่ม [บันทึกไฟล์สิทธิ์ (.lic)] แล้วนำไฟล์ไปเปิดในโปรแกรม POS
8. ตัวโปรแกรมจะปลดล็อกเป็นเวอร์ชันลิขสิทธิ์เต็มรูปแบบทันที

======================================================================
ข้อมูลการเชื่อมต่อและเว็บแอปพลิเคชัน:
======================================================================
- Production Web URL: https://spk.p-services.net/
- Health Check: https://spk.p-services.net/api/health
- Customer QR Ordering: https://spk.p-services.net/?store=DEFAULT&table=1
- Kitchen Display (KDS): https://spk.p-services.net/ (กด 1-Click Demo Login หรือล็อกอิน admin/psoft123)
"""
with open(os.path.join(pkg_local, "README.txt"), "w", encoding="utf-8") as f:
    f.write(readme_content)

# Copy to Desktop
os.makedirs(pkg_desktop, exist_ok=True)
shutil.copytree(pkg_local, pkg_desktop, dirs_exist_ok=True)

print("SUCCESS: Package created and copied to Desktop!")
print("Local path:", pkg_local)
print("Desktop path:", pkg_desktop)
