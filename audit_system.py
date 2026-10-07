import urllib.request
import urllib.error
import json
import ssl
import sys
import os
import re

sys.stdout.reconfigure(encoding='utf-8')

ctx = ssl.create_default_context()
BASE = 'https://spk.p-services.net'

def make_request(endpoint, method='GET', data=None, headers=None):
    hdrs = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) RestaurantPOS-Audit/1.0',
        'Content-Type': 'application/json'
    }
    if headers:
        hdrs.update(headers)
    encoded_data = json.dumps(data).encode('utf-8') if data is not None else None
    req = urllib.request.Request(f'{BASE}{endpoint}', data=encoded_data, headers=hdrs, method=method)
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=15) as r:
            return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode('utf-8', errors='replace')
        try:
            return e.code, json.loads(body)
        except Exception:
            return e.code, {'raw': body}
    except Exception as e:
        return 0, {'error': str(e)}

passed_count = 0
failed_count = 0

def check(test_name, condition, details=""):
    global passed_count, failed_count
    if condition:
        passed_count += 1
        print(f"[PASS] {test_name} {('- ' + details) if details else ''}")
    else:
        failed_count += 1
        print(f"[FAIL] {test_name} - {details}")

print("======================================================================")
print("     RESTAURANT POS SYSTEM — COMPREHENSIVE PRODUCTION AUDIT")
print("     Target: https://spk.p-services.net")
print("======================================================================")

# 1. Health Check
status, res = make_request('/api/health')
check("API Health Check", status == 200 and res.get('status') == 'Healthy', f"Status: {res.get('status')}")

# 2. Demo Store Default Credentials & Auth Fallback
status, res = make_request('/api/auth/login', 'POST', {'username': 'admin', 'password': 'psoft123'}, {'X-Tenant-Code': 'DEFAULT'})
check("Demo Store Login (psoft123)", status == 200 and res.get('success') is True, f"User: {res.get('data', {}).get('user', {}).get('username')}")
admin_token = res.get('data', {}).get('token')

status, res = make_request('/api/auth/login', 'POST', {'username': 'admin', 'password': '123456'}, {'X-Tenant-Code': 'DEFAULT'})
check("Demo Store Login Fallback (123456)", status == 200 and res.get('success') is True)

# 3. Master Tenant Info & Products in Demo Store
status, res = make_request('/api/products', 'GET', headers={'X-Tenant-Code': 'DEFAULT'})
products = res.get('data', [])
check("Demo Store Menu Products", status == 200 and len(products) >= 10, f"Found {len(products)} products")

status, res = make_request('/api/tables', 'GET', headers={'X-Tenant-Code': 'DEFAULT'})
tables = res.get('data', [])
check("Demo Store Tables", status == 200 and len(tables) >= 8, f"Found {len(tables)} tables")

# 4. Duplicate Store Code Rejection Test (Edit 17 Feature)
status, res = make_request('/api/stores/register', 'POST', {
    "storeCode": "DEFAULT",
    "storeName": "ร้านจำลองที่ซ้ำ",
    "adminUsername": "testadmin",
    "adminPassword": "password123",
    "phone": "0811111111",
    "branchName": "สาขาทดสอบ"
})
check("Duplicate Store Code Guard (409 Conflict)", status == 409, f"Received HTTP {status} (Expected 409)")

# 5. New Store Factory Clean State Verification (Edit 17 Feature)
# Generate unique store code
import random
import string
test_code = "TEST" + "".join(random.choices(string.ascii_uppercase + string.digits, k=4))
reg_status, reg_res = make_request('/api/stores/register', 'POST', {
    "storeCode": test_code,
    "storeName": f"ร้านทดสอบระบบใหม่ {test_code}",
    "adminUsername": "storeowner",
    "adminPassword": "psoft123password",
    "phone": "0899998888",
    "branchName": "สาขาเพิ่งเปิด"
})
check(f"Register New Store ({test_code})", reg_status == 200 and reg_res.get('success') is True, f"Store: {test_code}")

if reg_status == 200:
    # Check that new store has 0 products, 0 categories, 0 tables, 0 ingredients (100% Factory Clean)
    s_prod, res_prod = make_request('/api/products', 'GET', headers={'X-Tenant-Code': test_code})
    prod_count = len(res_prod.get('data', []))
    check("New Store Factory Clean Products", s_prod == 200 and prod_count == 0, f"Products count: {prod_count} (Expected 0)")

    s_cat, res_cat = make_request('/api/categories', 'GET', headers={'X-Tenant-Code': test_code})
    cat_count = len(res_cat.get('data', []))
    check("New Store Factory Clean Categories", s_cat == 200 and cat_count == 0, f"Categories count: {cat_count} (Expected 0)")

    s_tbl, res_tbl = make_request('/api/tables', 'GET', headers={'X-Tenant-Code': test_code})
    tbl_count = len(res_tbl.get('data', []))
    check("New Store Factory Clean Tables", s_tbl == 200 and tbl_count == 0, f"Tables count: {tbl_count} (Expected 0)")

    s_ing, res_ing = make_request('/api/ingredients', 'GET', headers={'X-Tenant-Code': test_code})
    ing_count = len(res_ing.get('data', []))
    check("New Store Factory Clean Raw Ingredients", s_ing == 200 and ing_count == 0, f"Ingredients count: {ing_count} (Expected 0)")

    # Verify new store admin login works
    s_login, res_login = make_request('/api/auth/login', 'POST', {'username': 'storeowner', 'password': 'psoft123password'}, {'X-Tenant-Code': test_code})
    check("New Store Admin Login", s_login == 200 and res_login.get('success') is True)

