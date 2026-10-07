import { getServerUrl, logError } from './logger';

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  errorCode?: string;
}

export interface TableItem {
  id: number;
  tableNumber: string;
  name: string;
  capacity: number;
  status: number; // 0=Available, 1=Occupied, 2=Reserved, 3=Billing
  currentOrderId?: number;
  currentBillAmount: number;
}

export interface CategoryItem {
  id: number;
  name: string;
  description?: string;
  sortOrder: number;
  productCount: number;
}

export interface ProductOptionItem {
  id: number;
  name: string;
  extraPrice: number;
}

export interface ProductOptionGroup {
  id: number;
  name: string;
  isRequired: boolean;
  allowMultiple: boolean;
  options: ProductOptionItem[];
}

export interface ProductItem {
  id: number;
  categoryId: number;
  categoryName: string;
  code: string;
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  isAvailable: boolean;
  outOfStockReason?: string;
  kitchenStation?: string;
  optionGroups: ProductOptionGroup[];
}

export interface IngredientItem {
  id: number;
  code: string;
  name: string;
  category: string;
  quantity: number;
  unit: string;
  minQuantityAlert: number;
  costPrice: number;
  lastRestockedAt?: string;
  notes?: string;
  isActive: boolean;
}

export interface OrderItemOption {
  id: number;
  groupName: string;
  optionName: string;
  extraPrice: number;
}

export interface OrderItem {
  id: number;
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
  specialNotes?: string;
  status: number;
  options: OrderItemOption[];
}

export interface Order {
  id: number;
  orderNumber: string;
  type: number; // 1=DineIn, 2=TakeAway, 3=Delivery
  tableId?: number;
  tableNumber?: string;
  customerName?: string;
  customerPhone?: string;
  status: number; // 1=New, 2=Accepted, 3=Preparing, 4=Ready, 5=Completed, 6=Cancelled
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  paidAmount: number;
  changeAmount: number;
  createdAt: string;
  notes?: string;
  items: OrderItem[];
}

export interface CreateOrderPayload {
  type: number;
  tableId?: number;
  tableNumber?: string;
  customerName?: string;
  customerPhone?: string;
  notes?: string;
  clientRequestId: string;
  items: {
    productId: number;
    quantity: number;
    specialNotes?: string;
    selectedOptionIds?: number[];
  }[];
}

export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: number;
  isActive: boolean;
  permissions: string[];
}

export interface LoginResponseData {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

export interface AuditLogItem {
  id: number;
  userId?: number;
  username: string;
  action: string;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface RegisterStorePayload {
  storeCode: string;
  storeName: string;
  ownerName: string;
  ownerPhone: string;
  email?: string;
  adminUsername?: string;
  adminPassword?: string;
  address?: string;
}

export interface StoreInfo {
  storeCode: string;
  storeName: string;
  ownerName: string;
  ownerPhone: string;
  address?: string;
  isActive: boolean;
  tableCount: number;
  productCount: number;
  createdAt?: string;
  expiresAt?: string;
  subscriptionPlan?: string;
  daysRemaining?: number;
  isExpired?: boolean;
  totalBillCount?: number;
  isPosOnline?: boolean;
  activePosCount?: number;
  openingHours?: string;
}

export interface TenantItem {
  id: number;
  storeCode: string;
  storeName: string;
  ownerName: string;
  ownerPhone: string;
  email?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
  expiresAt?: string;
  subscriptionPlan: string;
}

export interface GenerateKeyResponse {
  storeCode: string;
  plan: string;
  licenseKey: string;
  messageTemplate: string;
}

export function getStoredTenantCode(): string {
  if (typeof window !== 'undefined') {
    const params = new URLSearchParams(window.location.search);
    const tenantParam = params.get('store') || params.get('shop') || params.get('tenant') || params.get('code');
    if (tenantParam) {
      const normalized = tenantParam.trim().toUpperCase();
      localStorage.setItem('pos_tenant_code', normalized);
      return normalized;
    }
  }
  return localStorage.getItem('pos_tenant_code') || 'DEFAULT';
}

export function setStoredTenantCode(code: string) {
  const normalized = (code || 'DEFAULT').trim().toUpperCase();
  localStorage.setItem('pos_tenant_code', normalized);
}

export function getStoredToken(): string | null {
  return localStorage.getItem('pos_jwt_token');
}

export function getStoredUser(): AuthUser | null {
  const data = localStorage.getItem('pos_user');
  if (!data) return null;
  try {
    return JSON.parse(data);
  } catch {
    return null;
  }
}

export async function login(username: string, password: string): Promise<LoginResponseData> {
  const res = await request<LoginResponseData>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password })
  });
  localStorage.setItem('pos_jwt_token', res.token);
  localStorage.setItem('pos_user', JSON.stringify(res.user));
  return res;
}

