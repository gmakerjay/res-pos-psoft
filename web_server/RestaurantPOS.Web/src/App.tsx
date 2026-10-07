import React, { useState, useEffect } from 'react';
import { 
  getCategories, createCategory, updateCategory, deleteCategory,
  getProducts, createProduct, updateProduct, deleteProduct, createOrder, getActiveOrders, 
  updateOrderStatus, getDailyReport, checkServerHealth, login, logout, 
  getStoredUser, uploadProductImage, getIngredients, createIngredient,
  updateIngredient, deleteIngredient, adjustIngredientStock, getAuditLogs,
  getStoredTenantCode, setStoredTenantCode, getStoreInfo, registerStore, checkStore,
  simulateStoreStatus
} from './services/api';
import type { 
  CategoryItem, ProductItem, IngredientItem, Order, CreateOrderPayload, 
  AuthUser, AuditLogItem, StoreInfo
} from './services/api';
import { realtimeService } from './services/realtime';
import type { ConnectionState } from './services/realtime';
import { getServerUrl, setServerUrl, logError, initGlobalErrorHandlers } from './services/logger';
import { 
  playOrderAlertChime, playOrderReadyChime, 
  getSoundVolume, setSoundVolume, isSoundEnabled, setSoundEnabled, testOrderAlertSound 
} from './services/sound';
import { ErrorBoundary } from './components/ErrorBoundary';
import { SoftwareLandingView } from './components/SoftwareLandingView';
import { TableQrManagerModal } from './components/TableQrManagerModal';
import { DeveloperLicensePortalModal } from './components/DeveloperLicensePortalModal';
import { ActivateLicenseModal } from './components/ActivateLicenseModal';

// Initialize global logger error handlers
initGlobalErrorHandlers();