# 6. Customer Order Lifecycle & Phone Tracking in DEFAULT store
order_phone = "0819998888"
s_order, res_order = make_request('/api/orders', 'POST', {
    "tableId": 2,
    "tableNumber": "T02",
    "customerPhone": order_phone,
    "customerNote": "เผ็ดกลาง ไม่ใส่ชูรส",
    "orderType": 0,
    "items": [
        {
            "productId": 1,
            "quantity": 1,
            "unitPrice": 85,
            "note": "ออเดอร์ทดสอบระบบ",
            "selectedOptionIds": []
        }
    ]
}, {'X-Tenant-Code': 'DEFAULT'})

check("Customer QR Order Creation", s_order in (200, 201) and res_order.get('success') is True, f"Order: {res_order.get('data', {}).get('orderNumber')}")
audit_order_id = res_order.get('data', {}).get('id')

# Track order by phone (Edit 14 Feature)
s_track, res_track = make_request(f'/api/orders/track/{order_phone}', 'GET', headers={'X-Tenant-Code': 'DEFAULT'})
orders_tracked = res_track.get('data', [])
found_order = any(o.get('id') == audit_order_id for o in orders_tracked)
check("Phone-based Order Tracking Endpoint", s_track == 200 and found_order, f"Found {len(orders_tracked)} orders for {order_phone}")

# Kitchen status advance to Cooking (1) -> Ready (2)
s_status, res_status = make_request(f'/api/orders/{audit_order_id}/status', 'PUT', {"status": 2}, {
    'X-Tenant-Code': 'DEFAULT',
    'Authorization': f'Bearer {admin_token}'
})
check("Kitchen Status Update (Ready)", s_status == 200 and res_status.get('success') is True)

# Table consolidated checkout (F10)
s_pay, res_pay = make_request('/api/orders/table/T02/pay', 'POST', {
    "paymentMethod": 0,
    "receivedAmount": 100,
    "discount": 0,
    "cashierName": "admin"
}, {
    'X-Tenant-Code': 'DEFAULT',
    'Authorization': f'Bearer {admin_token}'
})
check("Consolidated Table Checkout (F10)", s_pay == 200 and res_pay.get('success') is True, f"Change: {res_pay.get('data', {}).get('changeAmount')} THB")

# Verify Table T02 is Available (0)
s_tbl2, res_tbl2 = make_request('/api/tables', 'GET', headers={'X-Tenant-Code': 'DEFAULT'})
t02 = next((t for t in res_tbl2.get('data', []) if t.get('tableNumber') == 'T02'), None)
check("Table T02 Reverted to Available State", t02 is not None and t02.get('status') == 0 and t02.get('currentBillAmount') == 0)

# 7. Developer Security Gate (Edit 15 Feature)
s_dev_unauth, _ = make_request('/api/stores/all', 'GET')
check("Developer Stores List Endpoint Blocks Unauthorized Access", s_dev_unauth in (401, 403), f"HTTP {s_dev_unauth}")

s_dev_auth, res_dev = make_request('/api/stores/all', 'GET', headers={
    'X-Dev-Key': 'dev2026'
})
check("Developer Stores List with Valid Dev Key Header", s_dev_auth == 200 and len(res_dev.get('data', [])) >= 1, f"Stores count: {len(res_dev.get('data', []))}")

# 8. Client Binaries Verification
base_dir = r"c:\Users\admin\Documents\res-pos_psoft"
client_exe = os.path.join(base_dir, "build_output", "client_pc", "RestaurantPOS.Wpf.exe")
client_release_exe = os.path.join(base_dir, "Client_POS_Release", "RestaurantPOS.Wpf.exe")
server_dll = os.path.join(base_dir, "build_output", "web_server", "RestaurantPOS.Server.dll")

check("Desktop POS Release EXE in build_output/client_pc", os.path.isfile(client_exe), f"Size: {os.path.getsize(client_exe):,} bytes")
check("Desktop POS Release EXE in Client_POS_Release", os.path.isfile(client_release_exe), f"Size: {os.path.getsize(client_release_exe):,} bytes")
check("Server DLL in build_output/web_server", os.path.isfile(server_dll), f"Size: {os.path.getsize(server_dll):,} bytes")

# 9. Strict Rule: Check NO Prohibited Emojis in Source Code (UI/Views)
emoji_pattern = re.compile(r'[\U00010000-\U0010ffff]', flags=re.UNICODE)
emoji_violating_files = []

scan_dirs = [
    os.path.join(base_dir, "client_pc", "RestaurantPOS.Wpf", "Views"),
    os.path.join(base_dir, "web_server", "RestaurantPOS.Web", "src")
]

for s_dir in scan_dirs:
    for root, _, files in os.walk(s_dir):
        for f in files:
            if f.endswith(('.xaml', '.cs', '.tsx', '.ts')):
                f_path = os.path.join(root, f)
                try:
                    with open(f_path, 'r', encoding='utf-8') as fp:
                        content = fp.read()
                        if emoji_pattern.search(content):
                            emoji_violating_files.append(os.path.relpath(f_path, base_dir))
                except Exception:
                    pass

check("Strict Rule 1: NO Emojis in Program Code/UI", len(emoji_violating_files) == 0, f"Violations: {emoji_violating_files if emoji_violating_files else 'Zero (100% Clean)'}")

print("======================================================================")
print(f"AUDIT SUMMARY: {passed_count} PASSED, {failed_count} FAILED")
print("======================================================================")

if failed_count == 0:
    print("ALL VERIFICATIONS COMPLETED SUCCESSFULLY WITH ZERO ERRORS.")
    sys.exit(0)
else:
    print(f"AUDIT FAILED WITH {failed_count} ERRORS.")
    sys.exit(1)