export function logout() {
  localStorage.removeItem('pos_jwt_token');
  localStorage.removeItem('pos_user');
}

export async function registerStore(payload: RegisterStorePayload): Promise<StoreInfo> {
  const res = await request<StoreInfo>('/api/stores/register', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
  setStoredTenantCode(payload.storeCode);
  return res;
}

export async function getStoreInfo(): Promise<StoreInfo> {
  return request<StoreInfo>('/api/stores/info');
}

export async function checkStore(storeCode: string): Promise<StoreInfo> {
  return request<StoreInfo>(`/api/stores/check/${encodeURIComponent(storeCode.trim().toUpperCase())}`);
}

export async function generateStoreCode(): Promise<string> {
  const res = await request<{ code: string }>('/api/stores/generate-code');
  return res.code;
}

export async function getAllStores(): Promise<TenantItem[]> {
  return request<TenantItem[]>('/api/stores/all');
}

export async function upgradeStoreLicense(data: { storeCode: string; plan: string; extendDays: number }): Promise<TenantItem> {
  return request<TenantItem>('/api/stores/license/upgrade', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function activateStoreLicense(data: { storeCode: string; licenseKey: string }): Promise<TenantItem> {
  return request<TenantItem>('/api/stores/license/activate', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function generateLicenseKey(data: { storeCode: string; plan: string }): Promise<GenerateKeyResponse> {
  return request<GenerateKeyResponse>('/api/stores/license/generate-key', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function toggleStoreStatus(data: { storeCode: string; isActive: boolean }): Promise<TenantItem> {
  return request<TenantItem>('/api/stores/license/toggle-status', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export interface StoreStatusData {
  storeCode: string;
  isOnline: boolean;
  activePosCount: number;
  message: string;
  timestamp: string;
}

export async function simulateStoreStatus(storeCode: string, isOnline: boolean | null): Promise<StoreStatusData> {
  return request<StoreStatusData>('/api/stores/status/simulate', {
    method: 'POST',
    body: JSON.stringify({ storeCode, isOnline })
  });
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const url = `${getServerUrl()}${endpoint}`;
  const token = getStoredToken();
  const tenantCode = getStoredTenantCode();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Tenant-Code': tenantCode,
    ...(options.headers as Record<string, string> || {})
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  try {
    const res = await fetch(url, {
      ...options,
      headers
    });

    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || `HTTP ${res.status}: Request failed`);
    }
    return json.data;
  } catch (error: any) {
    logError(`API Error [${options.method || 'GET'} ${endpoint}]: ${error.message}`, error, 'Web-API');
    throw error;
  }
}

export async function checkServerHealth(customUrl?: string): Promise<boolean> {
  try {
    const base = customUrl ? customUrl.replace(/\/$/, '') : getServerUrl();
    const res = await fetch(`${base}/api/health`, { signal: AbortSignal.timeout(3000) });
    return res.ok;
  } catch {
    return false;
  }
}

export async function getTables(): Promise<TableItem[]> {
  return request<TableItem[]>('/api/tables');
}

export async function createTable(payload: { tableNumber: string; name?: string; capacity?: number }): Promise<TableItem> {
  return request<TableItem>('/api/tables', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export async function deleteTable(id: number): Promise<boolean> {
  return request<boolean>(`/api/tables/${id}`, {
    method: 'DELETE'
  });
}


export async function getCategories(): Promise<CategoryItem[]> {
  return request<CategoryItem[]>('/api/categories');
}

export async function getProducts(categoryId?: number): Promise<ProductItem[]> {
  const query = categoryId ? `?categoryId=${categoryId}` : '';
  return request<ProductItem[]>(`/api/products${query}`);
}

export async function createOrder(payload: CreateOrderPayload): Promise<Order> {
  return request<Order>('/api/orders', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export async function getOrder(orderId: number): Promise<Order> {
  return request<Order>(`/api/orders/${orderId}`);
}

export async function getActiveOrders(): Promise<Order[]> {
  return request<Order[]>('/api/orders/active');
}

export async function getActiveOrdersByTable(tableRef: string): Promise<Order[]> {
  return request<Order[]>(`/api/orders/table/${encodeURIComponent(tableRef)}`);
}

export async function trackOrdersByPhone(phone: string): Promise<Order[]> {
  return request<Order[]>(`/api/orders/track/${encodeURIComponent(phone.trim())}`);
}

export async function updateOrderStatus(orderId: number, status: number): Promise<Order> {
  return request<Order>(`/api/orders/${orderId}/status`, {
    method: 'PUT',
    body: JSON.stringify({ status })
  });
}

export async function uploadProductImage(file: File): Promise<string> {
  const url = `${getServerUrl()}/api/products/upload-image`;
  const token = getStoredToken();
  const formData = new FormData();
  formData.append('file', file);

  const headers: Record<string, string> = {};
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(url, {
    method: 'POST',
    headers,
    body: formData
  });

  const json = await res.json();
  if (!res.ok || !json.success) {
    throw new Error(json.message || 'Image upload failed');
  }
  return json.data;
}

export async function createCategory(data: { name: string; description?: string; imageUrl?: string; sortOrder?: number }): Promise<CategoryItem> {
  return request<CategoryItem>('/api/categories', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function updateCategory(id: number, data: { name: string; description?: string; imageUrl?: string; sortOrder?: number }): Promise<CategoryItem> {
  return request<CategoryItem>(`/api/categories/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
}

export async function deleteCategory(id: number): Promise<boolean> {
  return request<boolean>(`/api/categories/${id}`, {
    method: 'DELETE'
  });
}

export async function createProduct(data: Partial<ProductItem>): Promise<ProductItem> {
  return request<ProductItem>('/api/products', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function updateProduct(id: number, data: Partial<ProductItem>): Promise<ProductItem> {
  return request<ProductItem>(`/api/products/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
}

export async function deleteProduct(id: number): Promise<boolean> {
  return request<boolean>(`/api/products/${id}`, {
    method: 'DELETE'
  });
}

export async function getIngredients(category?: string): Promise<IngredientItem[]> {
  const query = category ? `?category=${encodeURIComponent(category)}` : '';
  return request<IngredientItem[]>(`/api/ingredients${query}`);
}

export async function createIngredient(data: Partial<IngredientItem>): Promise<IngredientItem> {
  return request<IngredientItem>('/api/ingredients', {
    method: 'POST',
    body: JSON.stringify(data)
  });
}

export async function updateIngredient(id: number, data: Partial<IngredientItem>): Promise<IngredientItem> {
  return request<IngredientItem>(`/api/ingredients/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
}

export async function deleteIngredient(id: number): Promise<boolean> {
  return request<boolean>(`/api/ingredients/${id}`, {
    method: 'DELETE'
  });
}

export async function adjustIngredientStock(id: number, changeQuantity: number, reason: string): Promise<IngredientItem> {
  return request<IngredientItem>(`/api/ingredients/${id}/adjust`, {
    method: 'POST',
    body: JSON.stringify({ changeQuantity, reason })
  });
}

export async function adjustStock(productId: number, quantityChange: number, reason: string): Promise<any> {
  return request<any>(`/api/products/${productId}/stock`, {
    method: 'POST',
    body: JSON.stringify({ quantityChange, reason })
  });
}

export async function getAuditLogs(limit: number = 50): Promise<AuditLogItem[]> {
  return request<AuditLogItem[]>(`/api/audit?limit=${limit}`);
}

export async function getDailyReport(date?: string): Promise<any> {
  const query = date ? `?date=${date}` : '';
  return request<any>(`/api/reports/daily${query}`);
}
