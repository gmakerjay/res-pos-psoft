Restaurant POS System — Development Prompt

พัฒนาโปรแกรม Restaurant POS แบบ Windows Desktop + Web App + Linux Server โดยเน้นความเรียบง่าย เสถียร ใช้งานเร็ว และเหมาะกับร้านอาหารจริง

1. Windows POS
Technology: C# / Windows Desktop
UI สไตล์โปรแกรม Windows XP/Classic: เรียบ หรู อ่านง่าย ไม่รก
ห้ามใช้อิโมจิในโปรแกรม
Responsive รองรับจอประมาณ 1280px ถึง 4K
UI ต้องปรับขนาดอัตโนมัติ
รองรับรูปสินค้า/หมวดหมู่
Import รูปภาพได้ และ Resize/Optimize อัตโนมัติ
รองรับฟังก์ชัน POS ร้านอาหารครบถ้วน:
เปิด/ปิดบิล
โต๊ะ / Take Away / Delivery
เพิ่ม/ลด/แก้รายการ
จำนวน / ราคา / ส่วนลด
หมายเหตุรายการ
ชำระเงิน
พิมพ์ใบเสร็จ
พิมพ์ใบสั่งครัว
ยกเลิก/คืนรายการตามสิทธิ์
สต็อกสินค้า
รายงานยอดขาย
ประวัติการขาย
จัดการสินค้า/หมวดหมู่
จัดการพนักงานและสิทธิ์
ตั้งค่าร้าน/เครื่องพิมพ์

การพิมพ์ทั้งหมดต้องสั่งจากเครื่อง Windows PC ไม่ให้ Web App สั่ง Printer โดยตรง

2. Web App

Web App มี 2 กลุ่มหลัก

Customer Ordering

ลูกค้าสแกน QR หรือเปิด Web App แล้วสั่งอาหารได้โดยไม่ต้องรอพนักงาน

Flow:
Customer → Web App → Linux Server → POS/Tablet/Kitchen

เมื่อมี Order ใหม่:

POS ต้องได้รับข้อมูลแบบ Real-time
Tablet/Mobile ของร้านสามารถรับ Order ได้
มีสถานะ Order เช่น New / Accepted / Preparing / Ready / Completed / Cancelled
รองรับเลขโต๊ะ/จุดรับอาหาร
ป้องกัน Order ซ้ำและข้อมูลซ้ำ
Management

ใช้จัดการระบบจาก Web App ตามสิทธิ์:

สินค้า
หมวดหมู่
ราคา
รูปภาพ
ผู้ใช้งาน
สิทธิ์
Order
รายงาน
ประวัติ
การตั้งค่าระบบ

ต้องมี Super Admin สำหรับจัดการผู้ใช้งานและกำหนด Permission

3. Linux Server / Database

ใช้ Linux Server เป็นศูนย์กลางระบบ

หน้าที่:

Database กลาง
API
Authentication
Permission
Order Management
Realtime Event/Notification
Sync ข้อมูลระหว่าง POS และ Web App
เก็บประวัติและข้อมูลรายงาน

Architecture:

Windows POS
Tablet/Mobile
Customer Web
Web Management
↓
Linux Server / API / Realtime
↓
Central Database

ทุก Client ต้องใช้ API และระบบกลาง ไม่ให้ Client แก้ Database โดยตรง

4. Real-time

การเปลี่ยนแปลงสำคัญต้องกระจายแบบ Real-time เช่น:

Customer Order → POS
POS Update Order → Kitchen/Tablet
POS Update Product → Web
Web Update Product → POS
Order Status Change → ทุก Client ที่เกี่ยวข้อง

หากมีการเชื่อมต่อขาด ต้องมีระบบตรวจ Connection และ Sync ข้อมูลกลับเมื่อเชื่อมต่อได้

5. Authentication / Permission

ต้องออกแบบ Permission แบบละเอียด เช่น:

View POS
Create Order
Edit Order
Cancel Order
Refund
Discount
Manage Product
Manage Stock
Manage Users
Manage Settings
View Reports
Export/Import Data

Admin สามารถสร้าง Role และกำหนดสิทธิ์ให้ผู้ใช้งานแต่ละคนได้

6. Export / Import / Backup

ระบบต้องสามารถ:

Export ข้อมูล
Import ข้อมูล
Backup Database
Restore Database
Export รายงาน
ดูประวัติย้อนหลัง

ข้อมูลสำคัญต้องมี Audit/History เพื่อรู้ว่า ใครทำอะไร เมื่อไหร่

7. หลักการพัฒนา

ให้ยึดหลัก:

Simple > Complex

อย่าสร้างระบบซับซ้อนโดยไม่มีเหตุผล

UI ใช้งานง่าย
ลดจำนวนขั้นตอนการทำงาน
ปุ่มและเมนูชัดเจน
รองรับ Touch Screen
รองรับ Mouse/Keyboard
Code แยกเป็น Module
API แยกจาก UI
Database Access ผ่าน Service/API
Realtime เป็นระบบกลาง
รองรับการขยายระบบในอนาคต
เน้น Stability และ Data Integrity
ห้ามทำ Feature ที่ไม่เกี่ยวข้องกับ POS ร้านอาหาร

ก่อนพัฒนา Feature ใด ให้ตรวจสอบ Architecture เดิมก่อน และ แก้ไขของเดิมให้น้อยที่สุดเท่าที่จำเป็น

เป้าหมายสุดท้าย:

Windows POS เป็นเครื่องมือหลักของร้าน
Web App เป็นช่องทางลูกค้าและบริหาร
Linux Server เป็นศูนย์กลางข้อมูลและ Realtime
ทั้งสามส่วนทำงานเป็นระบบเดียวกัน

หน้าเว็บของลูกค้า / หน้าเว็บของร้าน / หน้าเว็บของแอดมิน 


Web App สำหรับลูกค้า

คือเว็บไซต์ที่ลูกค้าใช้สั่งอาหาร

ลูกค้าเปิดเว็บแล้วเลือกเมนู

จ่ายเงินผ่านระบบ (**อันนี้ยังก่อนครับแค่เค้าออเดอร์มาให้ได้ก่อน)

Order จะถูกส่งไปยังเซิร์ฟเวอร์กลาง

POS และ Tablet ของร้านจะรับ Order แบบ Real-time

Web App สำหรับร้าน / ผู้จัดการ (Management)

คือเว็บไซต์ที่เจ้าของหรือผู้จัดการร้านใช้บริหารจัดการระบบ

ดูยอดขาย

จัดการเมนู

จัดการสต็อก

จัดการพนักงาน

ดูรายงาน

จัดการการตั้งค่าระบบ

ตั้งค่าการจัดส่ง / เดลิเวอรี

Admin / Super Admin

คือส่วนที่ใช้บริหารจัดการผู้ดูแลระบบ

สร้างผู้จัดการร้าน

กำหนดสิทธิ์

จัดการสิทธิ์ของพนักงานแต่ละคน

ตั้งค่าระบบภาพรวมของทุกร้าน

ระบบทำงานเชื่อมต่อกันดังนี้:

ลูกค้าสั่ง → Web App ลูกค้า → Linux Server → POS / Tablet / Kitchen

POS / Tablet ส่งข้อมูลการขายกลับไป → Linux Server → Web Management / Admin

ทุกอย่างวิ่งผ่าน Linux Server ซึ่งทำหน้าที่เป็นตัวกลาง:

Database กลาง

API

Realtime Notification

Authentication

Permission

Logging / History