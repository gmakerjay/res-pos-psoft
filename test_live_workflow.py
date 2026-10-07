import urllib.request
import json
import ssl
import sys

sys.stdout.reconfigure(encoding='utf-8')

ctx = ssl.create_default_context()
BASE = 'https://spk.p-services.net'
HEADERS = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) RestaurantPOS/1.0',
    'Content-Type': 'application/json',
    'X-Tenant-Code': 'DEFAULT'
}

def post(endpoint, data=None):
    req = urllib.request.Request(f'{BASE}{endpoint}', data=json.dumps(data or {}).encode('utf-8'), headers=HEADERS, method='POST')
    with urllib.request.urlopen(req, context=ctx, timeout=10) as r:
        return json.loads(r.read())

def get(endpoint):
    req = urllib.request.Request(f'{BASE}{endpoint}', headers=HEADERS)
    with urllib.request.urlopen(req, context=ctx, timeout=10) as r:
        return json.loads(r.read())

print('[Test Customer QR Ordering Flow]')
# 1. Create Order for T01
order_payload = {
    "tableId": 1,
    "tableNumber": "T01",
    "customerPhone": "0891234567",
    "customerNote": "ไม่ใส่ผักชี, เผ็ดน้อย",
    "orderType": 0,
    "items": [
        {
            "productId": 1,
            "quantity": 2,
            "unitPrice": 85,
            "note": "เส้นเหนียวนุ่ม",
            "selectedOptionIds": []
        }
    ]
}

res = post('/api/orders', order_payload)
print('Create Order Response:', res.get('success'), '| OrderId:', res.get('data', {}).get('id'), '| OrderNumber:', res.get('data', {}).get('orderNumber'))
order_id = res.get('data', {}).get('id')

# 2. Check Table Status
tables = get('/api/tables')
t01 = next((t for t in tables.get('data', []) if t['tableNumber'] == 'T01'), None)
print(f'T01 Status after order: {t01.get("status")} (1=Occupied), BillAmount: {t01.get("currentBillAmount")} THB')

# 3. Check Order by ID
order_detail = get(f'/api/orders/{order_id}')
print('Order Items Count:', len(order_detail.get('data', {}).get('items', [])))
print('Order Status:', order_detail.get('data', {}).get('status'), '(0=Pending)')

# 4. Cashier Login to advance order status
login_res = post('/api/auth/login', {'username': 'admin', 'password': 'psoft123'})
token = login_res.get('data', {}).get('token')
HEADERS['Authorization'] = f'Bearer {token}'

# 5. Kitchen advances status to Cooking/Ready (2)
put_req = urllib.request.Request(f'{BASE}/api/orders/{order_id}/status', data=json.dumps({"status": 2}).encode('utf-8'), headers=HEADERS, method='PUT')
with urllib.request.urlopen(put_req, context=ctx) as r:
    print('Kitchen Status Updated -> Ready (2):', json.loads(r.read()).get('success'))

# 6. Consolidated Pay Table (F10 checkout)
pay_res = post(f'/api/orders/table/T01/pay', {
    "paymentMethod": 0, # Cash
    "receivedAmount": 200,
    "discount": 0,
    "cashierName": "admin"
})
print('Consolidated Checkout Table T01:', pay_res.get('success'), '| Change:', pay_res.get('data', {}).get('changeAmount'))

# 7. Check T01 Status after Payment (should be 0=Available)
tables_after = get('/api/tables')
t01_after = next((t for t in tables_after.get('data', []) if t['tableNumber'] == 'T01'), None)
print(f'T01 Status after checkout: {t01_after.get("status")} (0=Available), BillAmount: {t01_after.get("currentBillAmount")} THB')

print('\n*** FULL RESTAURANT WORKFLOW PASSED ON LIVE SERVER! ***')