export function App() {
  const [activeTab, setActiveTab] = useState<'customer' | 'kitchen' | 'manage'>('customer');
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const [showServerModal, setShowServerModal] = useState<boolean>(false);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(getStoredUser());
  const [showLoginModal, setShowLoginModal] = useState<boolean>(false);
  const [loginModalStoreCode, setLoginModalStoreCode] = useState<string>('');
  const [pendingTab, setPendingTab] = useState<'kitchen' | 'manage' | null>(null);

  // Multi-Tenant Store States
  const [storeInfo, setStoreInfo] = useState<StoreInfo | null>(null);
  const [isStorePosOnline, setIsStorePosOnline] = useState<boolean>(true);
  const [activePosTerminals, setActivePosTerminals] = useState<number>(0);
  const [showRegisterStoreModal, setShowRegisterStoreModal] = useState<boolean>(false);
  const [showSwitchStoreModal, setShowSwitchStoreModal] = useState<boolean>(false);
  const [showTableQrModal, setShowTableQrModal] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const p = new URLSearchParams(window.location.search);
    return p.get('view') === 'qr' || p.get('qr') === '1';
  });
  const [showDeveloperLicenseModal, setShowDeveloperLicenseModal] = useState<boolean>(false);
  const [showActivateLicenseModal, setShowActivateLicenseModal] = useState<boolean>(false);

  // Check if current visitor has scanned a table QR or opened a direct store link
  const [sessionParams] = useState(() => {
    if (typeof window === 'undefined') return { isCustomer: false, store: '', table: '', type: '' };
    const p = new URLSearchParams(window.location.search);
    const store = (p.get('store') || p.get('shop') || p.get('tenant') || p.get('code') || '').trim().toUpperCase();
    const table = (p.get('table') || '').trim().toUpperCase();
    const type = (p.get('type') || '').trim().toLowerCase();
    const demo = p.get('demo') === '1' || p.get('demo') === 'true' || p.get('view') === 'demo';
    const isCustomer = Boolean(store || table || type === 'takeaway' || demo);
    return { isCustomer, store, table, type };
  });

  const [isDemoViewActive, setIsDemoViewActive] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const p = new URLSearchParams(window.location.search);
    return Boolean(p.get('store') || p.get('table') || p.get('type') || p.get('demo') === '1' || p.get('view') === 'demo');
  });

  const isCustomerSession = sessionParams.isCustomer || isDemoViewActive;

  const loadStoreInfo = async () => {
    try {
      const info = await getStoreInfo();
      setStoreInfo(info);
      if (typeof info.isPosOnline === 'boolean') {
        setIsStorePosOnline(info.isPosOnline);
      }
      if (typeof info.activePosCount === 'number') {
        setActivePosTerminals(info.activePosCount);
      }
    } catch (e) {
      console.warn('Could not load store info', e);
    }
  };

  useEffect(() => {
    loadStoreInfo();
  }, []);

  // Initialize Realtime & Listeners
  useEffect(() => {
    realtimeService.start();
    const unsubState = realtimeService.onStateChange(setConnectionState);
    const unsubStoreStatus = realtimeService.onStoreStatusChanged((data) => {
      const currentTenant = getStoredTenantCode() || 'DEFAULT';
      if (data.storeCode.toUpperCase() === currentTenant.toUpperCase()) {
        setIsStorePosOnline(data.isOnline);
        setActivePosTerminals(data.activePosCount);
      }
    });

    return () => {
      unsubState();
      unsubStoreStatus();
      realtimeService.stop();
    };
  }, []);

  const handleToggleDemoOnline = async () => {
    const code = getStoredTenantCode() || 'DEFAULT';
    const nextStatus = !isStorePosOnline;
    try {
      await simulateStoreStatus(code, nextStatus);
      setIsStorePosOnline(nextStatus);
    } catch (e: any) {
      alert('ไม่สามารถสลับสถานะจำลองได้: ' + e.message);
    }
  };

  const handleTabChange = (tab: 'customer' | 'kitchen' | 'manage') => {
    if (tab === 'kitchen' || tab === 'manage') {
      if (!currentUser) {
        setPendingTab(tab);
        setShowLoginModal(true);
        return;
      }
    }
    setActiveTab(tab);
  };

  const handleLoginSuccess = (user: AuthUser) => {
    setCurrentUser(user);
    setShowLoginModal(false);
    loadStoreInfo();
    if (pendingTab) {
      setActiveTab(pendingTab);
      setPendingTab(null);
    } else {
      setActiveTab('manage');
    }
  };

  const handleLogout = () => {
    logout();
    setCurrentUser(null);
    if (activeTab !== 'customer') {
      setActiveTab('customer');
    }
  };

  return (
    <ErrorBoundary>
      <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', width: '100%', overflowX: 'hidden' }}>
        {/* Case 1: Software Portal Header (When NOT logged in and on Root Domain without QR/store params) */}
        {!currentUser && !isCustomerSession && (
          <header className="pos-header-portal">
            <div className="pos-header-portal-top">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontWeight: 'bold', fontSize: '17px', letterSpacing: '0.5px', whiteSpace: 'nowrap' }}>
                  RESTAURANT POS
                </span>
                <span style={{
                  fontSize: '11px',
                  background: 'rgba(255,255,255,0.2)',
                  padding: '2px 7px',
                  borderRadius: '4px',
                  whiteSpace: 'nowrap'
                }}>
                  ระบบจัดการร้านอาหาร
                </span>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '5px', fontSize: '11px', whiteSpace: 'nowrap' }}>
                <span style={{
                  width: '8px',
                  height: '8px',
                  borderRadius: '50%',
                  backgroundColor: connectionState === 'connected' ? '#00E676' : (connectionState === 'reconnecting' ? '#FFC107' : '#FF5252')
                }} />
                <span>
                  {connectionState === 'connected' ? '[Online]' : (connectionState === 'reconnecting' ? '[Reconnecting]' : '[Offline]')}
                </span>
              </div>
            </div>

            <div className="pos-header-portal-actions">
              <button
                onClick={() => {
                  setStoredTenantCode('DEFAULT');
                  window.history.pushState({}, '', '/?store=DEFAULT&table=T01');
                  setIsDemoViewActive(true);
                }}
                style={{
                  backgroundColor: '#FFD54F',
                  color: '#0D47A1',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '4px',
                  fontSize: '12px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  boxShadow: '0 2px 4px rgba(0,0,0,0.2)',
                  whiteSpace: 'nowrap'
                }}
                title="เปิดดูหน้าร้านอาหารตัวอย่างเพื่อสาธิตการสั่งอาหารและใช้งานจริง"
              >
                [ ดูตัวอย่างระบบจริง (Live Demo) ]
              </button>

              <button
                onClick={() => setShowActivateLicenseModal(true)}
                style={{
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  border: 'none',
                  padding: '5px 10px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
                title="กรอกรหัส Activation Key เพื่อปลดล็อกสิทธิ์ใช้งานร้าน"
              >
                [เปิดใช้งานคีย์]
              </button>

              <button
                onClick={() => setShowDeveloperLicenseModal(true)}
                style={{
                  backgroundColor: 'rgba(255,255,255,0.15)',
                  color: '#FFF',
                  border: '1px solid rgba(255,255,255,0.3)',
                  padding: '5px 10px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
                title="ศูนย์จัดการคีย์และสิทธิ์ร้านค้าทั้งหมด (SuperAdmin)"
              >
                [คีย์นักพัฒนา KeyGen]
              </button>

              <button
                onClick={() => {
                  setLoginModalStoreCode('');
                  setShowLoginModal(true);
                }}
                style={{
                  backgroundColor: 'rgba(255,255,255,0.15)',
                  color: '#FFF',
                  border: '1px solid rgba(255,255,255,0.4)',
                  padding: '5px 12px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
              >
                เข้าสู่ระบบร้านค้า
              </button>
            </div>
          </header>
        )}

        {/* Case 2: Customer Ordering Header (When customer visits via QR code or direct store link) */}
        {!currentUser && isCustomerSession && (
          <header className="pos-header-customer">
            {/* Row 1: Brand & Table Context + Live Server/Store Status */}
            <div className="pos-header-customer-row1">
              <div className="pos-header-customer-brand">
                <a
                  href="/"
                  style={{
                    color: '#FFF',
                    textDecoration: 'none',
                    fontWeight: 'bold',
                    fontSize: '13.5px',
                    whiteSpace: 'nowrap',
                    padding: '2px 6px',
                    backgroundColor: 'rgba(255,255,255,0.15)',
                    borderRadius: '4px'
                  }}
                  title="กลับหน้าหลักซอฟต์แวร์"
                >
                  POS
                </a>
                <span style={{
                  backgroundColor: '#1565C0',
                  padding: '3px 8px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  color: '#FFEB3B',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  maxWidth: '180px'
                }}>
                  [ร้าน: {storeInfo ? storeInfo.storeName : (getStoredTenantCode() === 'DEFAULT' ? 'ร้านอาหารตัวอย่าง (สาขาหลัก)' : getStoredTenantCode())}]
                </span>
                <span style={{
                  backgroundColor: '#2E7D32',
                  padding: '3px 8px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  whiteSpace: 'nowrap'
                }}>
                  {sessionParams.type === 'takeaway' ? 'สั่งกลับบ้าน' : (sessionParams.table ? `โต๊ะ ${sessionParams.table}` : 'สั่งออนไลน์')}
                </span>
              </div>

              <div className="pos-header-customer-status">
                {/* Real-Time Client PC (POS) Presence Badge */}
                <div style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '5px',
                  backgroundColor: isStorePosOnline ? '#2E7D32' : '#C62828',
                  color: '#FFF',
                  padding: '3px 8px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 'bold',
                  whiteSpace: 'nowrap'
                }}>
                  <span style={{
                    width: '6px',
                    height: '6px',
                    borderRadius: '50%',
                    backgroundColor: '#FFF'
                  }} />
                  <span>{isStorePosOnline ? '[หน้าร้าน: เปิดรับออเดอร์]' : '[หน้าร้าน: ยังไม่เปิดรับออเดอร์]'}</span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '4px', fontSize: '11px', whiteSpace: 'nowrap' }}>
                  <span style={{
                    width: '7px',
                    height: '7px',
                    borderRadius: '50%',
                    backgroundColor: connectionState === 'connected' ? '#00E676' : '#FF5252'
                  }} />
                  <span style={{ opacity: 0.85 }}>{connectionState === 'connected' ? '[Server: Online]' : '[Server: Offline]'}</span>
                </div>
              </div>
            </div>

            {/* Row 2: Live Demo Switcher & Navigation Actions (Cleanly separated on mobile, aligned on desktop) */}
            <div className="pos-header-customer-row2">
              {/* Demo Mode Toggle Button (Shows for DEFAULT store or demo session) */}
              {(getStoredTenantCode() === 'DEFAULT' || isDemoViewActive) && (
                <button
                  type="button"
                  onClick={handleToggleDemoOnline}
                  style={{
                    backgroundColor: isStorePosOnline ? '#E65100' : '#2E7D32',
                    color: '#FFF',
                    border: '1px solid rgba(255,255,255,0.4)',
                    padding: '4px 8px',
                    borderRadius: '4px',
                    fontSize: '11px',
                    fontWeight: 'bold',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap'
                  }}
                  title="คลิกเพื่อจำลองสลับสถานะร้านเปิด / ร้านปิด แบบ Real-Time สำหรับการนำเสนองานขาย"
                >
                  [ จำลอง: {isStorePosOnline ? 'สลับเป็นร้านปิด' : 'สลับเป็นร้านเปิด'} ]
                </button>
              )}

              <button
                type="button"
                onClick={() => {
                  setLoginModalStoreCode(getStoredTenantCode() || 'DEFAULT');
                  setShowLoginModal(true);
                }}
                style={{
                  backgroundColor: 'rgba(255,255,255,0.18)',
                  color: '#FFF',
                  border: '1px solid rgba(255,255,255,0.4)',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
                title="ทดลองเข้าสู่ระบบจัดการและจอครัว KDS (admin / psoft123)"
              >
                [ ลองเข้าจอครัว &amp; จัดการร้าน ]
              </button>

              <button
                type="button"
                onClick={() => {
                  setIsDemoViewActive(false);
                  window.history.pushState({}, '', '/');
                }}
                style={{
                  backgroundColor: '#FFEB3B',
                  color: '#0D47A1',
                  border: 'none',
                  padding: '4px 10px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
                  whiteSpace: 'nowrap'
                }}
                title="กลับสู่หน้าหลักแนะนำซอฟต์แวร์และลงทะเบียนร้านค้า"
              >
                [ กลับหน้าหลักซอฟต์แวร์ ]
              </button>
            </div>
          </header>
        )}

        {/* Case 3: Authenticated Staff Header (When staff / manager is logged in) */}
        {currentUser && (
          <header style={{
            backgroundColor: '#0D47A1',
            color: '#FFF',
            padding: '10px 16px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
            width: '100%',
            flexWrap: 'wrap',
            gap: '8px',
            boxSizing: 'border-box'
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span style={{ fontWeight: 'bold', fontSize: '16px', letterSpacing: '0.5px', whiteSpace: 'nowrap' }}>
                RESTAURANT POS
              </span>
              <div
                onClick={() => setShowSwitchStoreModal(true)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '4px',
                  backgroundColor: '#1565C0',
                  padding: '3px 8px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  color: '#FFEB3B',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
                title="คลิกเพื่อสลับร้านค้า"
              >
                <span>[ร้าน: {storeInfo ? storeInfo.storeName : getStoredTenantCode()}]</span>
              </div>

              {storeInfo && (
                <div
                  onClick={() => setShowActivateLicenseModal(true)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    backgroundColor: storeInfo.subscriptionPlan === 'FullLifetime' ? '#2E7D32' : (storeInfo.isExpired ? '#C62828' : '#E65100'),
                    padding: '3px 8px',
                    borderRadius: '4px',
                    fontSize: '11px',
                    fontWeight: 'bold',
                    color: '#FFF',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap'
                  }}
                  title="คลิกเพื่อกรอกรหัสเปิดใช้งาน (Activation Key)"
                >
                  {storeInfo.subscriptionPlan === 'FullLifetime'
                    ? '[เวอร์ชันเต็ม ตลอดชีพ]'
                    : (storeInfo.isExpired
                        ? '[หมดอายุทดลองใช้ (คลิกต่อสิทธิ์)]'
                        : `[ทดลองใช้: เหลือ ${storeInfo.daysRemaining ?? 14} วัน]`)}
                </div>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <button
                onClick={() => setShowDeveloperLicenseModal(true)}
                style={{
                  backgroundColor: 'rgba(255,255,255,0.15)',
                  color: '#FFD54F',
                  border: '1px solid rgba(255,255,255,0.3)',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
                title="ศูนย์จัดการคีย์และสิทธิ์ร้านค้าทั้งหมด (SuperAdmin)"
              >
                [คีย์นักพัฒนา KeyGen]
              </button>
              <span style={{ fontSize: '12px', backgroundColor: 'rgba(255,255,255,0.2)', padding: '3px 8px', borderRadius: '4px', whiteSpace: 'nowrap' }}>
                {currentUser.fullName}
              </span>
              <button
                onClick={() => setShowServerModal(true)}
                style={{
                  backgroundColor: 'rgba(255,255,255,0.15)',
                  color: '#FFF',
                  border: '1px solid rgba(255,255,255,0.3)',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
              >
                ตั้งค่าเซิร์ฟเวอร์
              </button>
              <button
                onClick={handleLogout}
                style={{
                  backgroundColor: '#C62828',
                  color: '#FFF',
                  border: 'none',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  fontSize: '11px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  whiteSpace: 'nowrap'
                }}
              >
                ออกจากระบบ
              </button>
            </div>
          </header>
        )}

        {/* Staff Navigation Tabs Bar (Visible ONLY after staff logs in) */}
        {currentUser && (
          <nav style={{
            backgroundColor: '#FFF',
            borderBottom: '1px solid #DEE2E6',
            padding: '0 12px',
            display: 'flex',
            gap: '6px',
            overflowX: 'auto',
            whiteSpace: 'nowrap'
          }}>
            <button
              onClick={() => handleTabChange('kitchen')}
              style={{
                padding: '10px 14px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'kitchen' ? '3px solid #1976D2' : '3px solid transparent',
                color: activeTab === 'kitchen' ? '#1976D2' : '#6C757D',
                fontWeight: activeTab === 'kitchen' ? 'bold' : 'normal',
                fontSize: '13px',
                cursor: 'pointer',
                whiteSpace: 'nowrap'
              }}
            >
              [ สำหรับครัว ] จอแสดงออเดอร์ (KDS)
            </button>
            <button
              onClick={() => handleTabChange('manage')}
              style={{
                padding: '10px 14px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'manage' ? '3px solid #1976D2' : '3px solid transparent',
                color: activeTab === 'manage' ? '#1976D2' : '#6C757D',
                fontWeight: activeTab === 'manage' ? 'bold' : 'normal',
                fontSize: '13px',
                cursor: 'pointer',
                whiteSpace: 'nowrap'
              }}
            >
              [ สำหรับผู้จัดการ ] สรุปรายงาน &amp; จัดการระบบ
            </button>
            <button
              onClick={() => handleTabChange('customer')}
              style={{
                padding: '10px 14px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'customer' ? '3px solid #1976D2' : '3px solid transparent',
                color: activeTab === 'customer' ? '#1976D2' : '#6C757D',
                fontWeight: activeTab === 'customer' ? 'bold' : 'normal',
                fontSize: '13px',
                cursor: 'pointer',
                whiteSpace: 'nowrap'
              }}
            >
              [ เมนูสั่งอาหารลูกค้า ]
            </button>
            <button
              onClick={() => setShowTableQrModal(true)}
              style={{
                marginLeft: 'auto',
                alignSelf: 'center',
                padding: '6px 14px',
                borderRadius: '4px',
                border: '1px solid #0D47A1',
                backgroundColor: '#0D47A1',
                color: '#FFF',
                fontWeight: 'bold',
                fontSize: '12.5px',
                cursor: 'pointer',
                whiteSpace: 'nowrap'
              }}
            >
              [ พิมพ์ป้าย QR Code โต๊ะ ]
            </button>
          </nav>

        )}

        {/* Main Tab Content */}
        <main style={{
          flex: 1,
          padding: (!currentUser && !isCustomerSession) ? '0' : '14px',
          maxWidth: '1200px',
          margin: '0 auto',
          width: '100%',
          boxSizing: 'border-box'
        }}>
          {!currentUser && !isCustomerSession && (
            <SoftwareLandingView
              onOpenLogin={(code) => {
                if (code) setLoginModalStoreCode(code);
                setShowLoginModal(true);
              }}
              onStoreCreated={(st) => {
                setStoreInfo(st);
                setStoredTenantCode(st.storeCode);
              }}
              onEnterDemo={(tableOrType) => {
                setStoredTenantCode('DEFAULT');
                if (tableOrType === 'takeaway') {
                  window.history.pushState({}, '', '/?store=DEFAULT&type=takeaway');
                } else {
                  window.history.pushState({}, '', `/?store=DEFAULT&table=${tableOrType || 'T01'}`);
                }
                setIsDemoViewActive(true);
              }}
            />
          )}

          {!currentUser && isCustomerSession && (
            <CustomerView
              storeInfo={storeInfo}
              isStorePosOnline={isStorePosOnline}
              activePosTerminals={activePosTerminals}
              onToggleDemoOnline={handleToggleDemoOnline}
            />
          )}
          {currentUser && activeTab === 'customer' && (
            <CustomerView
              storeInfo={storeInfo}
              isStorePosOnline={isStorePosOnline}
              activePosTerminals={activePosTerminals}
              onToggleDemoOnline={handleToggleDemoOnline}
            />
          )}
          {currentUser && activeTab === 'kitchen' && <KitchenView />}
          {currentUser && activeTab === 'manage' && <ManagementView />}
        </main>

        {/* Login Modal */}
        {showLoginModal && (
          <LoginModal 
            initialStoreCode={loginModalStoreCode}
            onSuccess={handleLoginSuccess} 
            onClose={() => {
              setShowLoginModal(false);
              setPendingTab(null);
            }} 
          />
        )}

        {/* Server IP / Domain Config Modal */}
        {showServerModal && (
          <ServerConfigModal onClose={() => setShowServerModal(false)} />
        )}

        {/* Register Store Modal */}
        {showRegisterStoreModal && (
          <RegisterStoreModal
            onClose={() => setShowRegisterStoreModal(false)}
            onSuccess={(newStore) => {
              setStoreInfo(newStore);
              setShowRegisterStoreModal(false);
              realtimeService.restart();
              window.location.reload();
            }}
          />
        )}

        {/* Switch Store Modal */}
        {showSwitchStoreModal && (
          <SwitchStoreModal
            currentCode={getStoredTenantCode()}
            onClose={() => setShowSwitchStoreModal(false)}
            onSwitch={(newStore) => {
              setStoreInfo(newStore);
              setShowSwitchStoreModal(false);
              realtimeService.restart();
              window.location.reload();
            }}
            onOpenRegister={() => {
              setShowSwitchStoreModal(false);
              setShowRegisterStoreModal(true);
            }}
          />
        )}

        {/* Table QR Code Manager Modal */}
        {showTableQrModal && (
          <TableQrManagerModal
            storeInfo={storeInfo}
            onClose={() => setShowTableQrModal(false)}
          />
        )}

        {/* Developer License Portal Modal */}
        {showDeveloperLicenseModal && (
          <DeveloperLicensePortalModal
            isOpen={showDeveloperLicenseModal}
            onClose={() => setShowDeveloperLicenseModal(false)}
          />
        )}

        {/* Activate License Modal */}
        {showActivateLicenseModal && (
          <ActivateLicenseModal
            isOpen={showActivateLicenseModal}
            storeInfo={storeInfo}
            onClose={() => setShowActivateLicenseModal(false)}
            onActivated={() => {
              loadStoreInfo();
            }}
          />
        )}
      </div>

    </ErrorBoundary>
  );
}

// -------------------------------------------------------------
// 1. Customer QR Ordering View
// -------------------------------------------------------------
interface CartItem {
  product: ProductItem;
  quantity: number;
  specialNotes: string;
}

interface CustomerViewProps {
  storeInfo?: StoreInfo | null;
  isStorePosOnline?: boolean;
  activePosTerminals?: number;
  onToggleDemoOnline?: () => void;
}

function CustomerView({
  storeInfo: propStoreInfo,
  isStorePosOnline: propIsStorePosOnline = true,
  activePosTerminals: _propActivePosTerminals = 0,
  onToggleDemoOnline
}: CustomerViewProps) {
  const [sessionParams] = useState(() => {
    if (typeof window === 'undefined') return { isCustomer: false, store: '', table: '', type: '' };
    const p = new URLSearchParams(window.location.search);
    const store = (p.get('store') || p.get('shop') || p.get('tenant') || p.get('code') || '').trim().toUpperCase();
    const table = (p.get('table') || '').trim().toUpperCase();
    const type = (p.get('type') || '').trim().toLowerCase();
    const demo = p.get('demo') === '1' || p.get('demo') === 'true';
    const isCustomer = Boolean(store || table || type === 'takeaway' || demo);
    return { isCustomer, store, table, type };
  });

  const [storeInfo, setStoreInfo] = useState<StoreInfo | null>(propStoreInfo || null);
  const [isStorePosOnline, setIsStorePosOnline] = useState<boolean>(propIsStorePosOnline);

  useEffect(() => {
    if (propStoreInfo) setStoreInfo(propStoreInfo);
  }, [propStoreInfo]);

  useEffect(() => {
    setIsStorePosOnline(propIsStorePosOnline);
  }, [propIsStorePosOnline]);

  useEffect(() => {
    if (!storeInfo) {
      getStoreInfo().then(info => {
        setStoreInfo(info);
        if (typeof info.isPosOnline === 'boolean') {
          setIsStorePosOnline(info.isPosOnline);
        }
      }).catch(() => {});
    }
  }, []);

  useEffect(() => {
    const unsub = realtimeService.onStoreStatusChanged((data) => {
      const currentCode = getStoredTenantCode() || 'DEFAULT';
      if (data.storeCode.toUpperCase() === currentCode.toUpperCase()) {
        setIsStorePosOnline(data.isOnline);
      }
    });
    return () => unsub();
  }, []);

  const [isReservation, setIsReservation] = useState<boolean>(false);
  const [reservationInfo, setReservationInfo] = useState<string>('');

  const [selectedTable, setSelectedTable] = useState<string>(() => {
    if (typeof window === 'undefined') return 'ออนไลน์';
    const p = new URLSearchParams(window.location.search);
    if (p.get('type') === 'takeaway') return 'กลับบ้าน';
    return p.get('table') || 'ออนไลน์';
  });

  const [customerName, setCustomerName] = useState<string>('');
  const [customerPhone, setCustomerPhone] = useState<string>('');
  const [tableNotes, setTableNotes] = useState<string>('');
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [products, setProducts] = useState<ProductItem[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<number | null>(null);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [showCartDrawer, setShowCartDrawer] = useState<boolean>(false);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  // Active order stored per tenant so only THIS browser session sees its own order status
  const [activeOrder, setActiveOrder] = useState<Order | null>(() => {
    if (typeof window === 'undefined') return null;
    try {
      const code = getStoredTenantCode() || 'DEFAULT';
      const saved = localStorage.getItem(`rpos_my_order_${code}`);
      return saved ? JSON.parse(saved) : null;
    } catch {
      return null;
    }
  });

  const updateMyActiveOrder = (order: Order | null) => {
    setActiveOrder(order);
    try {
      const code = getStoredTenantCode() || 'DEFAULT';
      if (order && order.status < 5) {
        localStorage.setItem(`rpos_my_order_${code}`, JSON.stringify(order));
      } else {
        localStorage.removeItem(`rpos_my_order_${code}`);
      }
    } catch {}
  };

  // Modal for customizing item notes before adding
  const [itemToCustom, setItemToCustom] = useState<ProductItem | null>(null);
  const [customNotes, setCustomNotes] = useState<string>('');
  const [customQty, setCustomQty] = useState<number>(1);

  // Modal for viewing full size image of product
  const [previewProduct, setPreviewProduct] = useState<ProductItem | null>(null);

  // Modal for confirming order with mandatory phone number & notes
  const [showConfirmModal, setShowConfirmModal] = useState<boolean>(false);
  const [confirmPhone, setConfirmPhone] = useState<string>('');
  const [confirmNotes, setConfirmNotes] = useState<string>('');
  const [confirmPhoneError, setConfirmPhoneError] = useState<string>('');

  // Check URL query param e.g. ?table=T03 or ?type=takeaway
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const tableParam = params.get('table');
    const typeParam = params.get('type');
    if (typeParam === 'takeaway') {
      setSelectedTable('กลับบ้าน');
    } else if (tableParam) {
      setSelectedTable(tableParam);
    } else {
      setSelectedTable('ออนไลน์');
    }
  }, []);

  // Fetch tables and menu
  useEffect(() => {
    loadData();

    // Listen to real-time order status updates strictly for THIS customer's active order
    const unsubOrder = realtimeService.onOrderStatusChanged((updated) => {
      setActiveOrder((prev) => {
        if (prev && prev.id === updated.id) {
          // Play chime if status transitions to Ready (4)
          if (prev.status !== 4 && updated.status === 4) {
            playOrderReadyChime();
          }
          const next = { ...prev, status: updated.status };
          try {
            const code = getStoredTenantCode() || 'DEFAULT';
            localStorage.setItem(`rpos_my_order_${code}`, JSON.stringify(next));
          } catch {}
          return next;
        }
        return prev;
      });
    });

    const unsubBillClosed = realtimeService.onBillClosed((closedOrder) => {
      setActiveOrder((prev) => {
        if (prev && prev.id === closedOrder.id) {
          try {
            const code = getStoredTenantCode() || 'DEFAULT';
            localStorage.removeItem(`rpos_my_order_${code}`);
          } catch {}
          return { ...prev, status: 5 };
        }
        return prev;
      });
    });

    // Listen to real-time menu and category updates
    const unsubMenu = realtimeService.onMenuUpdated(() => {
      loadData();
    });
    const unsubCat = realtimeService.onCategoryUpdated(() => {
      loadData();
    });

    return () => {
      unsubOrder();
      unsubBillClosed();
      unsubMenu();
      unsubCat();
    };
  }, []);

  const loadData = async () => {
    try {
      const [cList, pList] = await Promise.all([
        getCategories(),
        getProducts()
      ]);
      setCategories(cList);
      setProducts(pList);
    } catch (err: any) {
      logError('Failed to load menu data in CustomerView: ' + err.message, err);
    }
  };

  const handleOpenCustomize = (product: ProductItem) => {
    setItemToCustom(product);
    setCustomNotes('');
    setCustomQty(1);
  };

  const handleConfirmAddToCart = () => {
    if (!itemToCustom) return;

    setCart((prev) => {
      const existing = prev.find(i => i.product.id === itemToCustom.id && i.specialNotes === customNotes);
      if (existing) {
        return prev.map(i => i === existing ? { ...i, quantity: i.quantity + customQty } : i);
      }
      return [...prev, { product: itemToCustom, quantity: customQty, specialNotes: customNotes }];
    });

    setItemToCustom(null);
  };

  const handleQtyChange = (index: number, delta: number) => {
    setCart((prev) => {
      const updated = [...prev];
      updated[index].quantity += delta;
      if (updated[index].quantity <= 0) {
        return updated.filter((_, i) => i !== index);
      }
      return updated;
    });
  };

  const cartTotal = cart.reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
  const cartCount = cart.reduce((sum, item) => sum + item.quantity, 0);

  // Open Confirmation Popup Modal
  const handleOpenConfirmModal = () => {
    if (cart.length === 0) return;
    setConfirmPhone(customerPhone.trim());
    setConfirmNotes(tableNotes.trim());
    setConfirmPhoneError('');
    setShowConfirmModal(true);
  };

  // Submit Order to Backend API with Phone and Notes
  const doSubmitOrder = async (phone: string, notes: string) => {
    if (!isStorePosOnline) {
      alert('ขณะนี้ร้านยังไม่เปิดให้บริการ หรือระบบแคชเชียร์หน้าร้านไม่ได้เชื่อมต่อ ไม่สามารถสั่งอาหารได้ในขณะนี้');
      return;
    }
    setIsSubmitting(true);
    try {
      const isTakeaway = !sessionParams.table && !isReservation;
      const finalTableNumber = sessionParams.table 
        ? sessionParams.table 
        : (isReservation ? 'จองโต๊ะ' : 'ออนไลน์');

      let combinedNotes = notes ? notes.trim() : '';
      if (isReservation && reservationInfo.trim()) {
        combinedNotes = `[จองโต๊ะ: ${reservationInfo.trim()}] ${combinedNotes}`.trim();
      }

      const payload: CreateOrderPayload = {
        type: isTakeaway ? 2 : 1, // 1=DineIn, 2=TakeAway
        tableNumber: finalTableNumber,
        customerName: customerName.trim() || undefined,
        customerPhone: phone || undefined,
        notes: combinedNotes || undefined,
        clientRequestId: `WEB-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`,

        items: cart.map(i => ({
          productId: i.product.id,
          quantity: i.quantity,
          specialNotes: i.specialNotes
        }))
      };

      const newOrder = await createOrder(payload);
      updateMyActiveOrder(newOrder);
      setCart([]);
      setShowCartDrawer(false);
      setShowConfirmModal(false);
      alert(`สั่งอาหารเรียบร้อยแล้ว!\nเลขที่ออเดอร์: ${newOrder.orderNumber}\nระบบได้ส่งออเดอร์ไปยังเครื่องหลักหน้าร้านเรียบร้อยแล้ว`);
    } catch (err: any) {
      alert('ไม่สามารถส่งออเดอร์ได้: ' + err.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Validate and Confirm Order from Modal
  const handleConfirmOrder = () => {
    const p = confirmPhone.trim();
    if (!p || p.length < 9) {
      setConfirmPhoneError('กรุณากรอกเบอร์โทรศัพท์ให้ถูกต้อง (อย่างน้อย 9-10 หลัก) เพื่อให้ทางร้านสามารถโทรยืนยันออเดอร์ได้ ป้องกันการสั่งเล่น');
      return;
    }
    setCustomerPhone(p);
    setTableNotes(confirmNotes.trim());
    doSubmitOrder(p, confirmNotes.trim());
  };

  const filteredProducts = selectedCategory 
    ? products.filter(p => p.categoryId === selectedCategory) 
    : products;

  const resolveImageUrl = (img?: string) => {
    if (!img) return null;
    if (img.startsWith('http://') || img.startsWith('https://')) return img;
    return `${getServerUrl()}${img}`;
  };

  return (
    <div>
      {/* Store Information, Contact Card & Real-Time POS Status */}
      <div style={{
        backgroundColor: '#FFF',
        border: isStorePosOnline ? '1px solid #C8E6C9' : '1px solid #FFCDD2',
        borderLeft: isStorePosOnline ? '6px solid #2E7D32' : '6px solid #C62828',
        borderRadius: '8px',
        padding: '16px',
        marginBottom: '16px',
        boxShadow: 'var(--shadow)'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '10px' }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 'bold', color: '#1E293B', margin: 0 }}>
                {storeInfo?.storeName || (getStoredTenantCode() === 'DEFAULT' ? 'ร้านอาหารตัวอย่าง (สาขาหลัก)' : getStoredTenantCode())}
              </h2>
              <span style={{
                backgroundColor: isStorePosOnline ? '#2E7D32' : '#C62828',
                color: '#FFF',
                padding: '3px 10px',
                borderRadius: '20px',
                fontSize: '11.5px',
                fontWeight: 'bold'
              }}>
                {isStorePosOnline ? '[ เปิดให้บริการ - พร้อมรับออเดอร์ ]' : '[ ขณะนี้ร้านยังไม่เปิดรับออเดอร์ ]'}
              </span>
            </div>

            {/* Store Contacts: Phone, Address, Operating Hours */}
            <div style={{ marginTop: '8px', display: 'flex', flexWrap: 'wrap', gap: '16px', fontSize: '13px', color: '#475569' }}>
              {storeInfo?.ownerPhone && (
                <div>
                  <span style={{ fontWeight: 'bold', color: '#1E293B' }}>เบอร์โทรติดต่อร้าน: </span>
                  <a
                    href={`tel:${storeInfo.ownerPhone}`}
                    style={{ color: '#0D47A1', fontWeight: 'bold', textDecoration: 'none' }}
                    title="กดเพื่อโทรออก"
                  >
                    [ โทร: {storeInfo.ownerPhone} ]
                  </a>
                </div>
              )}
              {storeInfo?.address && (
                <div>
                  <span style={{ fontWeight: 'bold', color: '#1E293B' }}>ที่อยู่ร้าน: </span>
                  <span>{storeInfo.address}</span>
                </div>
              )}
              <div>
                <span style={{ fontWeight: 'bold', color: '#1E293B' }}>เวลาทำการ: </span>
                <span>{storeInfo?.openingHours || '10:00 - 22:00 น.'}</span>
              </div>
            </div>
          </div>

          {/* Interactive Demo Pitching Toggle (Visible for DEFAULT / demo store) */}
          {(getStoredTenantCode() === 'DEFAULT' || (typeof window !== 'undefined' && window.location.search.includes('demo'))) && onToggleDemoOnline && (
            <div style={{
              backgroundColor: '#F8FAFC',
              border: '1px dashed #94A3B8',
              padding: '8px 12px',
              borderRadius: '6px',
              display: 'flex',
              flexDirection: 'column',
              gap: '4px',
              alignItems: 'flex-end'
            }}>
              <span style={{ fontSize: '10.5px', color: '#64748B', fontWeight: 'bold' }}>
                [ จำลองสถานะสำหรับนำเสนอขาย (Live Pitch) ]
              </span>
              <button
                type="button"
                onClick={onToggleDemoOnline}
                style={{
                  backgroundColor: isStorePosOnline ? '#E65100' : '#2E7D32',
                  color: '#FFF',
                  border: 'none',
                  padding: '5px 12px',
                  borderRadius: '4px',
                  fontSize: '11.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                [ คลิกสลับ: {isStorePosOnline ? 'จำลองร้านปิด (ดูเมนูได้อย่างเดียว)' : 'จำลองร้านเปิด (สั่งอาหารได้)'} ]
              </button>
            </div>
          )}
        </div>

        {/* Offline Notice Banner when POS is not active */}
        {!isStorePosOnline && (
          <div style={{
            marginTop: '12px',
            backgroundColor: '#FFF8E1',
            border: '1px solid #FFE082',
            borderRadius: '6px',
            padding: '10px 14px',
            fontSize: '12.5px',
            color: '#B78103'
          }}>
            <div style={{ fontWeight: 'bold', marginBottom: '4px' }}>
              [ ประกาศ: ขณะนี้ร้านยังไม่เปิดให้บริการ หรือระบบเครื่องแคชเชียร์หน้าร้านปิดอยู่ ]
            </div>
            <div>
              ท่านสามารถ <strong>เลือกชมรายการอาหาร รูปภาพ ส่วนประกอบ และราคาล่วงหน้าได้ตามปกติ</strong> เมื่อทางร้านเปิดเครื่องแคชเชียร์หน้าร้าน ระบบจะปลดล็อกรับคำสั่งซื้อแบบเรียลไทม์ทันทีโดยไม่ต้องรีเฟรชหน้าจอ 
              {storeInfo?.ownerPhone ? ` หรือท่านสามารถโทรติดต่อสอบถามทางร้านได้โดยตรงที่เบอร์ ${storeInfo.ownerPhone}` : ''}
            </div>
          </div>
        )}
      </div>

      {/* Table Selector & Customer Information Banner */}
      <div style={{
        backgroundColor: '#FFF',
        padding: '14px',
        borderRadius: '8px',
        marginBottom: '16px',
        boxShadow: 'var(--shadow)',
        display: 'flex',
        flexWrap: 'wrap',
        gap: '12px',
        justifyContent: 'space-between',
        alignItems: 'center',
        boxSizing: 'border-box',
        width: '100%'
      }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '10px', width: '100%' }}>
          {sessionParams.table ? (
            <div style={{
              flex: '1 1 180px',
              backgroundColor: '#E8F5E9',
              border: '1.5px solid #4CAF50',
              padding: '6px 12px',
              borderRadius: '4px',
              display: 'flex',
              alignItems: 'center',
              gap: '8px'
            }}>
              <span style={{
                backgroundColor: '#2E7D32',
                color: '#FFF',
                padding: '2px 8px',
                borderRadius: '10px',
                fontSize: '11.5px',
                fontWeight: 'bold'
              }}>
                [ โต๊ะอาหาร: {sessionParams.table} ]
              </span>
              <span style={{ fontSize: '12px', color: '#1B5E20' }}>
                สั่งประจำโต๊ะนี้
              </span>
            </div>
          ) : (
            <div style={{
              flex: '1 1 240px',
              backgroundColor: isReservation ? '#EFF6FF' : '#FFF7ED',
              border: isReservation ? '1.5px solid #3B82F6' : '1.5px solid #F97316',
              padding: '8px 12px',
              borderRadius: '6px'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '8px', flexWrap: 'wrap' }}>
                <span style={{
                  backgroundColor: isReservation ? '#1D4ED8' : '#C2410C',
                  color: '#FFF',
                  padding: '2px 8px',
                  borderRadius: '10px',
                  fontSize: '11.5px',
                  fontWeight: 'bold'
                }}>
                  {isReservation ? '[ จองโต๊ะทานที่ร้านล่วงหน้า ]' : '[ สั่งออนไลน์ / Takeaway ]'}
                </span>
                <label style={{ fontSize: '12px', fontWeight: 'bold', color: '#1E293B', display: 'flex', alignItems: 'center', gap: '5px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={isReservation}
                    onChange={(e) => setIsReservation(e.target.checked)}
                    style={{ width: '15px', height: '15px', cursor: 'pointer' }}
                  />
                  ต้องการจองโต๊ะล่วงหน้า
                </label>
              </div>
              {isReservation ? (
                <div style={{ marginTop: '6px' }}>
                  <input
                    type="text"
                    placeholder="ระบุจำนวนท่าน และเวลาที่ต้องการจอง (เช่น 4 ท่าน เวลา 18:30 น.)"
                    value={reservationInfo}
                    onChange={(e) => setReservationInfo(e.target.value)}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #93C5FD', fontSize: '12px', boxSizing: 'border-box' }}
                  />
                </div>
              ) : (
                <div style={{ fontSize: '11px', color: '#9A3412', marginTop: '3px' }}>
                  สั่งซื้อกลับบ้าน / ล่วงหน้า กรุณาระบุเบอร์โทรเพื่อให้ทางร้านติดต่อแจ้งสถานะ
                </div>
              )}
            </div>
          )}

          <div style={{ flex: '1 1 130px', minWidth: '120px' }}>
            <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '2px', color: '#555' }}>
              ชื่อผู้สั่ง (ไม่บังคับ):
            </label>
            <input
              type="text"
              placeholder="เช่น คุณสมชาย"
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 10px',
                borderRadius: '4px',
                border: '1px solid #CCC',
                fontSize: '13px'
              }}
            />
          </div>

          <div style={{ flex: '1 1 130px', minWidth: '120px' }}>
            <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '2px', color: '#B91C1C' }}>
              เบอร์โทรศัพท์ (จำเป็น): *
            </label>
            <input
              type="tel"
              placeholder="เช่น 0812345678"
              value={customerPhone}
              onChange={(e) => setCustomerPhone(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 10px',
                borderRadius: '4px',
                border: '1.5px solid #F87171',
                fontSize: '13px',
                fontWeight: 'bold'
              }}
            />
          </div>

          <div style={{ flex: '2 1 160px', minWidth: '140px' }}>
            <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '2px', color: '#555' }}>
              หมายเหตุเพิ่มเติม (ไม่บังคับ):
            </label>
            <input
              type="text"
              placeholder="เช่น เวลามารับอาหาร หรือคำแนะนำพิเศษ"
              value={tableNotes}
              onChange={(e) => setTableNotes(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 10px',
                borderRadius: '4px',
                border: '1px solid #CCC',
                fontSize: '13px'
              }}
            />
          </div>
        </div>

        {activeOrder && (
          <div style={{
            backgroundColor: '#E8F5E9',
            border: '1px solid #81C784',
            padding: '8px 14px',
            borderRadius: '6px',
            textAlign: 'right',
            marginTop: '10px'
          }}>
            <div style={{ fontSize: '11px', color: '#2E7D32', fontWeight: 'bold' }}>
              ออเดอร์ล่าสุด #{activeOrder.orderNumber}
            </div>
            <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#1B5E20' }}>
              สถานะ: {getStatusText(activeOrder.status)}
            </div>
          </div>
        )}
      </div>

      {/* Real-time Order Tracking Timeline */}
      {activeOrder && (
        <div style={{
          backgroundColor: '#FFF',
          padding: '16px 20px',
          borderRadius: '8px',
          marginBottom: '16px',
          boxShadow: 'var(--shadow)',
          borderLeft: '5px solid #2E7D32'
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <h3 style={{ fontSize: '15px', fontWeight: 'bold', color: '#1B5E20' }}>
              การติดตามสถานะออเดอร์แบบ Real-time: #{activeOrder.orderNumber}
            </h3>
            <span style={{ fontSize: '12px', color: '#666' }}>
              โต๊ะ: {activeOrder.tableNumber} | ยอดรวม: {activeOrder.totalAmount.toFixed(2)} บาท
            </span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', position: 'relative', marginTop: '10px' }}>
            {[
              { step: 1, label: '[รอรับออเดอร์]' },
              { step: 2, label: '[รับออเดอร์แล้ว]' },
              { step: 3, label: '[กำลังปรุง]' },
              { step: 4, label: '[พร้อมเสิร์ฟ]' },
              { step: 5, label: '[เสร็จสิ้น]' }
            ].map((s) => {
              const isPassed = activeOrder.status >= s.step;
              const isCurrent = activeOrder.status === s.step;
              return (
                <div key={s.step} style={{ textAlign: 'center', flex: 1 }}>
                  <div style={{
                    width: '28px',
                    height: '28px',
                    borderRadius: '50%',
                    backgroundColor: isCurrent ? '#EF6C00' : (isPassed ? '#2E7D32' : '#E0E0E0'),
                    color: '#FFF',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    margin: '0 auto 6px',
                    fontSize: '12px',
                    fontWeight: 'bold',
                    boxShadow: isCurrent ? '0 0 8px rgba(239, 108, 0, 0.6)' : 'none'
                  }}>
                    {s.step}
                  </div>
                  <div style={{
                    fontSize: '11px',
                    fontWeight: isCurrent ? 'bold' : 'normal',
                    color: isCurrent ? '#EF6C00' : (isPassed ? '#2E7D32' : '#888')
                  }}>
                    {s.label}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Categories Horizontal Pills */}
      <div style={{
        display: 'flex',
        gap: '8px',
        overflowX: 'auto',
        paddingBottom: '10px',
        marginBottom: '16px'
      }}>
        <button
          onClick={() => setSelectedCategory(null)}
          style={{
            padding: '8px 16px',
            borderRadius: '20px',
            border: 'none',
            backgroundColor: selectedCategory === null ? '#1976D2' : '#FFF',
            color: selectedCategory === null ? '#FFF' : '#333',
            fontWeight: 600,
            fontSize: '13px',
            boxShadow: 'var(--shadow)',
            whiteSpace: 'nowrap'
          }}
        >
          ทั้งหมด ({products.length})
        </button>
        {categories.map(c => (
          <button
            key={c.id}
            onClick={() => setSelectedCategory(c.id)}
            style={{
              padding: '8px 16px',
              borderRadius: '20px',
              border: 'none',
              backgroundColor: selectedCategory === c.id ? '#1976D2' : '#FFF',
              color: selectedCategory === c.id ? '#FFF' : '#333',
              fontWeight: 600,
              fontSize: '13px',
              boxShadow: 'var(--shadow)',
              whiteSpace: 'nowrap'
            }}
          >
            {c.name}
          </button>
        ))}
      </div>

      {/* Food Menu Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
        gap: '16px',
        paddingBottom: '80px'
      }}>
        {filteredProducts.map(p => {
          const imgUrl = resolveImageUrl(p.imageUrl);
          const isSoldOut = !p.isAvailable;
          return (
            <div
              key={p.id}
              style={{
                backgroundColor: isSoldOut ? '#F8F9FA' : '#FFF',
                borderRadius: '8px',
                overflow: 'hidden',
                boxShadow: isSoldOut ? '0 1px 3px rgba(0,0,0,0.06)' : 'var(--shadow)',
                border: isSoldOut ? '1px solid #D6D6D6' : '1px solid transparent',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                opacity: isSoldOut ? 0.65 : 1,
                filter: isSoldOut ? 'grayscale(25%)' : 'none',
                position: 'relative',
                transition: 'all 0.2s ease'
              }}
            >
              {/* Product Image Area with Click-to-Enlarge Preview */}
              <div 
                onClick={() => setPreviewProduct(p)}
                title="คลิกเพื่อดูภาพเมนูขนาดใหญ่"
                style={{ 
                  position: 'relative', 
                  width: '100%', 
                  height: '140px', 
                  backgroundColor: '#ECEFF1', 
                  overflow: 'hidden',
                  cursor: 'pointer'
                }}
              >
                {imgUrl ? (
                  <img 
                    src={imgUrl} 
                    alt={p.name} 
                    style={{ width: '100%', height: '100%', objectFit: 'cover' }} 
                  />
                ) : (
                  <div style={{ 
                    width: '100%', 
                    height: '100%', 
                    display: 'flex', 
                    alignItems: 'center', 
                    justifyContent: 'center',
                    color: '#90A4AE',
                    fontSize: '12px'
                  }}>
                    [ ไม่มีรูปภาพเมนู ]
                  </div>
                )}

                {/* Click to preview tag */}
                <div style={{
                  position: 'absolute',
                  bottom: '6px',
                  right: '6px',
                  backgroundColor: 'rgba(0,0,0,0.6)',
                  color: '#FFF',
                  fontSize: '10px',
                  padding: '2px 6px',
                  borderRadius: '4px',
                  pointerEvents: 'none',
                  zIndex: 1
                }}>
                  [ คลิกดูรูปเต็ม ]
                </div>

                {/* Sold out image overlay badge */}
                {isSoldOut && (
                  <div style={{
                    position: 'absolute',
                    top: 0, left: 0, right: 0, bottom: 0,
                    backgroundColor: 'rgba(0,0,0,0.45)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 2
                  }}>
                    <span style={{
                      backgroundColor: '#C62828',
                      color: '#FFF',
                      padding: '5px 16px',
                      borderRadius: '20px',
                      fontWeight: 'bold',
                      fontSize: '14px',
                      letterSpacing: '1px',
                      boxShadow: '0 2px 8px rgba(0,0,0,0.4)'
                    }}>
                      หมด
                    </span>
                  </div>
                )}
              </div>

              {/* Card Body */}
              <div style={{ padding: '14px', flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                    <span style={{ fontSize: '11px', color: '#888' }}>
                      รหัส: {p.code}
                    </span>
                    {isSoldOut ? (
                      <span style={{ fontSize: '11px', color: '#C62828', fontWeight: 'bold' }}>
                        [ หมด ]
                      </span>
                    ) : (
                      <span style={{ fontSize: '11px', color: '#2E7D32', fontWeight: 'bold' }}>
                        [ พร้อมขาย ]
                      </span>
                    )}
                  </div>
                  <h3 style={{ fontSize: '15px', fontWeight: 'bold', marginBottom: '6px', color: isSoldOut ? '#555' : '#222' }}>
                    {p.name}
                  </h3>

                  {/* Out of Stock Reason Notice */}
                  {isSoldOut && (
                    <div style={{
                      backgroundColor: '#FFEBEE',
                      border: '1px solid #FFCDD2',
                      color: '#C62828',
                      padding: '4px 8px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 'bold',
                      marginBottom: '8px',
                      lineHeight: '1.4'
                    }}>
                      [ สินค้าหมด ] {p.outOfStockReason ? `สาเหตุ: ${p.outOfStockReason}` : 'สินค้าหมดชั่วคราว'}
                    </div>
                  )}

                  <p style={{ fontSize: '12px', color: isSoldOut ? '#888' : '#666', marginBottom: '12px' }}>
                    {p.description || 'อาหารจานโปรด ปรุงสดใหม่ตามสั่ง'}
                  </p>
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '10px' }}>
                  <span style={{ fontSize: '16px', fontWeight: 'bold', color: isSoldOut ? '#757575' : '#1976D2' }}>
                    {p.price.toFixed(2)} บาท
                  </span>
                  
                  {isSoldOut ? (
                    <button
                      disabled
                      style={{
                        backgroundColor: '#B0BEC5',
                        color: '#FFF',
                        border: 'none',
                        padding: '6px 14px',
                        borderRadius: '4px',
                        fontWeight: 'bold',
                        fontSize: '12px',
                        cursor: 'not-allowed'
                      }}
                    >
                      [ หมด ] สั่งไม่ได้
                    </button>
                  ) : (
                    <button
                      onClick={() => handleOpenCustomize(p)}
                      style={{
                        backgroundColor: '#1976D2',
                        color: '#FFF',
                        border: 'none',
                        padding: '6px 14px',
                        borderRadius: '4px',
                        fontWeight: 'bold',
                        fontSize: '13px',
                        cursor: 'pointer'
                      }}
                    >
                      เลือกเมนู
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Full-Screen Image Lightbox Preview Modal */}
      {previewProduct && (
        <div
          onClick={() => setPreviewProduct(null)}
          style={{
            position: 'fixed',
            top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.82)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 2000,
            padding: '20px'
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              backgroundColor: '#FFF',
              borderRadius: '12px',
              maxWidth: '560px',
              width: '100%',
              overflow: 'hidden',
              boxShadow: '0 12px 40px rgba(0,0,0,0.5)',
              display: 'flex',
              flexDirection: 'column',
              maxHeight: '90vh'
            }}
          >
            {/* Full Image Box */}
            <div style={{ 
              position: 'relative', 
              backgroundColor: '#111', 
              display: 'flex', 
              justifyContent: 'center', 
              alignItems: 'center', 
              minHeight: '260px', 
              maxHeight: '52vh', 
              overflow: 'hidden' 
            }}>
              {previewProduct.imageUrl ? (
                <img
                  src={resolveImageUrl(previewProduct.imageUrl) || ''}
                  alt={previewProduct.name}
                  style={{ width: '100%', height: '100%', maxHeight: '52vh', objectFit: 'contain' }}
                />
              ) : (
                <div style={{ color: '#888', padding: '40px', fontSize: '14px' }}>
                  [ ไม่มีรูปภาพเมนูขนาดใหญ่ ]
                </div>
              )}
              {!previewProduct.isAvailable && (
                <div style={{
                  position: 'absolute',
                  top: '16px',
                  right: '16px',
                  backgroundColor: '#C62828',
                  color: '#FFF',
                  padding: '6px 16px',
                  borderRadius: '20px',
                  fontWeight: 'bold',
                  fontSize: '14px',
                  boxShadow: '0 2px 8px rgba(0,0,0,0.5)'
                }}>
                  หมด
                </div>
              )}
            </div>

            {/* Product Details */}
            <div style={{ padding: '20px', overflowY: 'auto' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
                <div>
                  <div style={{ fontSize: '12px', color: '#888' }}>
                    รหัส: {previewProduct.code} | หมวดหมู่: {previewProduct.categoryName}
                  </div>
                  <h3 style={{ fontSize: '20px', fontWeight: 'bold', color: '#111', marginTop: '2px' }}>
                    {previewProduct.name}
                  </h3>
                </div>
                <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#1976D2', whiteSpace: 'nowrap', marginLeft: '12px' }}>
                  {previewProduct.price.toFixed(2)} บาท
                </div>
              </div>

              {!previewProduct.isAvailable && (
                <div style={{
                  backgroundColor: '#FFEBEE',
                  border: '1px solid #FFCDD2',
                  color: '#C62828',
                  padding: '8px 12px',
                  borderRadius: '6px',
                  fontSize: '13px',
                  fontWeight: 'bold',
                  marginBottom: '12px'
                }}>
                  สถานะ: สินค้าหมด
                  {previewProduct.outOfStockReason && (
                    <div style={{ fontWeight: 'normal', fontSize: '12px', marginTop: '2px' }}>
                      สาเหตุที่หมด: {previewProduct.outOfStockReason}
                    </div>
                  )}
                </div>
              )}

              <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', marginBottom: '20px' }}>
                {previewProduct.description || 'อาหารจานโปรด ปรุงสดใหม่ด้วยวัตถุดิบคุณภาพ คัดสรรความอร่อยเพื่อคุณ'}
              </p>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  onClick={() => setPreviewProduct(null)}
                  style={{
                    padding: '8px 20px',
                    backgroundColor: '#EEEEEE',
                    color: '#333',
                    border: '1px solid #CCC',
                    borderRadius: '6px',
                    fontWeight: 'bold',
                    fontSize: '13px',
                    cursor: 'pointer'
                  }}
                >
                  ปิดหน้าต่าง
                </button>
                {previewProduct.isAvailable && (
                  !isStorePosOnline ? (
                    <div style={{
                      backgroundColor: '#FFEBEE',
                      border: '1px solid #FFCDD2',
                      color: '#C62828',
                      padding: '8px 16px',
                      borderRadius: '6px',
                      fontWeight: 'bold',
                      fontSize: '12.5px',
                      display: 'flex',
                      alignItems: 'center'
                    }}>
                      [ ร้านยังไม่เปิดรับออเดอร์ ]
                    </div>
                  ) : (
                    <button
                      onClick={() => {
                        const p = previewProduct;
                        setPreviewProduct(null);
                        handleOpenCustomize(p);
                      }}
                      style={{
                        padding: '8px 20px',
                        backgroundColor: '#1976D2',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '6px',
                        fontWeight: 'bold',
                        fontSize: '13px',
                        cursor: 'pointer'
                      }}
                    >
                      เลือกเมนูสั่งอาหาร
                    </button>
                  )
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Floating Bottom Cart Bar */}
      {cartCount > 0 && (
        <div style={{
          position: 'fixed',
          bottom: '20px',
          left: '50%',
          transform: 'translateX(-50%)',
          width: '90%',
          maxWidth: '600px',
          backgroundColor: '#1E293B',
          color: '#FFF',
          padding: '12px 20px',
          borderRadius: '12px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          boxShadow: '0 4px 20px rgba(0,0,0,0.25)',
          zIndex: 99
        }}>
          <div>
            <div style={{ fontSize: '12px', color: '#94A3B8' }}>{cartCount} รายการในตะกร้า</div>
            <div style={{ fontSize: '18px', fontWeight: 'bold', color: '#38BDF8' }}>
              ยอดรวม: {cartTotal.toFixed(2)} บาท
            </div>
          </div>
          <button
            onClick={() => setShowCartDrawer(true)}
            style={{
              backgroundColor: '#0EA5E9',
              color: '#FFF',
              border: 'none',
              padding: '10px 20px',
              borderRadius: '8px',
              fontWeight: 'bold',
              fontSize: '14px'
            }}
          >
            ดูตะกร้าสั่งอาหาร
          </button>
        </div>
      )}

      {/* Item Customization Modal */}
      {itemToCustom && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            padding: '24px',
            borderRadius: '12px',
            width: '90%',
            maxWidth: '420px',
            boxShadow: '0 10px 25px rgba(0,0,0,0.2)'
          }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '8px' }}>
              {itemToCustom.name}
            </h3>
            <p style={{ color: '#1976D2', fontWeight: 'bold', fontSize: '16px', marginBottom: '16px' }}>
              ราคา: {itemToCustom.price.toFixed(2)} บาท
            </p>

            {/* Quantity */}
            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                จำนวน:
              </label>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <button
                  onClick={() => setCustomQty(Math.max(1, customQty - 1))}
                  style={{ width: '36px', height: '36px', fontSize: '18px', borderRadius: '4px', border: '1px solid #CCC' }}
                >
                  -
                </button>
                <span style={{ fontSize: '18px', fontWeight: 'bold', width: '30px', textAlign: 'center' }}>
                  {customQty}
                </span>
                <button
                  onClick={() => setCustomQty(customQty + 1)}
                  style={{ width: '36px', height: '36px', fontSize: '18px', borderRadius: '4px', border: '1px solid #CCC' }}
                >
                  +
                </button>
              </div>
            </div>

            {/* Special Instructions */}
            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                หมายเหตุเพิ่มเติม (เช่น ไม่เผ็ด, หวานน้อย, แยกน้ำ):
              </label>
              <input
                type="text"
                placeholder="ระบุความต้องการพิเศษ..."
                value={customNotes}
                onChange={(e) => setCustomNotes(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '4px',
                  border: '1px solid #CCC',
                  fontSize: '13px'
                }}
              />
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setItemToCustom(null)}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold'
                }}
              >
                ยกเลิก
              </button>
              {!isStorePosOnline ? (
                <div style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  backgroundColor: '#FFEBEE',
                  color: '#C62828',
                  border: '1px solid #FFCDD2',
                  fontWeight: 'bold',
                  fontSize: '12px',
                  textAlign: 'center',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center'
                }}>
                  [ ร้านยังไม่เปิดรับออเดอร์ ]
                </div>
              ) : (
                <button
                  onClick={handleConfirmAddToCart}
                  style={{
                    flex: 1,
                    padding: '10px',
                    borderRadius: '6px',
                    border: 'none',
                    backgroundColor: '#1976D2',
                    color: '#FFF',
                    fontWeight: 'bold'
                  }}
                >
                  ใส่ตะกร้า
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Cart Drawer Modal */}
      {showCartDrawer && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'flex-end',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            width: '100%',
            maxWidth: '450px',
            height: '100%',
            display: 'flex',
            flexDirection: 'column',
            boxShadow: '-4px 0 20px rgba(0,0,0,0.2)'
          }}>
            <div style={{
              padding: '16px 20px',
              borderBottom: '1px solid #EEE',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center'
            }}>
              <div>
                <h3 style={{ fontSize: '18px', fontWeight: 'bold' }}>
                  รายการสั่งอาหาร
                </h3>
                <div style={{ fontSize: '12px', color: '#666' }}>
                  โต๊ะ: {selectedTable} {customerName ? `| ลูกค้า: ${customerName}` : ''}
                </div>
              </div>
              <button
                onClick={() => setShowCartDrawer(false)}
                style={{ border: 'none', background: 'none', fontSize: '20px', fontWeight: 'bold', color: '#666' }}
              >
                X
              </button>
            </div>

            <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
              {cart.map((item, idx) => (
                <div
                  key={idx}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '12px 0',
                    borderBottom: '1px solid #F0F0F0'
                  }}
                >
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 'bold', fontSize: '14px' }}>{item.product.name}</div>
                    <div style={{ fontSize: '12px', color: '#1976D2' }}>
                      {item.product.price.toFixed(2)} x {item.quantity} = {(item.product.price * item.quantity).toFixed(2)} บาท
                    </div>
                    {item.specialNotes && (
                      <div style={{ fontSize: '11px', color: '#D32F2F', marginTop: '2px', fontWeight: 'bold' }}>
                        * หมายเหตุ: {item.specialNotes}
                      </div>
                    )}
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <button
                      onClick={() => handleQtyChange(idx, -1)}
                      style={{ width: '28px', height: '28px', borderRadius: '4px', border: '1px solid #CCC' }}
                    >
                      -
                    </button>
                    <span style={{ fontWeight: 'bold', width: '20px', textAlign: 'center' }}>
                      {item.quantity}
                    </span>
                    <button
                      onClick={() => handleQtyChange(idx, 1)}
                      style={{ width: '28px', height: '28px', borderRadius: '4px', border: '1px solid #CCC' }}
                    >
                      +
                    </button>
                  </div>
                </div>
              ))}
            </div>

            <div style={{ padding: '16px 20px', borderTop: '1px solid #EEE', backgroundColor: '#F9FAFB' }}>
              {tableNotes && (
                <div style={{ fontSize: '12px', color: '#555', marginBottom: '10px' }}>
                  หมายเหตุของโต๊ะ: {tableNotes}
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '14px', fontSize: '18px', fontWeight: 'bold' }}>
                <span>ยอดสุทธิ:</span>
                <span style={{ color: '#D32F2F' }}>{cartTotal.toFixed(2)} บาท</span>
              </div>

              <button
                disabled={isSubmitting || cart.length === 0 || !isStorePosOnline}
                onClick={handleOpenConfirmModal}
                style={{
                  width: '100%',
                  padding: '14px',
                  backgroundColor: !isStorePosOnline ? '#9E9E9E' : (isSubmitting ? '#9E9E9E' : '#2E7D32'),
                  color: '#FFF',
                  border: 'none',
                  borderRadius: '6px',
                  fontSize: '15px',
                  fontWeight: 'bold',
                  boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
                  cursor: !isStorePosOnline ? 'not-allowed' : 'pointer'
                }}
              >
                {!isStorePosOnline
                  ? '[ ขณะนี้ร้านยังไม่เปิดรับออเดอร์ (ระบบหน้าร้านออฟไลน์) ]'
                  : (isSubmitting ? 'กำลังส่งออเดอร์...' : 'ตรวจสอบและยืนยันการสั่งอาหาร')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Order Confirmation Modal (Pop-up with Phone & Notes verification) */}
      {showConfirmModal && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.65)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1100,
          padding: '16px'
        }}>
          <div style={{
            backgroundColor: '#FFF',
            borderRadius: '12px',
            maxWidth: '480px',
            width: '100%',
            overflow: 'hidden',
            boxShadow: '0 20px 40px rgba(0,0,0,0.3)',
            display: 'flex',
            flexDirection: 'column',
            maxHeight: '90vh'
          }}>
            <div style={{
              backgroundColor: '#1E3A8A',
              color: '#FFF',
              padding: '16px 20px',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center'
            }}>
              <div>
                <h3 style={{ fontSize: '18px', fontWeight: 'bold', margin: 0 }}>
                  ยืนยันการสั่งอาหาร (Order Confirmation)
                </h3>
                <div style={{ fontSize: '12px', color: '#93C5FD', marginTop: '2px' }}>
                  โต๊ะ: {selectedTable} {customerName ? `| ผู้สั่ง: ${customerName}` : ''}
                </div>
              </div>
              <button
                onClick={() => setShowConfirmModal(false)}
                style={{ border: 'none', background: 'none', color: '#FFF', fontSize: '20px', cursor: 'pointer', fontWeight: 'bold' }}
              >
                X
              </button>
            </div>

            <div style={{ padding: '20px', overflowY: 'auto' }}>
              {/* Phone Verification Box */}
              <div style={{
                backgroundColor: '#EFF6FF',
                border: '1.5px solid #3B82F6',
                borderRadius: '8px',
                padding: '14px',
                marginBottom: '14px'
              }}>
                <label style={{ display: 'block', fontWeight: 'bold', fontSize: '14px', color: '#1E40AF', marginBottom: '6px' }}>
                  ยืนยันว่าเบอร์โทรนี้นะครับ (จำเป็นต้องระบุ): *
                </label>
                <input
                  type="tel"
                  value={confirmPhone}
                  onChange={(e) => {
                    setConfirmPhone(e.target.value);
                    if (e.target.value.trim().length >= 9) setConfirmPhoneError('');
                  }}
                  placeholder="เช่น 0812345678"
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    fontSize: '16px',
                    fontWeight: 'bold',
                    borderRadius: '6px',
                    border: confirmPhoneError ? '2px solid #DC2626' : '1.5px solid #93C5FD',
                    backgroundColor: '#FFF',
                    boxSizing: 'border-box'
                  }}
                />
                {confirmPhoneError && (
                  <div style={{ color: '#DC2626', fontSize: '12px', fontWeight: 'bold', marginTop: '4px' }}>
                    {confirmPhoneError}
                  </div>
                )}
                <div style={{ fontSize: '11.5px', color: '#2563EB', marginTop: '6px', lineHeight: '1.4' }}>
                  * กรุณาระบุเบอร์โทรศัพท์จริง เพื่อให้ทางร้านสามารถโทรคอนเฟิร์มออเดอร์ได้ ป้องกันการสั่งเล่น
                </div>
              </div>

              {/* Notes Box */}
              <div style={{
                backgroundColor: '#FEF9C3',
                border: '1.5px solid #EAB308',
                borderRadius: '8px',
                padding: '14px',
                marginBottom: '14px'
              }}>
                <label style={{ display: 'block', fontWeight: 'bold', fontSize: '14px', color: '#854D0E', marginBottom: '6px' }}>
                  หมายเหตุตามนี้นะครับ (ระบุเพิ่มเติม):
                </label>
                <textarea
                  value={confirmNotes}
                  onChange={(e) => setConfirmNotes(e.target.value)}
                  placeholder="เช่น เผ็ดน้อย, ไม่ใส่ผักชี, แยกน้ำซุป, ขอช้อนส้อม 2 ชุด"
                  rows={2}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    fontSize: '13.5px',
                    borderRadius: '6px',
                    border: '1px solid #FDE047',
                    backgroundColor: '#FFF',
                    resize: 'vertical',
                    boxSizing: 'border-box'
                  }}
                />
                <div style={{ fontSize: '11.5px', color: '#854D0E', marginTop: '4px' }}>
                  * รายละเอียดเพิ่มเติมนี้จะถูกส่งตรงไปยังห้องครัว
                </div>
              </div>

              {/* Order Items Preview */}
              <div style={{
                border: '1px solid #E2E8F0',
                borderRadius: '8px',
                padding: '12px',
                marginBottom: '16px',
                maxHeight: '160px',
                overflowY: 'auto',
                backgroundColor: '#F8FAFC'
              }}>
                <div style={{ fontWeight: 'bold', fontSize: '13px', color: '#64748B', marginBottom: '8px' }}>
                  รายการอาหารที่สั่ง ({cartCount} รายการ):
                </div>
                {cart.map((item, idx) => (
                  <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '13px' }}>
                    <div>
                      <span style={{ fontWeight: 'bold' }}>{item.quantity}x {item.product.name}</span>
                      {item.specialNotes && (
                        <div style={{ fontSize: '11.5px', color: '#DC2626', fontWeight: 'bold', paddingLeft: '8px' }}>
                          * {item.specialNotes}
                        </div>
                      )}
                    </div>
                    <span style={{ fontWeight: 'bold', color: '#1E293B' }}>
                      {(item.product.price * item.quantity).toFixed(2)} บ.
                    </span>
                  </div>
                ))}
              </div>

              {/* Total Amount */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px', padding: '0 4px' }}>
                <span style={{ fontSize: '16px', fontWeight: 'bold' }}>ยอดสุทธิทั้งสิ้น:</span>
                <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#15803D' }}>
                  {cartTotal.toFixed(2)} บาท
                </span>
              </div>

              {/* Buttons */}
              <div style={{ display: 'flex', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowConfirmModal(false)}
                  style={{
                    flex: 1,
                    padding: '12px',
                    backgroundColor: '#F1F5F9',
                    border: '1px solid #CBD5E1',
                    borderRadius: '6px',
                    fontWeight: 'bold',
                    fontSize: '14px',
                    color: '#475569',
                    cursor: 'pointer'
                  }}
                >
                  แก้ไขรายการ
                </button>
                <button
                  type="button"
                  disabled={isSubmitting}
                  onClick={handleConfirmOrder}
                  style={{
                    flex: 2,
                    padding: '12px',
                    backgroundColor: isSubmitting ? '#9E9E9E' : '#15803D',
                    border: 'none',
                    borderRadius: '6px',
                    fontWeight: 'bold',
                    fontSize: '15px',
                    color: '#FFF',
                    cursor: 'pointer',
                    boxShadow: '0 2px 8px rgba(21, 128, 61, 0.4)'
                  }}
                >
                  {isSubmitting ? 'กำลังส่งคำสั่งซื้อ...' : 'ยืนยันสั่งอาหารทันที'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// -------------------------------------------------------------
// 2. Kitchen Display System (KDS) View
// -------------------------------------------------------------
function KitchenView() {
  const [orders, setOrders] = useState<Order[]>([]);

  useEffect(() => {
    loadActiveOrders();

    // Listen to real-time events and trigger audio chime on new orders!
    const unsubNew = realtimeService.onOrderCreated(() => {
      playOrderAlertChime();
      loadActiveOrders();
    });

    const unsubStatus = realtimeService.onOrderStatusChanged(() => {
      loadActiveOrders();
    });

    const unsubBill = realtimeService.onBillClosed(() => {
      loadActiveOrders();
    });

    return () => {
      unsubNew();
      unsubStatus();
      unsubBill();
    };
  }, []);

  const loadActiveOrders = async () => {
    try {
      const active = await getActiveOrders();
      setOrders(active);
    } catch (err: any) {
      logError('Failed to load active orders in KitchenView: ' + err.message, err);
    }
  };

  const handleUpdateStatus = async (orderId: number, newStatus: number) => {
    try {
      await updateOrderStatus(orderId, newStatus);
      await loadActiveOrders();
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการอัปเดตสถานะ: ' + err.message);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <div>
          <h2 style={{ fontSize: '20px', fontWeight: 'bold' }}>
            จอแสดงผลรายการอาหารสำหรับห้องครัว (Kitchen Display)
          </h2>
          <p style={{ fontSize: '12px', color: '#666' }}>
            ระบบจะส่งเสียงเตือนเมื่อมีออเดอร์ใหม่เข้ามาแบบ Real-time
          </p>
        </div>
        <button
          onClick={loadActiveOrders}
          style={{
            backgroundColor: '#FFF',
            border: '1px solid #CCC',
            padding: '6px 14px',
            borderRadius: '4px',
            fontWeight: 600
          }}
        >
          รีเฟรช
        </button>
      </div>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
        gap: '16px'
      }}>
        {orders.length === 0 ? (
          <div style={{
            gridColumn: '1 / -1',
            backgroundColor: '#FFF',
            padding: '40px',
            textAlign: 'center',
            borderRadius: '8px',
            color: '#888',
            boxShadow: 'var(--shadow)'
          }}>
            [ ยังไม่มีรายการออเดอร์ที่รอดำเนินการ ]
          </div>
        ) : (
          orders.map(order => (
            <div
              key={order.id}
              style={{
                backgroundColor: '#FFF',
                borderRadius: '8px',
                padding: '16px',
                boxShadow: 'var(--shadow)',
                borderTop: `5px solid ${order.status === 1 ? '#D32F2F' : (order.status === 2 ? '#1976D2' : (order.status === 3 ? '#F57C00' : '#388E3C'))}`
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
                <div>
                  <span style={{ fontSize: '18px', fontWeight: 'bold' }}>
                    โต๊ะ: {order.tableNumber || 'สั่งกลับบ้าน'}
                  </span>
                  <div style={{ fontSize: '11px', color: '#666' }}>
                    บิล: {order.orderNumber}
                  </div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', alignItems: 'center', marginTop: '4px' }}>
                    <span style={{ fontSize: '12.5px', color: '#1E293B', fontWeight: 'bold' }}>
                      ลูกค้า: {order.customerName || 'ลูกค้าทั่วไป'}
                    </span>
                    {order.customerPhone ? (
                      <span style={{ 
                        fontSize: '12.5px', 
                        color: '#1D4ED8', 
                        fontWeight: 'bold',
                        backgroundColor: '#DBEAFE',
                        padding: '2px 8px',
                        borderRadius: '4px',
                        border: '1px solid #93C5FD'
                      }}>
                        โทร: {order.customerPhone}
                      </span>
                    ) : (
                      <span style={{ fontSize: '11px', color: '#94A3B8' }}>[ไม่ระบุเบอร์โทร]</span>
                    )}
                  </div>
                </div>
                <span style={{
                  fontSize: '12px',
                  fontWeight: 'bold',
                  padding: '4px 8px',
                  borderRadius: '4px',
                  backgroundColor: '#F0F0F0'
                }}>
                  {getStatusText(order.status)}
                </span>
              </div>

              {order.notes && (
                <div style={{
                  backgroundColor: '#FFF9C4',
                  border: '1px solid #FFF59D',
                  padding: '6px 10px',
                  borderRadius: '4px',
                  fontSize: '12px',
                  color: '#795548',
                  marginBottom: '10px',
                  fontWeight: 'bold'
                }}>
                  หมายเหตุของโต๊ะ: {order.notes}
                </div>
              )}

              <div style={{ borderTop: '1px dashed #DDD', paddingTop: '10px', marginBottom: '14px' }}>
                {order.items.map((item, idx) => (
                  <div key={idx} style={{ marginBottom: '8px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 'bold', fontSize: '14px' }}>
                      <span>{item.quantity}x {item.productName}</span>
                    </div>
                    {item.specialNotes && (
                      <div style={{ fontSize: '12px', color: '#D32F2F', fontWeight: 'bold', paddingLeft: '12px' }}>
                        * {item.specialNotes}
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Action buttons */}
              <div style={{ display: 'flex', gap: '8px' }}>
                {order.status === 1 && (
                  <button
                    onClick={() => handleUpdateStatus(order.id, 2)}
                    style={{
                      flex: 1,
                      backgroundColor: '#1976D2',
                      color: '#FFF',
                      border: 'none',
                      padding: '8px',
                      borderRadius: '4px',
                      fontWeight: 'bold',
                      fontSize: '13px'
                    }}
                  >
                    รับออเดอร์
                  </button>
                )}
                {order.status === 2 && (
                  <button
                    onClick={() => handleUpdateStatus(order.id, 3)}
                    style={{
                      flex: 1,
                      backgroundColor: '#F57C00',
                      color: '#FFF',
                      border: 'none',
                      padding: '8px',
                      borderRadius: '4px',
                      fontWeight: 'bold',
                      fontSize: '13px'
                    }}
                  >
                    เริ่มปรุง
                  </button>
                )}
                {order.status === 3 && (
                  <button
                    onClick={() => handleUpdateStatus(order.id, 4)}
                    style={{
                      flex: 1,
                      backgroundColor: '#388E3C',
                      color: '#FFF',
                      border: 'none',
                      padding: '8px',
                      borderRadius: '4px',
                      fontWeight: 'bold',
                      fontSize: '13px'
                    }}
                  >
                    ปรุงเสร็จพร้อมเสิร์ฟ
                  </button>
                )}
                {order.status === 4 && (
                  <button
                    onClick={() => handleUpdateStatus(order.id, 5)}
                    style={{
                      flex: 1,
                      backgroundColor: '#455A64',
                      color: '#FFF',
                      border: 'none',
                      padding: '8px',
                      borderRadius: '4px',
                      fontWeight: 'bold',
                      fontSize: '13px'
                    }}
                  >
                    เสิร์ฟเรียบร้อย
                  </button>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// 3. Management, Menu & Reports View
// -------------------------------------------------------------
function ManagementView() {
  const [subTab, setSubTab] = useState<'sales' | 'menu' | 'stock' | 'audit'>('sales');

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <h2 style={{ fontSize: '20px', fontWeight: 'bold' }}>
          ระบบจัดการร้านและหลังบ้าน (Store Management &amp; Settings)
        </h2>
      </div>

      {/* Sub Tabs */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', borderBottom: '2px solid #E0E0E0', paddingBottom: '8px' }}>
        <button
          onClick={() => setSubTab('sales')}
          style={{
            padding: '8px 16px',
            borderRadius: '4px',
            border: 'none',
            backgroundColor: subTab === 'sales' ? '#1976D2' : '#F5F5F5',
            color: subTab === 'sales' ? '#FFF' : '#333',
            fontWeight: 'bold',
            fontSize: '13px'
          }}
        >
          สรุปยอดขาย (Sales Dashboard)
        </button>
        <button
          onClick={() => setSubTab('menu')}
          style={{
            padding: '8px 16px',
            borderRadius: '4px',
            border: 'none',
            backgroundColor: subTab === 'menu' ? '#1976D2' : '#F5F5F5',
            color: subTab === 'menu' ? '#FFF' : '#333',
            fontWeight: 'bold',
            fontSize: '13px',
            cursor: 'pointer'
          }}
        >
          จัดการรายการอาหาร &amp; หมวดหมู่
        </button>
        <button
          onClick={() => setSubTab('stock')}
          style={{
            padding: '8px 16px',
            borderRadius: '4px',
            border: 'none',
            backgroundColor: subTab === 'stock' ? '#1976D2' : '#F5F5F5',
            color: subTab === 'stock' ? '#FFF' : '#333',
            fontWeight: 'bold',
            fontSize: '13px',
            cursor: 'pointer'
          }}
        >
          จัดการสต๊อกวัตถุดิบ (Raw Material Inventory)
        </button>
        <button
          onClick={() => setSubTab('audit')}
          style={{
            padding: '8px 16px',
            borderRadius: '4px',
            border: 'none',
            backgroundColor: subTab === 'audit' ? '#1976D2' : '#F5F5F5',
            color: subTab === 'audit' ? '#FFF' : '#333',
            fontWeight: 'bold',
            fontSize: '13px',
            cursor: 'pointer'
          }}
        >
          ประวัติการใช้งาน (Audit Logs)
        </button>
      </div>

      {subTab === 'sales' && <SalesDashboardView />}
      {subTab === 'menu' && <MenuManagementView />}
      {subTab === 'stock' && <StockManagementView />}
      {subTab === 'audit' && <AuditLogManagementView />}
    </div>
  );
}

// -------------------------------------------------------------
// 3.1 Sales Dashboard
// -------------------------------------------------------------
function SalesDashboardView() {
  const [report, setReport] = useState<any>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    loadReport();

    const unsubBill = realtimeService.onBillClosed(() => {
      loadReport();
    });

    const unsubNew = realtimeService.onOrderCreated(() => {
      loadReport();
    });

    return () => {
      unsubBill();
      unsubNew();
    };
  }, []);

  const loadReport = async () => {
    setIsLoading(true);
    try {
      const data = await getDailyReport();
      setReport(data);
    } catch (err: any) {
      logError('Failed to load daily report: ' + err.message, err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 'bold' }}>
          สรุปรายงานยอดขายประจำวัน (Daily Sales Overview)
        </h3>
        <button
          onClick={loadReport}
          style={{
            backgroundColor: '#FFF',
            border: '1px solid #CCC',
            padding: '6px 14px',
            borderRadius: '4px',
            fontWeight: 600,
            fontSize: '13px',
            cursor: 'pointer'
          }}
        >
          รีเฟรชข้อมูล
        </button>
      </div>

      {isLoading ? (
        <div style={{ padding: '20px', textAlign: 'center', color: '#666' }}>กำลังโหลดรายงานยอดขาย...</div>
      ) : report ? (
        <>
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
            gap: '16px',
            marginBottom: '20px'
          }}>
            <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
              <div style={{ fontSize: '13px', color: '#666' }}>ยอดขายรวมวันนี้</div>
              <div style={{ fontSize: '22px', fontWeight: 'bold', color: '#1B5E20', marginTop: '4px' }}>
                {(report.totalSales ?? 0).toFixed(2)} บาท
              </div>
            </div>
            <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
              <div style={{ fontSize: '13px', color: '#666' }}>เงินสด (Cash)</div>
              <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#1565C0', marginTop: '4px' }}>
                {(report.cashSales ?? 0).toFixed(2)} บาท
              </div>
            </div>
            <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
              <div style={{ fontSize: '13px', color: '#666' }}>สแกน PromptPay QR</div>
              <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#E65100', marginTop: '4px' }}>
                {(report.qrSales ?? 0).toFixed(2)} บาท
              </div>
            </div>
            <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
              <div style={{ fontSize: '13px', color: '#666' }}>จำนวนบิลที่ปิดการขาย</div>
              <div style={{ fontSize: '22px', fontWeight: 'bold', color: '#4A148C', marginTop: '4px' }}>
                {report.totalOrders ?? 0} บิล
              </div>
            </div>
          </div>

          <div style={{ backgroundColor: '#FFF', padding: '20px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 'bold', marginBottom: '12px' }}>
              10 อันดับเมนูขายดีประจำวัน
            </h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid #EEE', textAlign: 'left', backgroundColor: '#F8F9FA' }}>
                  <th style={{ padding: '8px' }}>ชื่อเมนูอาหาร</th>
                  <th style={{ padding: '8px', textAlign: 'center' }}>จำนวนที่ขายได้</th>
                  <th style={{ padding: '8px', textAlign: 'right' }}>ยอดขายรวม (บาท)</th>
                </tr>
              </thead>
              <tbody>
                {(!report.topProducts || report.topProducts.length === 0) ? (
                  <tr>
                    <td colSpan={3} style={{ padding: '20px', textAlign: 'center', color: '#888' }}>
                      ยังไม่มีรายการสั่งซื้อที่เสร็จสิ้นในวันนี้
                    </td>
                  </tr>
                ) : (
                  report.topProducts.map((item: any, idx: number) => (
                    <tr key={idx} style={{ borderBottom: '1px solid #F5F5F5' }}>
                      <td style={{ padding: '10px 8px' }}>{item.productName}</td>
                      <td style={{ padding: '10px 8px', textAlign: 'center', fontWeight: 'bold' }}>{item.quantitySold} จาน</td>
                      <td style={{ padding: '10px 8px', textAlign: 'right', fontWeight: 'bold', color: '#1976D2' }}>
                        {(item.totalRevenue ?? 0).toFixed(2)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <div style={{ padding: '20px', textAlign: 'center', color: '#888' }}>ไม่พบข้อมูลรายงาน</div>
      )}
    </div>
  );
}

// -------------------------------------------------------------
// 3.2 Menu & Category Management (Dedicated Page)
// -------------------------------------------------------------
function MenuManagementView() {
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [products, setProducts] = useState<ProductItem[]>([]);
  const [selectedCatFilter, setSelectedCatFilter] = useState<number | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Category Modal State
  const [showCatModal, setShowCatModal] = useState<boolean>(false);
  const [editingCat, setEditingCat] = useState<CategoryItem | null>(null);
  const [catName, setCatName] = useState<string>('');
  const [catDesc, setCatDesc] = useState<string>('');
  const [catSortOrder, setCatSortOrder] = useState<number>(1);
  const [isCatSaving, setIsCatSaving] = useState<boolean>(false);

  // Product Modal State
  const [showProdModal, setShowProdModal] = useState<boolean>(false);
  const [editingProduct, setEditingProduct] = useState<ProductItem | null>(null);
  const [prodCode, setProdCode] = useState<string>('');
  const [prodName, setProdName] = useState<string>('');
  const [prodCatId, setProdCatId] = useState<number>(0);
  const [prodPrice, setProdPrice] = useState<string>('');
  const [prodDesc, setProdDesc] = useState<string>('');
  const [prodStation, setProdStation] = useState<string>('MainKitchen');
  const [prodImageUrl, setProdImageUrl] = useState<string>('');
  const [prodIsAvailable, setProdIsAvailable] = useState<boolean>(true);
  const [prodOutOfStockReason, setProdOutOfStockReason] = useState<string>('วัตถุดิบหมด');
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [isProdSaving, setIsProdSaving] = useState<boolean>(false);

  useEffect(() => {
    loadData();

    const unsubMenu = realtimeService.onMenuUpdated(() => loadData());
    const unsubCat = realtimeService.onCategoryUpdated(() => loadData());
    return () => {
      unsubMenu();
      unsubCat();
    };
  }, []);

  const loadData = async () => {
    try {
      const [cats, prods] = await Promise.all([getCategories(), getProducts()]);
      setCategories(cats);
      setProducts(prods);
      if (cats.length > 0 && prodCatId === 0) {
        setProdCatId(cats[0].id);
      }
    } catch (err: any) {
      logError('Failed to load menu management data: ' + err.message, err);
    }
  };

  // --- Category Handlers ---
  const handleOpenAddCategory = () => {
    setEditingCat(null);
    setCatName('');
    setCatDesc('');
    setCatSortOrder(categories.length + 1);
    setShowCatModal(true);
  };

  const handleOpenEditCategory = (cat: CategoryItem) => {
    setEditingCat(cat);
    setCatName(cat.name);
    setCatDesc(cat.description || '');
    setCatSortOrder(cat.sortOrder);
    setShowCatModal(true);
  };

  const handleSaveCategory = async () => {
    if (!catName.trim()) {
      alert('กรุณาระบุชื่อหมวดหมู่');
      return;
    }
    setIsCatSaving(true);
    try {
      if (editingCat) {
        await updateCategory(editingCat.id, {
          name: catName.trim(),
          description: catDesc.trim() || undefined,
          sortOrder: catSortOrder
        });
        alert('อัปเดตหมวดหมู่สำเร็จ');
      } else {
        await createCategory({
          name: catName.trim(),
          description: catDesc.trim() || undefined,
          sortOrder: catSortOrder
        });
        alert('เพิ่มหมวดหมู่ใหม่สำเร็จ');
      }
      setShowCatModal(false);
      await loadData();
    } catch (err: any) {
      alert('เกิดข้อผิดพลาด: ' + err.message);
    } finally {
      setIsCatSaving(false);
    }
  };

  const handleDeleteCategory = async (cat: CategoryItem) => {
    if (!window.confirm(`ต้องการลบหมวดหมู่ '${cat.name}' หรือไม่?`)) return;
    try {
      await deleteCategory(cat.id);
      alert('ลบหมวดหมู่เรียบร้อยแล้ว');
      await loadData();
    } catch (err: any) {
      alert('ไม่สามารถลบหมวดหมู่ได้: ' + err.message);
    }
  };

  // --- Product Handlers ---
  const handleOpenAddProduct = () => {
    setEditingProduct(null);
    setProdCode(`M${String(products.length + 1).padStart(2, '0')}`);
    setProdName('');
    setProdCatId(categories[0]?.id || 1);
    setProdPrice('50');
    setProdDesc('');
    setProdStation('MainKitchen');
    setProdImageUrl('');
    setProdIsAvailable(true);
    setProdOutOfStockReason('วัตถุดิบหมด');
    setShowProdModal(true);
  };

  const handleOpenEditProduct = (p: ProductItem) => {
    setEditingProduct(p);
    setProdCode(p.code);
    setProdName(p.name);
    setProdCatId(p.categoryId);
    setProdPrice(p.price.toString());
    setProdDesc(p.description || '');
    setProdStation(p.kitchenStation || 'MainKitchen');
    setProdImageUrl(p.imageUrl || '');
    setProdIsAvailable(p.isAvailable);
    setProdOutOfStockReason(p.outOfStockReason || 'วัตถุดิบหมด');
    setShowProdModal(true);
  };

  const handleQuickToggleAvailability = async (p: ProductItem) => {
    const nextAvailable = !p.isAvailable;
    let nextReason: string | undefined = undefined;
    if (!nextAvailable) {
      const inputReason = window.prompt(`ระบุสาเหตุที่เมนู '${p.name}' หมด:`, p.outOfStockReason || 'วัตถุดิบหมด');
      if (inputReason === null) return; // user cancelled
      nextReason = inputReason.trim() || 'วัตถุดิบหมด';
    }

    try {
      await updateProduct(p.id, {
        isAvailable: nextAvailable,
        outOfStockReason: nextReason
      });
      await loadData();
    } catch (err: any) {
      alert('ไม่สามารถเปลี่ยนสถานะได้: ' + err.message);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    try {
      const uploadedUrl = await uploadProductImage(file);
      setProdImageUrl(uploadedUrl);
      alert('อัปโหลดรูปภาพขึ้นเซิร์ฟเวอร์เรียบร้อยแล้ว');
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการอัปโหลดรูป: ' + err.message);
    } finally {
      setIsUploading(false);
    }
  };

  const handleSaveProduct = async () => {
    if (!prodCode.trim() || !prodName.trim()) {
      alert('กรุณากรอกรหัสและชื่อเมนูอาหาร');
      return;
    }
    const priceVal = parseFloat(prodPrice);
    if (isNaN(priceVal) || priceVal < 0) {
      alert('กรุณาระบุราคาที่ถูกต้อง');
      return;
    }

    setIsProdSaving(true);
    try {
      const payload: Partial<ProductItem> = {
        code: prodCode.trim().toUpperCase(),
        name: prodName.trim(),
        categoryId: prodCatId,
        price: priceVal,
        description: prodDesc.trim() || undefined,
        kitchenStation: prodStation,
        imageUrl: prodImageUrl.trim() || undefined,
        isAvailable: prodIsAvailable,
        outOfStockReason: prodIsAvailable ? undefined : prodOutOfStockReason.trim()
      };

      if (editingProduct) {
        await updateProduct(editingProduct.id, payload);
        alert('อัปเดตเมนูอาหารสำเร็จ');
      } else {
        await createProduct(payload);
        alert('เพิ่มเมนูอาหารใหม่สำเร็จ');
      }
      setShowProdModal(false);
      await loadData();
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการบันทึก: ' + err.message);
    } finally {
      setIsProdSaving(false);
    }
  };

  const handleDeleteProduct = async (p: ProductItem) => {
    if (!window.confirm(`ต้องการลบเมนู '${p.name}' (${p.code}) หรือไม่?`)) return;
    try {
      await deleteProduct(p.id);
      alert('ลบเมนูอาหารเรียบร้อยแล้ว');
      await loadData();
    } catch (err: any) {
      alert('ไม่สามารถลบได้: ' + err.message);
    }
  };

  const resolveImageUrl = (img?: string) => {
    if (!img) return null;
    if (img.startsWith('http://') || img.startsWith('https://')) return img;
    return `${getServerUrl()}${img}`;
  };

  const filteredProducts = products.filter(p => {
    const matchCat = selectedCatFilter === null || p.categoryId === selectedCatFilter;
    const matchQuery = !searchQuery.trim() || 
      p.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
      p.code.toLowerCase().includes(searchQuery.toLowerCase());
    return matchCat && matchQuery;
  });

  return (
    <div>
      {/* 1. Category Management Section */}
      <div style={{ backgroundColor: '#FFF', padding: '16px 20px', borderRadius: '8px', boxShadow: 'var(--shadow)', marginBottom: '20px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <div>
            <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#1565C0' }}>
              หมวดหมู่อาหาร (Menu Categories)
            </h3>
            <p style={{ fontSize: '12px', color: '#666' }}>
              จัดการและจัดเรียงหมวดหมู่เมนูในร้าน สามารถเพิ่ม แก้ไข และลบได้อิสระ
            </p>
          </div>
          <button
            onClick={handleOpenAddCategory}
            style={{
              backgroundColor: '#1976D2',
              color: '#FFF',
              border: 'none',
              padding: '7px 14px',
              borderRadius: '5px',
              fontWeight: 'bold',
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            [ + เพิ่มหมวดหมู่ ]
          </button>
        </div>

        {/* Category List Pills */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
          <div
            onClick={() => setSelectedCatFilter(null)}
            style={{
              padding: '6px 14px',
              borderRadius: '20px',
              backgroundColor: selectedCatFilter === null ? '#1565C0' : '#ECEFF1',
              color: selectedCatFilter === null ? '#FFF' : '#37474F',
              fontSize: '12px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            ทั้งหมด ({products.length})
          </div>

          {categories.map(c => {
            const isSelected = selectedCatFilter === c.id;
            return (
              <div
                key={c.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '4px 10px',
                  borderRadius: '20px',
                  backgroundColor: isSelected ? '#1565C0' : '#F0F4F8',
                  color: isSelected ? '#FFF' : '#333',
                  border: '1px solid #CFD8DC',
                  fontSize: '12px'
                }}
              >
                <span 
                  onClick={() => setSelectedCatFilter(c.id)} 
                  style={{ cursor: 'pointer', fontWeight: isSelected ? 'bold' : 'normal' }}
                >
                  {c.name} ({c.productCount})
                </span>
                <button
                  onClick={() => handleOpenEditCategory(c)}
                  title="แก้ไขหมวดหมู่นี้"
                  style={{
                    background: 'none',
                    border: 'none',
                    color: isSelected ? '#FFF' : '#1976D2',
                    cursor: 'pointer',
                    fontSize: '11px',
                    fontWeight: 'bold',
                    padding: '0 2px'
                  }}
                >
                  [แก้]
                </button>
                <button
                  onClick={() => handleDeleteCategory(c)}
                  title="ลบหมวดหมู่นี้"
                  style={{
                    background: 'none',
                    border: 'none',
                    color: isSelected ? '#FFCDD2' : '#C62828',
                    cursor: 'pointer',
                    fontSize: '11px',
                    fontWeight: 'bold',
                    padding: '0 2px'
                  }}
                >
                  [ลบ]
                </button>
              </div>
            );
          })}
        </div>
      </div>

      {/* 2. Menu Items Management Section */}
      <div style={{ backgroundColor: '#FFF', padding: '16px 20px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h3 style={{ fontSize: '16px', fontWeight: 'bold' }}>
              รายการอาหารทั้งหมด ({filteredProducts.length} รายการ)
            </h3>
            <p style={{ fontSize: '12px', color: '#666' }}>
              เพิ่ม ลบ แก้ไขรายละเอียด ราคา รูปภาพ และระบุสถานะของหมดพร้อมสาเหตุ
            </p>
          </div>

          <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
            <input
              type="text"
              placeholder="ค้นหารหัส หรือชื่อเมนู..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              style={{
                padding: '7px 12px',
                borderRadius: '4px',
                border: '1px solid #CCC',
                fontSize: '13px',
                width: '180px'
              }}
            />
            <button
              onClick={handleOpenAddProduct}
              style={{
                backgroundColor: '#2E7D32',
                color: '#FFF',
                border: 'none',
                padding: '7px 16px',
                borderRadius: '5px',
                fontWeight: 'bold',
                fontSize: '13px',
                cursor: 'pointer'
              }}
            >
              [ + เพิ่มเมนูอาหารใหม่ ]
            </button>
          </div>
        </div>

        {/* Menu Items Table / Cards Grid */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
          gap: '14px'
        }}>
          {filteredProducts.map(p => {
            const imgUrl = resolveImageUrl(p.imageUrl);
            const isSoldOut = !p.isAvailable;
            return (
              <div
                key={p.id}
                style={{
                  border: isSoldOut ? '1px solid #FFCDD2' : '1px solid #E0E0E0',
                  borderRadius: '8px',
                  padding: '12px',
                  display: 'flex',
                  gap: '12px',
                  backgroundColor: isSoldOut ? '#FFF9F9' : '#FAFAFA',
                  position: 'relative'
                }}
              >
                {/* Thumbnail */}
                <div style={{
                  width: '80px',
                  height: '80px',
                  borderRadius: '6px',
                  backgroundColor: '#EEE',
                  overflow: 'hidden',
                  flexShrink: 0,
                  position: 'relative',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '10px',
                  color: '#999'
                }}>
                  {imgUrl ? (
                    <img src={imgUrl} alt={p.name} style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                  ) : (
                    '[ไม่มีรูป]'
                  )}
                  {isSoldOut && (
                    <div style={{
                      position: 'absolute',
                      top: 0, left: 0, right: 0, bottom: 0,
                      backgroundColor: 'rgba(0,0,0,0.5)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: '#FFF',
                      fontWeight: 'bold',
                      fontSize: '12px'
                    }}>
                      หมด
                    </div>
                  )}
                </div>

                {/* Details */}
                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: '11px', color: '#888' }}>{p.code} | {p.categoryName}</span>
                      {isSoldOut ? (
                        <span style={{ fontSize: '11px', color: '#C62828', fontWeight: 'bold' }}>
                          [ หมด ]
                        </span>
                      ) : (
                        <span style={{ fontSize: '11px', color: '#2E7D32', fontWeight: 'bold' }}>
                          [ พร้อมขาย ]
                        </span>
                      )}
                    </div>
                    <div style={{ fontWeight: 'bold', fontSize: '14px', marginTop: '2px', color: isSoldOut ? '#666' : '#111' }}>
                      {p.name}
                    </div>
                    <div style={{ fontSize: '13px', color: isSoldOut ? '#888' : '#1976D2', fontWeight: 'bold' }}>
                      {p.price.toFixed(2)} บาท
                    </div>
                    {isSoldOut && (
                      <div style={{ fontSize: '11px', color: '#C62828', marginTop: '2px' }}>
                        สาเหตุ: {p.outOfStockReason || 'วัตถุดิบหมด'}
                      </div>
                    )}
                  </div>

                  {/* Actions */}
                  <div style={{ display: 'flex', gap: '6px', marginTop: '8px' }}>
                    <button
                      onClick={() => handleQuickToggleAvailability(p)}
                      title={isSoldOut ? 'เปลี่ยนสถานะเป็นพร้อมขาย' : 'เปลี่ยนสถานะเป็นของหมด'}
                      style={{
                        padding: '3px 8px',
                        backgroundColor: isSoldOut ? '#2E7D32' : '#C62828',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '3px',
                        fontSize: '11px',
                        fontWeight: 'bold',
                        cursor: 'pointer'
                      }}
                    >
                      {isSoldOut ? 'เปิดขาย' : 'ระบุว่าหมด'}
                    </button>
                    <button
                      onClick={() => handleOpenEditProduct(p)}
                      style={{
                        padding: '3px 8px',
                        backgroundColor: '#1976D2',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '3px',
                        fontSize: '11px',
                        fontWeight: 'bold',
                        cursor: 'pointer'
                      }}
                    >
                      แก้ไข
                    </button>
                    <button
                      onClick={() => handleDeleteProduct(p)}
                      style={{
                        padding: '3px 8px',
                        backgroundColor: '#E0E0E0',
                        color: '#C62828',
                        border: 'none',
                        borderRadius: '3px',
                        fontSize: '11px',
                        fontWeight: 'bold',
                        cursor: 'pointer'
                      }}
                    >
                      ลบ
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Category Modal (Create / Edit) */}
      {showCatModal && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            padding: '24px',
            borderRadius: '10px',
            width: '90%',
            maxWidth: '440px',
            boxShadow: '0 10px 30px rgba(0,0,0,0.2)'
          }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '16px' }}>
              {editingCat ? `แก้ไขหมวดหมู่: ${editingCat.name}` : 'เพิ่มหมวดหมู่อาหารใหม่'}
            </h3>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                ชื่อหมวดหมู่:
              </label>
              <input
                type="text"
                placeholder="เช่น อาหารจานด่วน, เครื่องดื่มร้อน"
                value={catName}
                onChange={(e) => setCatName(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                คำอธิบาย / รายละเอียด (ไม่บังคับ):
              </label>
              <input
                type="text"
                placeholder="เช่น เมนูแนะนำประจำร้าน"
                value={catDesc}
                onChange={(e) => setCatDesc(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                ลำดับการแสดงผล (Sort Order):
              </label>
              <input
                type="number"
                value={catSortOrder}
                onChange={(e) => setCatSortOrder(parseInt(e.target.value) || 1)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setShowCatModal(false)}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                ยกเลิก
              </button>
              <button
                onClick={handleSaveCategory}
                disabled={isCatSaving}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#1976D2',
                  color: '#FFF',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {isCatSaving ? 'กำลังบันทึก...' : 'บันทึกหมวดหมู่'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Product Modal (Create / Edit) */}
      {showProdModal && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            padding: '24px',
            borderRadius: '10px',
            width: '90%',
            maxWidth: '540px',
            maxHeight: '90vh',
            overflowY: 'auto',
            boxShadow: '0 10px 30px rgba(0,0,0,0.2)'
          }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '16px' }}>
              {editingProduct ? `แก้ไขเมนูอาหาร: ${editingProduct.name}` : 'เพิ่มเมนูอาหารใหม่'}
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  รหัสเมนู:
                </label>
                <input
                  type="text"
                  placeholder="เช่น M05"
                  value={prodCode}
                  onChange={(e) => setProdCode(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  ชื่อเมนูอาหาร:
                </label>
                <input
                  type="text"
                  placeholder="เช่น กะเพราทะเลรวมมิตร"
                  value={prodName}
                  onChange={(e) => setProdName(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  หมวดหมู่:
                </label>
                <select
                  value={prodCatId}
                  onChange={(e) => setProdCatId(parseInt(e.target.value))}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                >
                  {categories.map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  ราคาขาย (บาท):
                </label>
                <input
                  type="number"
                  value={prodPrice}
                  onChange={(e) => setProdPrice(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px', fontWeight: 'bold' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                แผนกครัวที่ปรุง (Kitchen Station):
              </label>
              <select
                value={prodStation}
                onChange={(e) => setProdStation(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              >
                <option value="MainKitchen">ครัวหลัก (Main Kitchen)</option>
                <option value="Bar">บาร์น้ำ &amp; เครื่องดื่ม (Bar / Beverage)</option>
                <option value="Dessert">ครัวของหวาน (Dessert Station)</option>
              </select>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                คำอธิบายเมนูอาหาร:
              </label>
              <textarea
                rows={2}
                value={prodDesc}
                onChange={(e) => setProdDesc(e.target.value)}
                placeholder="เช่น กุ้ง หมึก หอย ปรุงสดใหม่พร้อมใบกะเพราหอมกรุ่น"
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            {/* Status & Out-Of-Stock Reason */}
            <div style={{
              padding: '14px',
              backgroundColor: prodIsAvailable ? '#F1F8E9' : '#FFEBEE',
              borderRadius: '6px',
              border: prodIsAvailable ? '1px solid #C8E6C9' : '1px solid #FFCDD2',
              marginBottom: '16px'
            }}>
              <label style={{ fontSize: '13px', fontWeight: 'bold', display: 'block', marginBottom: '8px' }}>
                สถานะการจำหน่ายเมนูนี้:
              </label>

              <div style={{ display: 'flex', gap: '20px', marginBottom: '10px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer', fontWeight: 'bold', color: '#2E7D32' }}>
                  <input
                    type="radio"
                    name="prodAvailability"
                    checked={prodIsAvailable}
                    onChange={() => setProdIsAvailable(true)}
                  />
                  พร้อมขาย (In Stock)
                </label>

                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer', fontWeight: 'bold', color: '#C62828' }}>
                  <input
                    type="radio"
                    name="prodAvailability"
                    checked={!prodIsAvailable}
                    onChange={() => setProdIsAvailable(false)}
                  />
                  สินค้าหมด (Sold Out)
                </label>
              </div>

              {/* Reason input only when sold out */}
              {!prodIsAvailable && (
                <div style={{ marginTop: '10px', paddingTop: '10px', borderTop: '1px dashed #FFCDD2' }}>
                  <label style={{ fontSize: '12px', fontWeight: 'bold', color: '#C62828', display: 'block', marginBottom: '4px' }}>
                    ระบุสาเหตุที่สินค้าหมด (แสดงให้ลูกค้าทราบบนหน้าเว็บ):
                  </label>
                  
                  {/* Quick Preset Buttons */}
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', marginBottom: '8px' }}>
                    {['วัตถุดิบหมด', 'หมดชั่วคราว', 'หมดตามฤดูกาล', 'รอส่งของพรุ่งนี้'].map(r => (
                      <button
                        key={r}
                        type="button"
                        onClick={() => setProdOutOfStockReason(r)}
                        style={{
                          padding: '3px 8px',
                          borderRadius: '4px',
                          border: '1px solid #EF9A9A',
                          backgroundColor: prodOutOfStockReason === r ? '#C62828' : '#FFF',
                          color: prodOutOfStockReason === r ? '#FFF' : '#C62828',
                          fontSize: '11px',
                          cursor: 'pointer'
                        }}
                      >
                        {r}
                      </button>
                    ))}
                  </div>

                  <input
                    type="text"
                    value={prodOutOfStockReason}
                    onChange={(e) => setProdOutOfStockReason(e.target.value)}
                    placeholder="เช่น วัตถุดิบกุ้งสดหมด รอเข้าพรุ่งนี้"
                    style={{
                      width: '100%',
                      padding: '8px',
                      borderRadius: '4px',
                      border: '1px solid #EF9A9A',
                      fontSize: '13px'
                    }}
                  />
                </div>
              )}
            </div>

            {/* Image Preview & Upload */}
            <div style={{ marginBottom: '20px', padding: '12px', backgroundColor: '#F9FAFB', borderRadius: '6px', border: '1px solid #EEE' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '6px' }}>
                รูปภาพอาหาร (อัปโหลดขึ้นเซิร์ฟเวอร์):
              </label>

              {prodImageUrl && (
                <div style={{ marginBottom: '10px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <img
                    src={resolveImageUrl(prodImageUrl) || ''}
                    alt="Preview"
                    style={{ width: '100px', height: '80px', objectFit: 'cover', borderRadius: '4px', border: '1px solid #DDD' }}
                  />
                  <span style={{ fontSize: '11px', color: '#666' }}>{prodImageUrl}</span>
                </div>
              )}

              <input
                type="file"
                accept="image/*"
                onChange={handleFileChange}
                disabled={isUploading}
                style={{ fontSize: '12px' }}
              />
              {isUploading && (
                <div style={{ fontSize: '12px', color: '#1976D2', marginTop: '4px', fontWeight: 'bold' }}>
                  กำลังอัปโหลดรูปภาพขึ้นเซิร์ฟเวอร์...
                </div>
              )}
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setShowProdModal(false)}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                ยกเลิก
              </button>
              <button
                onClick={handleSaveProduct}
                disabled={isProdSaving || isUploading}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#1976D2',
                  color: '#FFF',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {isProdSaving ? 'กำลังบันทึก...' : 'บันทึกข้อมูลเมนู'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// -------------------------------------------------------------
// 3.3 Raw Material Stock Management (วัตถุดิบแทนที่จะเป็นรายการอาหาร)
// -------------------------------------------------------------
function StockManagementView() {
  const [ingredients, setIngredients] = useState<IngredientItem[]>([]);
  const [selectedCatFilter, setSelectedCatFilter] = useState<string>('ทั้งหมด');
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Adjust Stock Modal
  const [selectedIngredient, setSelectedIngredient] = useState<IngredientItem | null>(null);
  const [adjustType, setAdjustType] = useState<'in' | 'out' | 'scrap' | 'set'>('in');
  const [adjustQty, setAdjustQty] = useState<number>(10);
  const [adjustReason, setAdjustReason] = useState<string>('รับวัตถุดิบเข้าสต๊อก');
  const [isSubmittingAdjust, setIsSubmittingAdjust] = useState<boolean>(false);

  // Add / Edit Ingredient Modal
  const [showIngModal, setShowIngModal] = useState<boolean>(false);
  const [editingIng, setEditingIng] = useState<IngredientItem | null>(null);
  const [ingCode, setIngCode] = useState<string>('');
  const [ingName, setIngName] = useState<string>('');
  const [ingCategory, setIngCategory] = useState<string>('เนื้อสัตว์และอาหารทะเล');
  const [ingQty, setIngQty] = useState<string>('10');
  const [ingUnit, setIngUnit] = useState<string>('กก.');
  const [ingMinAlert, setIngMinAlert] = useState<string>('5');
  const [ingCost, setIngCost] = useState<string>('0');
  const [ingNotes, setIngNotes] = useState<string>('');
  const [isIngSaving, setIsIngSaving] = useState<boolean>(false);

  const ingredientCategories = [
    'ทั้งหมด',
    'เนื้อสัตว์และอาหารทะเล',
    'ผักและสมุนไพร',
    'เครื่องปรุงและน้ำมัน',
    'เส้นและแป้ง',
    'ข้าวและธัญพืช',
    'ผลไม้และของหวาน',
    'เครื่องดื่มและบาร์',
    'บรรจุภัณฑ์',
    'ของสดและเบ็ดเตล็ด'
  ];

  useEffect(() => {
    loadIngredients();

    const unsub = realtimeService.onIngredientUpdated(() => {
      loadIngredients();
    });
    return () => unsub();
  }, []);

  const loadIngredients = async () => {
    try {
      const list = await getIngredients();
      setIngredients(list);
    } catch (err: any) {
      logError('Failed to load ingredients: ' + err.message, err);
    }
  };

  const handleOpenAdd = () => {
    setEditingIng(null);
    setIngCode(`ING${String(ingredients.length + 1).padStart(3, '0')}`);
    setIngName('');
    setIngCategory('เนื้อสัตว์และอาหารทะเล');
    setIngQty('10');
    setIngUnit('กก.');
    setIngMinAlert('5');
    setIngCost('100');
    setIngNotes('');
    setShowIngModal(true);
  };

  const handleOpenEdit = (ing: IngredientItem) => {
    setEditingIng(ing);
    setIngCode(ing.code);
    setIngName(ing.name);
    setIngCategory(ing.category);
    setIngQty(ing.quantity.toString());
    setIngUnit(ing.unit);
    setIngMinAlert(ing.minQuantityAlert.toString());
    setIngCost(ing.costPrice.toString());
    setIngNotes(ing.notes || '');
    setShowIngModal(true);
  };

  const handleSaveIngredient = async () => {
    if (!ingCode.trim() || !ingName.trim()) {
      alert('กรุณากรอกรหัสและชื่อวัตถุดิบ');
      return;
    }

    setIsIngSaving(true);
    try {
      const payload: Partial<IngredientItem> = {
        code: ingCode.trim().toUpperCase(),
        name: ingName.trim(),
        category: ingCategory.trim(),
        quantity: parseFloat(ingQty) || 0,
        unit: ingUnit.trim() || 'กก.',
        minQuantityAlert: parseFloat(ingMinAlert) || 5,
        costPrice: parseFloat(ingCost) || 0,
        notes: ingNotes.trim() || undefined
      };

      if (editingIng) {
        await updateIngredient(editingIng.id, payload);
        alert('อัปเดตข้อมูลวัตถุดิบสำเร็จ');
      } else {
        await createIngredient(payload);
        alert('เพิ่มวัตถุดิบใหม่เข้าสู่ระบบสำเร็จ');
      }
      setShowIngModal(false);
      await loadIngredients();
    } catch (err: any) {
      alert('เกิดข้อผิดพลาด: ' + err.message);
    } finally {
      setIsIngSaving(false);
    }
  };

  const handleDeleteIngredient = async (ing: IngredientItem) => {
    if (!window.confirm(`ต้องการลบวัตถุดิบ '${ing.name}' (${ing.code}) หรือไม่?`)) return;
    try {
      await deleteIngredient(ing.id);
      alert('ลบรายการวัตถุดิบเรียบร้อยแล้ว');
      await loadIngredients();
    } catch (err: any) {
      alert('ไม่สามารถลบวัตถุดิบได้: ' + err.message);
    }
  };

  const handleOpenAdjust = (ing: IngredientItem) => {
    setSelectedIngredient(ing);
    setAdjustType('in');
    setAdjustQty(10);
    setAdjustReason('รับวัตถุดิบเข้าสต๊อก');
  };

  const handleConfirmAdjust = async () => {
    if (!selectedIngredient) return;
    if (adjustQty <= 0) {
      alert('กรุณาระบุจำนวนที่ถูกต้อง');
      return;
    }

    let finalChange = adjustQty;
    if (adjustType === 'out' || adjustType === 'scrap') {
      finalChange = -adjustQty;
    } else if (adjustType === 'set') {
      finalChange = adjustQty - selectedIngredient.quantity;
    }

    setIsSubmittingAdjust(true);
    try {
      await adjustIngredientStock(selectedIngredient.id, finalChange, adjustReason);
      alert(`ปรับปรุงสต๊อกวัตถุดิบ ${selectedIngredient.name} เรียบร้อยแล้ว`);
      setSelectedIngredient(null);
      await loadIngredients();
    } catch (err: any) {
      alert('เกิดข้อผิดพลาด: ' + err.message);
    } finally {
      setIsSubmittingAdjust(false);
    }
  };

  // KPI Calculations
  const totalItems = ingredients.length;
  const lowStockCount = ingredients.filter(i => i.quantity <= i.minQuantityAlert).length;
  const totalValuation = ingredients.reduce((sum, i) => sum + (i.quantity * i.costPrice), 0);

  const filteredIngredients = ingredients.filter(i => {
    const matchCat = selectedCatFilter === 'ทั้งหมด' || i.category === selectedCatFilter;
    const matchQuery = !searchQuery.trim() || 
      i.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
      i.code.toLowerCase().includes(searchQuery.toLowerCase());
    return matchCat && matchQuery;
  });

  return (
    <div>
      {/* Notice Banner */}
      <div style={{
        backgroundColor: '#E8F5E9',
        borderLeft: '5px solid #2E7D32',
        padding: '12px 16px',
        borderRadius: '6px',
        marginBottom: '16px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
      }}>
        <div>
          <div style={{ fontWeight: 'bold', color: '#1B5E20', fontSize: '14px' }}>
            ระบบจัดการสต๊อกวัตถุดิบ (Raw Material Inventory)
          </div>
          <div style={{ fontSize: '12px', color: '#2E7D32', marginTop: '2px' }}>
            การทำสต๊อกของร้านจะจัดการในระดับ "วัตถุดิบและส่วนประกอบ" (เช่น เนื้อสัตว์, ผัก, เครื่องปรุง, บรรจุภัณฑ์) โดยแยกเป็นอิสระจากรายการอาหาร
          </div>
        </div>
        <button
          onClick={handleOpenAdd}
          style={{
            backgroundColor: '#2E7D32',
            color: '#FFF',
            border: 'none',
            padding: '8px 16px',
            borderRadius: '5px',
            fontWeight: 'bold',
            fontSize: '13px',
            cursor: 'pointer',
            whiteSpace: 'nowrap'
          }}
        >
          [ + เพิ่มวัตถุดิบใหม่ ]
        </button>
      </div>

      {/* KPI Summary Cards */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
        gap: '14px',
        marginBottom: '18px'
      }}>
        <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
          <div style={{ fontSize: '12px', color: '#666' }}>จำนวนวัตถุดิบทั้งหมด</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#1565C0', marginTop: '4px' }}>
            {totalItems} รายการ
          </div>
        </div>

        <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
          <div style={{ fontSize: '12px', color: '#666' }}>วัตถุดิบใกล้หมด (ถึงจุดเตือน)</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: lowStockCount > 0 ? '#C62828' : '#2E7D32', marginTop: '4px' }}>
            {lowStockCount} รายการ
          </div>
        </div>

        <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
          <div style={{ fontSize: '12px', color: '#666' }}>มูลค่าสต๊อกวัตถุดิบรวม</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#2E7D32', marginTop: '4px' }}>
            {totalValuation.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} บาท
          </div>
        </div>
      </div>

      {/* Filter & Search Bar */}
      <div style={{ backgroundColor: '#FFF', padding: '16px', borderRadius: '8px', boxShadow: 'var(--shadow)', marginBottom: '16px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '12px' }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
            {ingredientCategories.map(cat => (
              <button
                key={cat}
                onClick={() => setSelectedCatFilter(cat)}
                style={{
                  padding: '5px 12px',
                  borderRadius: '16px',
                  border: 'none',
                  backgroundColor: selectedCatFilter === cat ? '#1976D2' : '#F0F0F0',
                  color: selectedCatFilter === cat ? '#FFF' : '#333',
                  fontSize: '12px',
                  fontWeight: selectedCatFilter === cat ? 'bold' : 'normal',
                  cursor: 'pointer'
                }}
              >
                {cat}
              </button>
            ))}
          </div>

          <input
            type="text"
            placeholder="ค้นหาวัตถุดิบ (รหัส/ชื่อ)..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              padding: '6px 12px',
              borderRadius: '4px',
              border: '1px solid #CCC',
              fontSize: '13px',
              width: '200px'
            }}
          />
        </div>

        {/* Ingredients Table */}
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid #EEE', backgroundColor: '#F8F9FA', textAlign: 'left' }}>
              <th style={{ padding: '10px' }}>รหัส</th>
              <th style={{ padding: '10px' }}>ชื่อวัตถุดิบ</th>
              <th style={{ padding: '10px' }}>หมวดหมู่</th>
              <th style={{ padding: '10px', textAlign: 'right' }}>คงเหลือ</th>
              <th style={{ padding: '10px', textAlign: 'center' }}>หน่วย</th>
              <th style={{ padding: '10px', textAlign: 'right' }}>จุดเตือน</th>
              <th style={{ padding: '10px', textAlign: 'right' }}>ต้นทุน/หน่วย</th>
              <th style={{ padding: '10px', textAlign: 'right' }}>มูลค่ารวม</th>
              <th style={{ padding: '10px', textAlign: 'center' }}>สถานะ</th>
              <th style={{ padding: '10px', textAlign: 'center' }}>การจัดการ</th>
            </tr>
          </thead>
          <tbody>
            {filteredIngredients.length === 0 ? (
              <tr>
                <td colSpan={10} style={{ padding: '24px', textAlign: 'center', color: '#888' }}>
                  ไม่พบรายการวัตถุดิบตามเงื่อนไขที่เลือก
                </td>
              </tr>
            ) : (
              filteredIngredients.map(ing => {
                const isLow = ing.quantity <= ing.minQuantityAlert;
                const value = ing.quantity * ing.costPrice;
                return (
                  <tr key={ing.id} style={{ borderBottom: '1px solid #F0F0F0', backgroundColor: isLow ? '#FFF9F9' : '#FFF' }}>
                    <td style={{ padding: '10px', fontFamily: 'monospace', fontWeight: 'bold' }}>{ing.code}</td>
                    <td style={{ padding: '10px', fontWeight: 'bold' }}>{ing.name}</td>
                    <td style={{ padding: '10px', color: '#555' }}>{ing.category}</td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 'bold', color: isLow ? '#C62828' : '#1565C0', fontSize: '14px' }}>
                      {ing.quantity.toLocaleString('th-TH', { minimumFractionDigits: 1, maximumFractionDigits: 2 })}
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>{ing.unit}</td>
                    <td style={{ padding: '10px', textAlign: 'right', color: '#666' }}>{ing.minQuantityAlert}</td>
                    <td style={{ padding: '10px', textAlign: 'right' }}>{ing.costPrice.toFixed(2)} บ.</td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 'bold', color: '#2E7D32' }}>
                      {value.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} บ.
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>
                      {isLow ? (
                        <span style={{ backgroundColor: '#FFEBEE', color: '#C62828', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold' }}>
                          [ ใกล้หมด ]
                        </span>
                      ) : (
                        <span style={{ backgroundColor: '#E8F5E9', color: '#2E7D32', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold' }}>
                          [ ปกติ ]
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>
                      <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                        <button
                          onClick={() => handleOpenAdjust(ing)}
                          style={{
                            padding: '4px 10px',
                            backgroundColor: '#2E7D32',
                            color: '#FFF',
                            border: 'none',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 'bold',
                            cursor: 'pointer'
                          }}
                        >
                          ปรับสต๊อก
                        </button>
                        <button
                          onClick={() => handleOpenEdit(ing)}
                          style={{
                            padding: '4px 8px',
                            backgroundColor: '#1976D2',
                            color: '#FFF',
                            border: 'none',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 'bold',
                            cursor: 'pointer'
                          }}
                        >
                          แก้ไข
                        </button>
                        <button
                          onClick={() => handleDeleteIngredient(ing)}
                          style={{
                            padding: '4px 8px',
                            backgroundColor: '#F5F5F5',
                            color: '#C62828',
                            border: '1px solid #CCC',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 'bold',
                            cursor: 'pointer'
                          }}
                        >
                          ลบ
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Adjust Stock Modal */}
      {selectedIngredient && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            padding: '24px',
            borderRadius: '10px',
            width: '90%',
            maxWidth: '460px',
            boxShadow: '0 10px 30px rgba(0,0,0,0.2)'
          }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '6px' }}>
              ปรับยอดสต๊อกวัตถุดิบ: {selectedIngredient.name}
            </h3>
            <p style={{ fontSize: '12px', color: '#666', marginBottom: '14px' }}>
              รหัส: {selectedIngredient.code} | ปัจจุบันคงเหลือ: <strong>{selectedIngredient.quantity} {selectedIngredient.unit}</strong>
            </p>

            {/* Action Type */}
            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '6px' }}>
                ประเภทรายการปรับสต๊อก:
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                <button
                  type="button"
                  onClick={() => {
                    setAdjustType('in');
                    setAdjustReason('รับวัตถุดิบเข้าสต๊อก');
                  }}
                  style={{
                    padding: '8px',
                    borderRadius: '4px',
                    border: '1px solid #2E7D32',
                    backgroundColor: adjustType === 'in' ? '#2E7D32' : '#FFF',
                    color: adjustType === 'in' ? '#FFF' : '#2E7D32',
                    fontWeight: 'bold',
                    fontSize: '12px',
                    cursor: 'pointer'
                  }}
                >
                  [+] รับเข้าสต๊อก
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setAdjustType('out');
                    setAdjustReason('เบิกใช้งานในครัว');
                  }}
                  style={{
                    padding: '8px',
                    borderRadius: '4px',
                    border: '1px solid #1976D2',
                    backgroundColor: adjustType === 'out' ? '#1976D2' : '#FFF',
                    color: adjustType === 'out' ? '#FFF' : '#1976D2',
                    fontWeight: 'bold',
                    fontSize: '12px',
                    cursor: 'pointer'
                  }}
                >
                  [-] เบิกใช้งานในครัว
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setAdjustType('scrap');
                    setAdjustReason('ชำรุด / เสียหาย / เน่าเสีย');
                  }}
                  style={{
                    padding: '8px',
                    borderRadius: '4px',
                    border: '1px solid #C62828',
                    backgroundColor: adjustType === 'scrap' ? '#C62828' : '#FFF',
                    color: adjustType === 'scrap' ? '#FFF' : '#C62828',
                    fontWeight: 'bold',
                    fontSize: '12px',
                    cursor: 'pointer'
                  }}
                >
                  [-] ชำรุด/หมดอายุ
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setAdjustType('set');
                    setAdjustReason('ปรับยอดตามการนับสต๊อกจริง');
                  }}
                  style={{
                    padding: '8px',
                    borderRadius: '4px',
                    border: '1px solid #555',
                    backgroundColor: adjustType === 'set' ? '#555' : '#FFF',
                    color: adjustType === 'set' ? '#FFF' : '#555',
                    fontWeight: 'bold',
                    fontSize: '12px',
                    cursor: 'pointer'
                  }}
                >
                  [=] ปรับยอดนับจริง
                </button>
              </div>
            </div>

            {/* Quantity */}
            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                {adjustType === 'set' ? 'ระบุยอดคงเหลือจริงที่นับได้:' : `จำนวนที่ต้องการปรับ (${selectedIngredient.unit}):`}
              </label>
              <input
                type="number"
                step="0.1"
                value={adjustQty}
                onChange={(e) => setAdjustQty(parseFloat(e.target.value) || 0)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '16px', fontWeight: 'bold' }}
              />
            </div>

            {/* Reason */}
            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                เหตุผลการปรับยอด:
              </label>
              <input
                type="text"
                value={adjustReason}
                onChange={(e) => setAdjustReason(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setSelectedIngredient(null)}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                ยกเลิก
              </button>
              <button
                onClick={handleConfirmAdjust}
                disabled={isSubmittingAdjust}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {isSubmittingAdjust ? 'กำลังบันทึก...' : 'ยืนยันปรับสต๊อก'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add / Edit Ingredient Modal */}
      {showIngModal && (
        <div style={{
          position: 'fixed',
          top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: '#FFF',
            padding: '24px',
            borderRadius: '10px',
            width: '90%',
            maxWidth: '500px',
            boxShadow: '0 10px 30px rgba(0,0,0,0.2)'
          }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '16px' }}>
              {editingIng ? `แก้ไขข้อมูลวัตถุดิบ: ${editingIng.name}` : 'เพิ่มวัตถุดิบใหม่'}
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  รหัสวัตถุดิบ:
                </label>
                <input
                  type="text"
                  placeholder="เช่น ING01"
                  value={ingCode}
                  onChange={(e) => setIngCode(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  ชื่อวัตถุดิบ:
                </label>
                <input
                  type="text"
                  placeholder="เช่น สันคอหมูสไลด์"
                  value={ingName}
                  onChange={(e) => setIngName(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  หมวดหมู่:
                </label>
                <select
                  value={ingCategory}
                  onChange={(e) => setIngCategory(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                >
                  {ingredientCategories.filter(c => c !== 'ทั้งหมด').map(c => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  หน่วยนับ:
                </label>
                <input
                  type="text"
                  placeholder="เช่น กก., ฟอง, ลิตร, ขวด, ห่อ"
                  value={ingUnit}
                  onChange={(e) => setIngUnit(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '10px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  คงเหลือ:
                </label>
                <input
                  type="number"
                  step="0.1"
                  value={ingQty}
                  onChange={(e) => setIngQty(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px', fontWeight: 'bold' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  จุดเตือนขั้นต่ำ:
                </label>
                <input
                  type="number"
                  step="0.1"
                  value={ingMinAlert}
                  onChange={(e) => setIngMinAlert(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  ต้นทุน/หน่วย (บ.):
                </label>
                <input
                  type="number"
                  step="0.1"
                  value={ingCost}
                  onChange={(e) => setIngCost(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '12px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                หมายเหตุเพิ่มเติม:
              </label>
              <input
                type="text"
                placeholder="เช่น สั่งจากฟาร์ม CP ส่งทุกวันอังคาร"
                value={ingNotes}
                onChange={(e) => setIngNotes(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setShowIngModal(false)}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                ยกเลิก
              </button>
              <button
                onClick={handleSaveIngredient}
                disabled={isIngSaving}
                style={{
                  flex: 1,
                  padding: '10px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {isIngSaving ? 'กำลังบันทึก...' : 'บันทึกข้อมูลวัตถุดิบ'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// 3.4 Audit Log Management
function AuditLogManagementView() {
  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    loadLogs();
  }, []);

  const loadLogs = async () => {
    setIsLoading(true);
    try {
      const data = await getAuditLogs(50);
      setLogs(data);
    } catch (err: any) {
      logError('Failed to load audit logs: ' + err.message, err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{ backgroundColor: '#FFF', padding: '20px', borderRadius: '8px', boxShadow: 'var(--shadow)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
        <h3 style={{ fontSize: '16px', fontWeight: 'bold' }}>
          ประวัติการใช้งานและออดิตระบบ (Audit Logs)
        </h3>
        <button
          onClick={loadLogs}
          style={{
            backgroundColor: '#FFF',
            border: '1px solid #CCC',
            padding: '5px 12px',
            borderRadius: '4px',
            fontWeight: 600,
            fontSize: '12px'
          }}
        >
          รีเฟรชประวัติ
        </button>
      </div>

      {isLoading ? (
        <div style={{ padding: '20px', textAlign: 'center', color: '#666' }}>กำลังโหลดข้อมูลประวัติ...</div>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid #EEE', textAlign: 'left', backgroundColor: '#F8F9FA' }}>
              <th style={{ padding: '8px' }}>วัน-เวลา</th>
              <th style={{ padding: '8px' }}>ผู้ใช้งาน</th>
              <th style={{ padding: '8px' }}>การกระทำ (Action)</th>
              <th style={{ padding: '8px' }}>รายละเอียด (Details)</th>
              <th style={{ padding: '8px' }}>IP Address</th>
            </tr>
          </thead>
          <tbody>
            {logs.length === 0 ? (
              <tr>
                <td colSpan={5} style={{ padding: '20px', textAlign: 'center', color: '#888' }}>
                  [ ยังไม่มีประวัติการบันทึก ]
                </td>
              </tr>
            ) : (
              logs.map(log => (
                <tr key={log.id} style={{ borderBottom: '1px solid #F0F0F0' }}>
                  <td style={{ padding: '8px', color: '#666' }}>{new Date(log.createdAt).toLocaleString()}</td>
                  <td style={{ padding: '8px', fontWeight: 'bold', color: '#1976D2' }}>{log.username}</td>
                  <td style={{ padding: '8px', fontWeight: 'bold' }}>{log.action}</td>
                  <td style={{ padding: '8px', color: '#333' }}>{log.details || '-'}</td>
                  <td style={{ padding: '8px', fontFamily: 'monospace', color: '#666' }}>{log.ipAddress || '-'}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}

// -------------------------------------------------------------
// 4. Staff Login Modal
// -------------------------------------------------------------
interface LoginModalProps {
  initialStoreCode?: string;
  onSuccess: (user: AuthUser) => void;
  onClose: () => void;
}

function LoginModal({ initialStoreCode, onSuccess, onClose }: LoginModalProps) {
  const [storeCode, setStoreCode] = useState<string>(() => initialStoreCode || getStoredTenantCode());
  const [username, setUsername] = useState<string>('admin');
  const [password, setPassword] = useState<string>('123456');
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [isLoggingIn, setIsLoggingIn] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password.trim()) {
      setErrorMsg('กรุณากรอกชื่อผู้ใช้และรหัสผ่าน');
      return;
    }

    setIsLoggingIn(true);
    setErrorMsg('');
    try {
      if (storeCode.trim()) {
        setStoredTenantCode(storeCode.trim().toUpperCase());
      }
      const res = await login(username.trim(), password.trim());
      onSuccess(res.user);
    } catch (err: any) {
      setErrorMsg(err.message || 'ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
    } finally {
      setIsLoggingIn(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      top: 0, left: 0, right: 0, bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.6)',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      zIndex: 2000
    }}>
      <div style={{
        backgroundColor: '#FFF',
        padding: '28px',
        borderRadius: '10px',
        width: '90%',
        maxWidth: '400px',
        boxShadow: '0 10px 30px rgba(0,0,0,0.3)'
      }}>
        <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '8px', color: '#0D47A1' }}>
          เข้าสู่ระบบร้านค้า (Store Login)
        </h3>
        <p style={{ fontSize: '12px', color: '#666', marginBottom: '16px' }}>
          เข้าสู่ระบบเพื่อจัดการเมนูอาหาร สต็อกวัตถุดิบ หรือเข้าใช้งานจอครัว (KDS)
        </p>

        {errorMsg && (
          <div style={{
            backgroundColor: '#FFEBEE',
            border: '1px solid #FFCDD2',
            color: '#C62828',
            padding: '8px 12px',
            borderRadius: '4px',
            fontSize: '12px',
            marginBottom: '14px',
            fontWeight: 'bold'
          }}>
            {errorMsg}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: '14px' }}>
            <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
              รหัสร้านค้า (Store Code / Security Key):
            </label>
            <input
              type="text"
              placeholder="เช่น DEFAULT, SHOP1234"
              value={storeCode}
              onChange={(e) => setStoreCode(e.target.value.toUpperCase())}
              style={{
                width: '100%',
                padding: '8px 12px',
                borderRadius: '4px',
                border: '1px solid #CCC',
                fontSize: '14px',
                fontFamily: 'monospace',
                fontWeight: 'bold',
                color: '#1565C0'
              }}
            />
          </div>

          <div style={{ marginBottom: '14px' }}>
            <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
              ชื่อผู้ใช้งาน (Username):
            </label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              style={{ width: '100%', padding: '8px 12px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '14px' }}
            />
          </div>

          <div style={{ marginBottom: '18px' }}>
            <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
              รหัสผ่าน (Password):
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              style={{ width: '100%', padding: '8px 12px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '14px' }}
            />
          </div>

          <div style={{ display: 'flex', gap: '10px' }}>
            <button
              type="button"
              onClick={onClose}
              style={{
                flex: 1,
                padding: '10px',
                borderRadius: '6px',
                border: '1px solid #CCC',
                backgroundColor: '#F5F5F5',
                fontWeight: 'bold'
              }}
            >
              ยกเลิก
            </button>
            <button
              type="submit"
              disabled={isLoggingIn}
              style={{
                flex: 1,
                padding: '10px',
                borderRadius: '6px',
                border: 'none',
                backgroundColor: '#1976D2',
                color: '#FFF',
                fontWeight: 'bold'
              }}
            >
              {isLoggingIn ? 'กำลังตรวจสอบ...' : 'เข้าสู่ระบบ'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// 5. Server Connection & IP/Domain Configuration Modal
// -------------------------------------------------------------
function ServerConfigModal({ onClose }: { onClose: () => void }) {
  const [serverUrlInput, setServerUrlInput] = useState<string>(getServerUrl());
  const [storeCodeInput, setStoreCodeInput] = useState<string>(getStoredTenantCode());
  const [testResult, setTestResult] = useState<string>('');
  const [isTesting, setIsTesting] = useState<boolean>(false);
  const [soundVol, setSoundVol] = useState<number>(getSoundVolume());
  const [soundActive, setSoundActive] = useState<boolean>(isSoundEnabled());

  const handleTest = async () => {
    setIsTesting(true);
    setTestResult('กำลังทดสอบการเชื่อมต่อ...');
    let cleanUrl = serverUrlInput.trim().replace(/\/$/, '');
    if (!cleanUrl.startsWith('http://') && !cleanUrl.startsWith('https://')) {
      cleanUrl = `http://${cleanUrl}`;
    }
    const healthy = await checkServerHealth(cleanUrl);
    setIsTesting(false);

    if (healthy) {
      try {
        const store = await checkStore(storeCodeInput.trim().toUpperCase());
        setTestResult(`เชื่อมต่อสำเร็จ [ร้าน: ${store.storeName} (${store.storeCode})]`);
      } catch {
        setTestResult(`เซิร์ฟเวอร์ออนไลน์ แต่ไม่พบร้านค้า '${storeCodeInput}'`);
      }
    } else {
      setTestResult('เชื่อมต่อไม่สำเร็จ กรุณาตรวจสอบ URL หรือเครือข่าย');
    }
  };

  const handleSave = () => {
    let cleanUrl = serverUrlInput.trim().replace(/\/$/, '');
    if (!cleanUrl.startsWith('http://') && !cleanUrl.startsWith('https://')) {
      cleanUrl = `http://${cleanUrl}`;
    }
    setServerUrl(cleanUrl);
    setStoredTenantCode(storeCodeInput.trim().toUpperCase() || 'DEFAULT');
    setSoundVolume(soundVol);
    setSoundEnabled(soundActive);
    alert(`บันทึกการตั้งค่าการเชื่อมต่อเรียบร้อยแล้ว [ร้านค้า: ${storeCodeInput.trim().toUpperCase() || 'DEFAULT'}]`);
    window.location.reload();
  };

  return (
    <div style={{
      position: 'fixed',
      top: 0, left: 0, right: 0, bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.5)',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      zIndex: 2000
    }}>
      <div style={{
        backgroundColor: '#FFF',
        padding: '24px',
        borderRadius: '10px',
        width: '90%',
        maxWidth: '520px',
        boxShadow: '0 10px 30px rgba(0,0,0,0.25)'
      }}>
        <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '8px' }}>
          ตั้งค่าการเชื่อมต่อเซิร์ฟเวอร์ &amp; รหัสร้านค้า
        </h3>
        <p style={{ fontSize: '12px', color: '#666', marginBottom: '16px' }}>
          กำหนด URL เซิร์ฟเวอร์กลาง และระบุรหัสร้านค้า (Store Code) สำหรับแยกฐานข้อมูลเป็นเอกเทศน์
        </p>

        <div style={{ marginBottom: '12px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
            Server Address (IP / Domain / HTTPS):
          </label>
          <input
            type="text"
            placeholder="เช่น http://192.168.1.100:5000 หรือ https://pos.restaurant.com"
            value={serverUrlInput}
            onChange={(e) => setServerUrlInput(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '14px', fontFamily: 'monospace' }}
          />
          <div style={{ fontSize: '11px', color: '#888', marginTop: '4px' }}>
            ตัวอย่าง: http://127.0.0.1:5000, http://192.168.1.100:5000, https://pos.example.com
          </div>
        </div>

        <div style={{ marginBottom: '14px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
            Store Code (รหัสร้านค้า):
          </label>
          <input
            type="text"
            placeholder="เช่น DEFAULT, SHOP01"
            value={storeCodeInput}
            onChange={(e) => setStoreCodeInput(e.target.value.toUpperCase())}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '14px', fontFamily: 'monospace', fontWeight: 'bold' }}
          />
          <div style={{ fontSize: '11px', color: '#888', marginTop: '4px' }}>
            รหัสร้านค้าเพื่อแยก Database 1 ร้าน ต่อ 1 Database (ค่าเริ่มต้น: DEFAULT)
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '16px' }}>
          <button
            onClick={handleTest}
            disabled={isTesting}
            style={{
              padding: '6px 14px',
              backgroundColor: '#F0F0F0',
              border: '1px solid #CCC',
              borderRadius: '4px',
              fontWeight: 600,
              fontSize: '12px'
            }}
          >
            {isTesting ? 'กำลังทดสอบ...' : 'ทดสอบการเชื่อมต่อ'}
          </button>
          <span style={{ fontSize: '12px', fontWeight: 'bold', color: testResult.includes('สำเร็จ') ? '#2E7D32' : '#D32F2F' }}>
            {testResult}
          </span>
        </div>

        {/* Sound & Alert Volume Settings */}
        <div style={{
          backgroundColor: '#F8F9FA',
          border: '1px solid #E9ECEF',
          borderRadius: '6px',
          padding: '12px 14px',
          marginBottom: '20px'
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <label style={{ fontSize: '13px', fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={soundActive}
                onChange={(e) => {
                  setSoundActive(e.target.checked);
                  setSoundEnabled(e.target.checked);
                }}
              />
              เปิดเสียงแจ้งเตือนออเดอร์ใหม่ (Audio Alert)
            </label>
            <button
              onClick={() => testOrderAlertSound()}
              style={{
                backgroundColor: '#FFF',
                border: '1px solid #CCC',
                padding: '4px 10px',
                borderRadius: '4px',
                fontSize: '11px',
                fontWeight: 'bold',
                cursor: 'pointer'
              }}
            >
              ทดสอบเสียงเตือน
            </button>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{ fontSize: '12px', color: '#666', width: '80px' }}>ความดัง: {soundVol}%</span>
            <input
              type="range"
              min="0"
              max="100"
              value={soundVol}
              onChange={(e) => {
                const val = parseInt(e.target.value, 10);
                setSoundVol(val);
                setSoundVolume(val);
              }}
              style={{ flex: 1, cursor: 'pointer' }}
            />
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            onClick={onClose}
            style={{
              flex: 1,
              padding: '10px',
              borderRadius: '6px',
              border: '1px solid #CCC',
              backgroundColor: '#F5F5F5',
              fontWeight: 'bold'
            }}
          >
            ยกเลิก
          </button>
          <button
            onClick={handleSave}
            style={{
              flex: 1,
              padding: '10px',
              borderRadius: '6px',
              border: 'none',
              backgroundColor: '#1976D2',
              color: '#FFF',
              fontWeight: 'bold'
            }}
          >
            บันทึกและเชื่อมต่อ
          </button>
        </div>
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// 6. Register New Store Modal (Multi-Tenant Onboarding)
// -------------------------------------------------------------
interface RegisterStoreModalProps {
  onClose: () => void;
  onSuccess: (newStore: StoreInfo) => void;
}

function RegisterStoreModal({ onClose, onSuccess }: RegisterStoreModalProps) {
  const [storeCode, setStoreCode] = useState(() => `SHOP${Math.floor(1000 + Math.random() * 9000)}`);
  const [storeName, setStoreName] = useState('');
  const [ownerName, setOwnerName] = useState('');
  const [ownerPhone, setOwnerPhone] = useState('');
  const [adminPassword, setAdminPassword] = useState('123456');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [createdStore, setCreatedStore] = useState<StoreInfo | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);

  const randomizeCode = () => {
    setStoreCode(`SHOP${Math.floor(1000 + Math.random() * 9000)}`);
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(true);
    setTimeout(() => setCopiedKey(false), 2500);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const cleanCode = storeCode.trim().toUpperCase();
    if (!cleanCode) {
      setError('กรุณากรอกหรือสุ่มรหัสเชื่อมต่อร้านค้า');
      return;
    }
    if (!/^[A-Z0-9_-]{3,20}$/.test(cleanCode)) {
      setError('รหัสเชื่อมต่อต้องเป็นตัวอักษรภาษาอังกฤษหรือตัวเลข 3-20 ตัวอักษร');
      return;
    }
    if (!storeName.trim()) {
      setError('กรุณากรอกชื่อร้านอาหารของคุณ');
      return;
    }
    if (!ownerPhone.trim()) {
      setError('กรุณากรอกเบอร์โทรศัพท์ติดต่อ');
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await registerStore({
        storeCode: cleanCode,
        storeName: storeName.trim(),
        ownerName: ownerName.trim() || 'เจ้าของร้าน',
        ownerPhone: ownerPhone.trim(),
        adminUsername: 'admin',
        adminPassword: adminPassword.trim() || '123456'
      });
      setCreatedStore(res);
    } catch (err: any) {
      setError(err.message || 'เกิดข้อผิดพลาดในการลงทะเบียนร้านค้า กรุณาลองใหม่อีกครั้ง');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.65)',
      display: 'flex', justifyContent: 'center', alignItems: 'center',
      zIndex: 2100
    }}>
      <div style={{
        backgroundColor: '#FFF',
        padding: '24px',
        borderRadius: '12px',
        width: '92%',
        maxWidth: '560px',
        maxHeight: '92vh',
        overflowY: 'auto',
        boxShadow: '0 12px 35px rgba(0,0,0,0.35)'
      }}>
        {createdStore ? (
          <div>
            <div style={{
              backgroundColor: '#E8F5E9',
              border: '1px solid #A5D6A7',
              borderRadius: '8px',
              padding: '16px',
              marginBottom: '16px',
              textAlign: 'center'
            }}>
              <h3 style={{ fontSize: '20px', fontWeight: 'bold', color: '#2E7D32', marginBottom: '4px' }}>
                เปิดร้านสำเร็จแล้ว พร้อมเริ่มขายทันที!
              </h3>
              <p style={{ fontSize: '13px', color: '#1B5E20', margin: 0 }}>
                ร้าน "{createdStore.storeName}" ได้รับการเปิดใช้งานและพร้อมเชื่อมต่อกับโปรแกรม POS แล้ว
              </p>
            </div>

            {/* Connection Key Hero Box */}
            <div style={{
              backgroundColor: '#E3F2FD',
              border: '2px dashed #1976D2',
              borderRadius: '8px',
              padding: '16px',
              marginBottom: '16px',
              textAlign: 'center'
            }}>
              <div style={{ fontSize: '12px', fontWeight: 'bold', color: '#0D47A1', marginBottom: '6px' }}>
                รหัสเชื่อมต่อโปรแกรม POS หน้าร้านของคุณ (Connection Key):
              </div>
              <div style={{
                fontFamily: 'Consolas, monospace',
                fontSize: '28px',
                fontWeight: 'bold',
                color: '#1565C0',
                letterSpacing: '2px',
                marginBottom: '10px'
              }}>
                {createdStore.storeCode}
              </div>
              <button
                type="button"
                onClick={() => copyToClipboard(createdStore.storeCode)}
                style={{
                  padding: '6px 16px',
                  borderRadius: '20px',
                  border: 'none',
                  backgroundColor: copiedKey ? '#2E7D32' : '#1976D2',
                  color: '#FFF',
                  fontSize: '12.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {copiedKey ? 'คัดลอกรหัสแล้ว!' : 'คัดลอกรหัสเชื่อมต่อ'}
              </button>
            </div>

            {/* How to Connect 3 Easy Steps */}
            <div style={{
              backgroundColor: '#F8F9FA',
              border: '1px solid #E0E0E0',
              borderRadius: '8px',
              padding: '14px',
              marginBottom: '16px',
              fontSize: '13px'
            }}>
              <div style={{ fontWeight: 'bold', color: '#333', marginBottom: '8px' }}>
                วิธีนำไปเชื่อมต่อกับโปรแกรมขายหน้าร้าน (3 ขั้นตอนง่ายๆ):
              </div>
              <ol style={{ margin: 0, paddingLeft: '20px', lineHeight: '1.7', color: '#444' }}>
                <li>เปิดโปรแกรม <strong>Restaurant POS</strong> บนคอมพิวเตอร์ของคุณ</li>
                <li>ไปที่หน้าต่างเชื่อมต่อ แล้วกรอกรหัสร้าน: <strong style={{ color: '#1565C0' }}>{createdStore.storeCode}</strong></li>
                <li>เข้าสู่ระบบด้วยรหัสผ่านที่คุณตั้งไว้ (<strong style={{ color: '#2E7D32' }}>{adminPassword}</strong>) และเริ่มขายอาหารได้ทันที!</li>
              </ol>

              <div style={{ marginTop: '12px', paddingTop: '10px', borderTop: '1px solid #EEE' }}>
                <div style={{ fontSize: '12px', fontWeight: 'bold', color: '#555', marginBottom: '4px' }}>
                  ลิงก์สำหรับให้ลูกค้าสแกนสั่งอาหารจากมือถือ (โต๊ะ 1):
                </div>
                <div style={{
                  padding: '6px 10px',
                  backgroundColor: '#FFF',
                  border: '1px solid #DDD',
                  borderRadius: '4px',
                  fontFamily: 'monospace',
                  fontSize: '11.5px',
                  wordBreak: 'break-all',
                  color: '#1565C0'
                }}>
                  {window.location.origin}/?store={createdStore.storeCode}&table=T01
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => onSuccess(createdStore)}
                style={{
                  flex: 1,
                  padding: '12px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  fontWeight: 'bold',
                  fontSize: '14px',
                  cursor: 'pointer'
                }}
              >
                เข้าดูหน้าเว็บจัดการร้านค้านี้ทันที
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <h3 style={{ fontSize: '18px', fontWeight: 'bold', color: '#111', margin: 0 }}>
                เปิดร้านใหม่ — ทดลองใช้งานฟรี
              </h3>
              <button
                type="button"
                onClick={onClose}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#888' }}
              >
                [x]
              </button>
            </div>
            <p style={{ fontSize: '12.5px', color: '#666', marginBottom: '16px', lineHeight: '1.4' }}>
              กรอกข้อมูลง่ายๆ ใน 1 นาทีเพื่อรับ <strong>รหัสเชื่อมต่อโปรแกรมหน้าร้าน</strong> และเริ่มทดสอบใช้งานระบบได้ทันที
            </p>

            {error && (
              <div style={{
                backgroundColor: '#FFEBEE',
                border: '1px solid #FFCDD2',
                color: '#C62828',
                padding: '8px 12px',
                borderRadius: '6px',
                fontSize: '12.5px',
                marginBottom: '14px'
              }}>
                {error}
              </div>
            )}

            {/* Store Name */}
            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '12.5px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                ชื่อร้านอาหารของคุณ *
              </label>
              <input
                type="text"
                placeholder="เช่น ครัวคุณแม่, ส้มตำแซ่บ, คาเฟ่ริมน้ำ"
                value={storeName}
                onChange={(e) => setStoreName(e.target.value)}
                style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid #CCC', fontSize: '13px' }}
                required
              />
            </div>

            {/* Owner Details */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '12.5px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  เบอร์โทรศัพท์ติดต่อ *
                </label>
                <input
                  type="tel"
                  placeholder="เช่น 081-234-5678"
                  value={ownerPhone}
                  onChange={(e) => setOwnerPhone(e.target.value)}
                  style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid #CCC', fontSize: '13px' }}
                  required
                />
              </div>
              <div>
                <label style={{ fontSize: '12.5px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                  ชื่อผู้ติดต่อ / เจ้าของร้าน
                </label>
                <input
                  type="text"
                  placeholder="เช่น สมชาย"
                  value={ownerName}
                  onChange={(e) => setOwnerName(e.target.value)}
                  style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid #CCC', fontSize: '13px' }}
                />
              </div>
            </div>

            {/* Admin Password */}
            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '12.5px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
                กำหนดรหัสผ่านเข้าใช้งานโปรแกรม *
              </label>
              <input
                type="text"
                placeholder="เช่น 123456"
                value={adminPassword}
                onChange={(e) => setAdminPassword(e.target.value)}
                style={{ width: '100%', padding: '9px 12px', borderRadius: '6px', border: '1px solid #CCC', fontSize: '13px' }}
                required
              />
              <span style={{ fontSize: '11px', color: '#888' }}>ใช้สำหรับเข้าสู่ระบบในโปรแกรม POS หน้าร้าน (ค่าเริ่มต้น: 123456)</span>
            </div>

            {/* Store Code (Connection Key) */}
            <div style={{ marginBottom: '18px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                <label style={{ fontSize: '12.5px', fontWeight: 'bold' }}>
                  รหัสเชื่อมต่อร้านของคุณ (สร้างให้อัตโนมัติ) *
                </label>
                <button
                  type="button"
                  onClick={randomizeCode}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#1565C0',
                    fontSize: '11.5px',
                    fontWeight: 'bold',
                    cursor: 'pointer',
                    textDecoration: 'underline'
                  }}
                >
                  [สุ่มรหัสใหม่]
                </button>
              </div>
              <input
                type="text"
                value={storeCode}
                onChange={(e) => setStoreCode(e.target.value.toUpperCase())}
                style={{
                  width: '100%',
                  padding: '9px 12px',
                  borderRadius: '6px',
                  border: '1px solid #1976D2',
                  backgroundColor: '#F0F7FF',
                  fontSize: '14px',
                  fontFamily: 'monospace',
                  fontWeight: 'bold',
                  color: '#0D47A1'
                }}
                required
              />
              <span style={{ fontSize: '11px', color: '#666' }}>
                รหัสนี้จะใช้สำหรับนำไปกรอกในโปรแกรม POS บนคอมพิวเตอร์ของคุณเพื่อเชื่อมต่อร้าน
              </span>
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                type="button"
                onClick={onClose}
                disabled={isSubmitting}
                style={{
                  flex: 1,
                  padding: '11px',
                  borderRadius: '6px',
                  border: '1px solid #CCC',
                  backgroundColor: '#F5F5F5',
                  fontWeight: 'bold',
                  fontSize: '13px',
                  cursor: 'pointer'
                }}
              >
                ยกเลิก
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                style={{
                  flex: 2,
                  padding: '11px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  fontWeight: 'bold',
                  fontSize: '13.5px',
                  cursor: isSubmitting ? 'not-allowed' : 'pointer'
                }}
              >
                {isSubmitting ? 'กำลังเปิดร้านค้า...' : 'เปิดร้านและรับรหัสเชื่อมต่อทันที'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

// -------------------------------------------------------------
// 7. Switch Store Modal (Multi-Tenant Switcher)
// -------------------------------------------------------------
interface SwitchStoreModalProps {
  currentCode: string;
  onClose: () => void;
  onSwitch: (store: StoreInfo) => void;
  onOpenRegister: () => void;
}

function SwitchStoreModal({ currentCode, onClose, onSwitch, onOpenRegister }: SwitchStoreModalProps) {
  const [targetCode, setTargetCode] = useState(currentCode);
  const [isChecking, setIsChecking] = useState(false);
  const [error, setError] = useState('');

  const handleSwitch = async () => {
    setError('');
    const clean = targetCode.trim().toUpperCase();
    if (!clean) {
      setError('กรุณากรอกรหัสร้านค้า');
      return;
    }

    setIsChecking(true);
    try {
      const store = await checkStore(clean);
      setStoredTenantCode(clean);
      onSwitch(store);
    } catch {
      setError(`ไม่พบร้านค้าที่มีรหัส '${clean}' หรือถูกระงับการใช้งาน`);
    } finally {
      setIsChecking(false);
    }
  };

  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.6)',
      display: 'flex', justifyContent: 'center', alignItems: 'center',
      zIndex: 2100
    }}>
      <div style={{
        backgroundColor: '#FFF',
        padding: '24px',
        borderRadius: '10px',
        width: '90%',
        maxWidth: '440px',
        boxShadow: '0 10px 30px rgba(0,0,0,0.3)'
      }}>
        <h3 style={{ fontSize: '18px', fontWeight: 'bold', marginBottom: '6px' }}>
          สลับร้านค้า (Switch Store)
        </h3>
        <p style={{ fontSize: '12px', color: '#666', marginBottom: '16px' }}>
          กรอกรหัสร้านค้าที่ต้องการเข้าใช้งาน หรือคลิกเพื่อสมัครเปิดร้านใหม่
        </p>

        {error && (
          <div style={{
            backgroundColor: '#FFEBEE',
            border: '1px solid #FFCDD2',
            color: '#C62828',
            padding: '8px 12px',
            borderRadius: '4px',
            fontSize: '12px',
            marginBottom: '12px'
          }}>
            {error}
          </div>
        )}

        <div style={{ marginBottom: '14px' }}>
          <label style={{ fontSize: '12.5px', fontWeight: 'bold', display: 'block', marginBottom: '4px' }}>
            รหัสร้านค้า (Store Code):
          </label>
          <input
            type="text"
            placeholder="เช่น DEFAULT, SHOP01"
            value={targetCode}
            onChange={(e) => setTargetCode(e.target.value.toUpperCase())}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '4px', border: '1px solid #CCC', fontSize: '14px', fontFamily: 'monospace', fontWeight: 'bold' }}
          />
          <div style={{ fontSize: '11px', color: '#888', marginTop: '4px' }}>
            ร้านปัจจุบัน: <strong>{currentCode}</strong>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
          <button
            onClick={() => setTargetCode('DEFAULT')}
            style={{
              padding: '4px 10px',
              fontSize: '11px',
              borderRadius: '4px',
              border: '1px solid #CCC',
              backgroundColor: '#F0F0F0',
              cursor: 'pointer'
            }}
          >
            ใช้ร้านเริ่มต้น (DEFAULT)
          </button>
        </div>

        <div style={{ display: 'flex', gap: '10px', marginBottom: '14px' }}>
          <button
            onClick={onClose}
            disabled={isChecking}
            style={{
              flex: 1,
              padding: '10px',
              borderRadius: '6px',
              border: '1px solid #CCC',
              backgroundColor: '#F5F5F5',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            ยกเลิก
          </button>
          <button
            onClick={handleSwitch}
            disabled={isChecking}
            style={{
              flex: 1,
              padding: '10px',
              borderRadius: '6px',
              border: 'none',
              backgroundColor: '#1976D2',
              color: '#FFF',
              fontWeight: 'bold',
              cursor: isChecking ? 'not-allowed' : 'pointer'
            }}
          >
            {isChecking ? 'กำลังตรวจสอบ...' : 'เข้าใช้งานร้านนี้'}
          </button>
        </div>

        <div style={{ textAlign: 'center', paddingTop: '10px', borderTop: '1px solid #EEE' }}>
          <button
            onClick={onOpenRegister}
            style={{
              background: 'none',
              border: 'none',
              color: '#2E7D32',
              fontWeight: 'bold',
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            + ยังไม่มีร้านค้า? คลิกเพื่อสมัครเปิดร้านใหม่
          </button>
        </div>
      </div>
    </div>
  );
}

function getStatusText(status: number): string {
  switch (status) {
    case 1: return 'รอรับออเดอร์';
    case 2: return 'รับออเดอร์แล้ว';
    case 3: return 'กำลังปรุง';
    case 4: return 'พร้อมเสิร์ฟ';
    case 5: return 'เสร็จสิ้น';
    case 6: return 'ยกเลิกแล้ว';
    default: return 'ไม่ระบุ';
  }
}

export default App;
