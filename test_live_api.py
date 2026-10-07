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

print('[1] Health Check:')
print(get('/api/health'))

print('\n[2] Login admin / psoft123:')
login_res = post('/api/auth/login', {'username': 'admin', 'password': 'psoft123'})
print('Success:', login_res.get('success'), 'User:', login_res.get('data', {}).get('user', {}).get('username'))
token = login_res.get('data', {}).get('token')
HEADERS['Authorization'] = f'Bearer {token}'

print('\n[3] Get Tables:')
tables = get('/api/tables')
t_list = tables.get('data', [])
print(f'Count: {len(t_list)}, Tables: {[t["tableNumber"] for t in t_list]}')

print('\n[4] Get Products:')
products = get('/api/products')
p_list = products.get('data', [])
print(f'Count: {len(p_list)}, Sample: {[p["name"] for p in p_list[:4]]}')

print('\n[5] Get Categories:')
cats = get('/api/categories')
c_list = cats.get('data', [])
print(f'Count: {len(c_list)}, Sample: {[c["name"] for c in c_list[:4]]}')

print('\n[6] Get Ingredients (Raw Materials):')
ing = get('/api/ingredients')
i_list = ing.get('data', [])
print(f'Count: {len(i_list)}, Sample: {[i["name"] for i in i_list[:4]]}')

print('\n[7] Multi-Tenant Stores Info:')
stores = get('/api/stores/info')
print('Store Info:', stores.get('data', {}).get('storeName'), '| Plan:', stores.get('data', {}).get('subscriptionPlan'))

print('\nALL API ENDPOINTS TESTED SUCCESSFULLY AGAINST LIVE SERVER!')
