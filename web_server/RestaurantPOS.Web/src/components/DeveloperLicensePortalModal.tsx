import { useState, useEffect } from 'react';
import {
  getAllStores,
  upgradeStoreLicense,
  generateLicenseKey,
  toggleStoreStatus,
  isDevUnlocked,
  unlockDevMode,
  type TenantItem,
  type GenerateKeyResponse
} from '../services/api';

interface DeveloperLicensePortalModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function DeveloperLicensePortalModal({ isOpen, onClose }: DeveloperLicensePortalModalProps) {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => isDevUnlocked());
  const [inputPin, setInputPin] = useState<string>('');
  const [authError, setAuthError] = useState<string>('');
  const [stores, setStores] = useState<TenantItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [search, setSearch] = useState('');

  // Key Generator State
  const [targetStoreCode, setTargetStoreCode] = useState('');
  const [targetPlan, setTargetPlan] = useState('FullLifetime');
  const [generatedKeyResult, setGeneratedKeyResult] = useState<GenerateKeyResponse | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      if (isDevUnlocked()) {
        setIsAuthenticated(true);
        loadStores();
      } else {
        setIsAuthenticated(false);
        setInputPin('');
        setAuthError('');
      }
    }
  }, [isOpen]);

  const handleVerifyPin = (e: React.FormEvent) => {
    e.preventDefault();
    if (unlockDevMode(inputPin)) {
      setIsAuthenticated(true);
      setAuthError('');
      loadStores();
    } else {
      setAuthError('รหัสผ่านนักพัฒนาไม่ถูกต้อง กรุณากรอกใหม่อีกครั้ง');
    }
  };

  const loadStores = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getAllStores();
      setStores(data);
    } catch (err: any) {
      setError(err.message || 'ไม่สามารถโหลดรายชื่อร้านค้าได้');
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateKey = async (storeCode: string, plan: string) => {
    setActionLoading(`keygen-${storeCode}`);
    setError('');
    setSuccessMsg('');
    try {
      const res = await generateLicenseKey({ storeCode, plan });
      setGeneratedKeyResult(res);
      setSuccessMsg(`สร้าง Activation Key สำหรับร้าน ${storeCode} สำเร็จ`);
    } catch (err: any) {
      setError(err.message || 'สร้าง Activation Key ล้มเหลว');
    } finally {
      setActionLoading(null);
    }
  };

  const handleUpgradeStore = async (storeCode: string, plan: string, extendDays: number) => {
    const planName = plan === 'FullLifetime' ? 'เวอร์ชันเต็ม ตลอดชีพ' : (plan === 'FullYearly' ? 'เวอร์ชันเต็ม รายปี (+1 ปี)' : `ต่ออายุ ${extendDays} วัน`);
    if (!window.confirm(`ยืนยันการเปลี่ยนสิทธิ์ร้าน ${storeCode} เป็น "${planName}" หรือไม่?`)) {
      return;
    }

    setActionLoading(`upgrade-${storeCode}`);
    setError('');
    setSuccessMsg('');
    try {
      await upgradeStoreLicense({ storeCode, plan, extendDays });
      setSuccessMsg(`อัปเดตสิทธิ์ร้าน ${storeCode} สำเร็จ`);
      await loadStores();
    } catch (err: any) {
      setError(err.message || 'เกิดข้อผิดพลาดในการอัปเดตสิทธิ์');
    } finally {
      setActionLoading(null);
    }
  };

  const handleToggleStatus = async (storeCode: string, currentStatus: boolean) => {
    const nextStatus = !currentStatus;
    const actionText = nextStatus ? 'เปิดใช้งาน' : 'ระงับสิทธิ์';
    if (!window.confirm(`ยืนยันการ ${actionText} ร้าน ${storeCode} หรือไม่?`)) {
      return;
    }

    setActionLoading(`status-${storeCode}`);
    setError('');
    setSuccessMsg('');
    try {
      await toggleStoreStatus({ storeCode, isActive: nextStatus });
      setSuccessMsg(`เปลี่ยนสถานะร้าน ${storeCode} เป็น [${actionText}] สำเร็จ`);
      await loadStores();
    } catch (err: any) {
      setError(err.message || 'เกิดข้อผิดพลาดในการเปลี่ยนสถานะ');
    } finally {
      setActionLoading(null);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(true);
    setTimeout(() => setCopiedKey(false), 2500);
  };

  if (!isOpen) return null;

  if (!isAuthenticated) {
    return (
      <div style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0,0,0,0.7)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 9999,
        padding: '16px'
      }}>
        <div style={{
          backgroundColor: '#FFF',
          borderRadius: '8px',
          width: '100%',
          maxWidth: '420px',
          boxShadow: '0 10px 30px rgba(0,0,0,0.3)',
          overflow: 'hidden'
        }}>
          <div style={{
            backgroundColor: '#0D47A1',
            color: '#FFF',
            padding: '12px 18px',
            fontSize: '15px',
            fontWeight: 'bold',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center'
          }}>
            <span>[ ยืนยันสิทธิ์นักพัฒนา (Developer PIN) ]</span>
            <button
              onClick={onClose}
              style={{
                background: 'none',
                border: 'none',
                color: '#FFF',
                fontSize: '16px',
                cursor: 'pointer'
              }}
            >
              [x]
            </button>
          </div>
          <form onSubmit={handleVerifyPin} style={{ padding: '20px' }}>
            <p style={{ fontSize: '13px', color: '#555', marginTop: 0, marginBottom: '14px', lineHeight: '1.5' }}>
              ส่วนนี้สำหรับผู้ดูแลระบบและทีมพัฒนาซอฟต์แวร์เท่านั้น เพื่อความปลอดภัยกรุณากรอกรหัสผ่านนักพัฒนาเพื่อเข้าสู่ศูนย์จัดการคีย์
            </p>
            {authError && (
              <div style={{
                backgroundColor: '#FFEBEE',
                color: '#C62828',
                border: '1px solid #FFCDD2',
                padding: '8px 12px',
                borderRadius: '4px',
                fontSize: '12.5px',
                marginBottom: '12px',
                fontWeight: 'bold'
              }}>
                {authError}
              </div>
            )}
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 'bold', marginBottom: '6px', color: '#333' }}>
                รหัสผ่านนักพัฒนา (Developer PIN):
              </label>
              <input
                type="password"
                placeholder="กรอกรหัสผ่านนักพัฒนา..."
                value={inputPin}
                onChange={(e) => setInputPin(e.target.value)}
                autoFocus
                style={{
                  width: '100%',
                  padding: '9px 12px',
                  borderRadius: '4px',
                  border: '1px solid #CCC',
                  fontSize: '14px'
                }}
              />
            </div>
            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                type="button"
                onClick={onClose}
                style={{
                  flex: 1,
                  padding: '9px',
                  borderRadius: '4px',
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
                style={{
                  flex: 1,
                  padding: '9px',
                  borderRadius: '4px',
                  border: 'none',
                  backgroundColor: '#0D47A1',
                  color: '#FFF',
                  fontWeight: 'bold',
                  fontSize: '13px',
                  cursor: 'pointer'
                }}
              >
                ยืนยันรหัสผ่าน
              </button>
            </div>
          </form>
        </div>
      </div>
    );
  }

  const filteredStores = stores.filter(s =>
    s.storeCode.toLowerCase().includes(search.toLowerCase()) ||
    s.storeName.toLowerCase().includes(search.toLowerCase()) ||
    s.ownerName.toLowerCase().includes(search.toLowerCase()) ||
    s.ownerPhone.includes(search)
  );

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      backgroundColor: 'rgba(0,0,0,0.65)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 9999,
      padding: '16px'
    }}>
      <div style={{
        backgroundColor: '#FFF',
        borderRadius: '10px',
        width: '100%',
        maxWidth: '1100px',
        maxHeight: '92vh',
        display: 'flex',
        flexDirection: 'column',
        boxShadow: '0 8px 32px rgba(0,0,0,0.3)',
        overflow: 'hidden'
      }}>
        {/* Modal Header */}
        <div style={{
          backgroundColor: '#0D47A1',
          color: '#FFF',
          padding: '14px 20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          borderBottom: '2px solid #1565C0'
        }}>
          <div>
            <div style={{ fontSize: '18px', fontWeight: 'bold' }}>
              [ ศูนย์จัดการคีย์และสิทธิ์ใช้งานร้านค้า (Developer License Portal) ]
            </div>
            <div style={{ fontSize: '12px', color: '#BBDEFB', marginTop: '2px' }}>
              จัดการสิทธิ์ Free Trial, Upgrade เวอร์ชันเต็ม และสร้าง Activation Key ส่งให้ลูกค้า
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              backgroundColor: 'rgba(255,255,255,0.2)',
              border: 'none',
              color: '#FFF',
              padding: '6px 12px',
              borderRadius: '4px',
              fontSize: '13px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            [ปิด]
          </button>
        </div>

        {/* Modal Body */}
        <div style={{ padding: '16px 20px', overflowY: 'auto', flex: 1, backgroundColor: '#F8F9FA' }}>
          {error && (
            <div style={{
              backgroundColor: '#FFEBEE',
              border: '1px solid #FFCDD2',
              color: '#C62828',
              padding: '10px 14px',
              borderRadius: '6px',
              fontSize: '13px',
              marginBottom: '14px',
              fontWeight: 'bold'
            }}>
              {error}
            </div>
          )}

          {successMsg && (
            <div style={{
              backgroundColor: '#E8F5E9',
              border: '1px solid #C8E6C9',
              color: '#2E7D32',
              padding: '10px 14px',
              borderRadius: '6px',
              fontSize: '13px',
              marginBottom: '14px',
              fontWeight: 'bold'
            }}>
              {successMsg}
            </div>
          )}

          {/* Quick Key Generator Card */}
          <div style={{
            backgroundColor: '#FFF',
            borderRadius: '8px',
            border: '1px solid #BBDEFB',
            padding: '16px',
            marginBottom: '18px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.04)'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#0D47A1', marginBottom: '10px' }}>
              [ เครื่องมือสร้าง Activation Key (Key Generator) ]
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '10px', alignItems: 'flex-end' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '4px' }}>
                  รหัสร้านค้า (Store Code):
                </label>
                <input
                  type="text"
                  placeholder="เช่น RPOS-8K2N-9X4M-7B1Q หรือ DEFAULT"
                  value={targetStoreCode}
                  onChange={(e) => setTargetStoreCode(e.target.value.toUpperCase())}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '4px',
                    border: '1px solid #B0BEC5',
                    fontFamily: 'monospace',
                    fontSize: '13px',
                    boxSizing: 'border-box'
                  }}
                />
              </div>

              <div>
                <label style={{ fontSize: '12px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '4px' }}>
                  แพ็กเกจสิทธิ์ (Plan):
                </label>
                <select
                  value={targetPlan}
                  onChange={(e) => setTargetPlan(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '4px',
                    border: '1px solid #B0BEC5',
                    fontSize: '13px',
                    boxSizing: 'border-box'
                  }}
                >
                  <option value="FullLifetime">เวอร์ชันเต็ม ตลอดชีพ (Full Lifetime)</option>
                  <option value="FullYearly">เวอร์ชันเต็ม รายปี 1 ปี (Full Yearly)</option>
                  <option value="Extend30">ต่ออายุการใช้งาน 30 วัน (Extend 30 Days)</option>
                </select>
              </div>

              <div>
                <button
                  type="button"
                  disabled={!targetStoreCode.trim() || actionLoading === 'manual-keygen'}
                  onClick={() => handleGenerateKey(targetStoreCode, targetPlan)}
                  style={{
                    width: '100%',
                    padding: '9px 14px',
                    borderRadius: '4px',
                    border: 'none',
                    backgroundColor: !targetStoreCode.trim() ? '#B0BEC5' : '#1565C0',
                    color: '#FFF',
                    fontSize: '13px',
                    fontWeight: 'bold',
                    cursor: !targetStoreCode.trim() ? 'not-allowed' : 'pointer'
                  }}
                >
                  {actionLoading === 'manual-keygen' ? 'กำลังสร้างคีย์...' : 'สร้าง Activation Key'}
                </button>
              </div>
            </div>

            {/* Generated Key Display Box */}
            {generatedKeyResult && (
              <div style={{
                marginTop: '14px',
                padding: '14px',
                backgroundColor: '#E8F5E9',
                border: '1px solid #81C784',
                borderRadius: '6px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px', flexWrap: 'wrap', gap: '8px' }}>
                  <div>
                    <span style={{ fontSize: '12px', fontWeight: 'bold', color: '#2E7D32' }}>
                      [ Activation Key สำเร็จ ] ร้าน: {generatedKeyResult.storeCode}
                    </span>
                    <span style={{ marginLeft: '8px', fontFamily: 'monospace', fontSize: '16px', fontWeight: 'bold', color: '#1B5E20' }}>
                      {generatedKeyResult.licenseKey}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(generatedKeyResult.messageTemplate)}
                    style={{
                      padding: '5px 12px',
                      borderRadius: '4px',
                      border: 'none',
                      backgroundColor: copiedKey ? '#2E7D32' : '#0D47A1',
                      color: '#FFF',
                      fontSize: '12px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    {copiedKey ? 'คัดลอกข้อความส่งลูกค้าแล้ว!' : 'คัดลอกข้อความส่งลูกค้า'}
                  </button>
                </div>
                <pre style={{
                  margin: 0,
                  padding: '10px',
                  backgroundColor: '#FFF',
                  border: '1px solid #C8E6C9',
                  borderRadius: '4px',
                  fontSize: '12px',
                  whiteSpace: 'pre-wrap',
                  wordBreak: 'break-all',
                  fontFamily: 'Consolas, monospace',
                  color: '#333'
                }}>
                  {generatedKeyResult.messageTemplate}
                </pre>
              </div>
            )}
          </div>

          {/* Stores List Header & Search */}
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '12px',
            flexWrap: 'wrap',
            gap: '10px'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#333' }}>
              รายชื่อร้านค้าทั้งหมด ({stores.length} ร้าน)
            </div>
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <input
                type="text"
                placeholder="ค้นหารหัสร้าน, ชื่อร้าน, เจ้าของ, เบอร์โทร..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                style={{
                  padding: '6px 12px',
                  borderRadius: '4px',
                  border: '1px solid #B0BEC5',
                  fontSize: '13px',
                  width: '260px'
                }}
              />
              <button
                type="button"
                onClick={loadStores}
                disabled={loading}
                style={{
                  padding: '6px 12px',
                  borderRadius: '4px',
                  border: '1px solid #90CAF9',
                  backgroundColor: '#E3F2FD',
                  color: '#1565C0',
                  fontSize: '12px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                {loading ? 'รีเฟรช...' : 'รีเฟรช'}
              </button>
            </div>
          </div>

          {/* Stores Table */}
          <div style={{
            backgroundColor: '#FFF',
            borderRadius: '8px',
            border: '1px solid #E0E0E0',
            overflowX: 'auto'
          }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12.5px', textAlign: 'left' }}>
              <thead>
                <tr style={{ backgroundColor: '#ECEFF1', borderBottom: '2px solid #CFD8DC', color: '#37474F' }}>
                  <th style={{ padding: '10px 12px' }}>รหัสร้าน (Store Code)</th>
                  <th style={{ padding: '10px 12px' }}>ชื่อร้าน / ผู้ดูแล</th>
                  <th style={{ padding: '10px 12px' }}>เบอร์โทร</th>
                  <th style={{ padding: '10px 12px' }}>สิทธิ์ (Plan)</th>
                  <th style={{ padding: '10px 12px' }}>วันหมดอายุ</th>
                  <th style={{ padding: '10px 12px' }}>สถานะ</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>การจัดการสิทธิ์</th>
                </tr>
              </thead>
              <tbody>
                {filteredStores.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: '#78909C' }}>
                      {loading ? 'กำลังโหลดข้อมูลร้านค้า...' : 'ไม่พบข้อมูลร้านค้า'}
                    </td>
                  </tr>
                ) : (
                  filteredStores.map(store => {
                    const isLifetime = store.subscriptionPlan === 'FullLifetime' || store.subscriptionPlan === 'Enterprise';
                    const expires = store.expiresAt ? new Date(store.expiresAt) : null;
                    const isExpired = expires ? (expires.getTime() < Date.now()) : false;
                    const diffDays = expires ? Math.max(0, Math.ceil((expires.getTime() - Date.now()) / (1000 * 60 * 60 * 24))) : 0;

                    return (
                      <tr key={store.id} style={{ borderBottom: '1px solid #ECEFF1' }}>
                        <td style={{ padding: '10px 12px', fontFamily: 'monospace', fontWeight: 'bold', color: '#1565C0' }}>
                          {store.storeCode}
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <div style={{ fontWeight: 'bold', color: '#333' }}>{store.storeName}</div>
                          <div style={{ fontSize: '11px', color: '#757575' }}>{store.ownerName}</div>
                        </td>
                        <td style={{ padding: '10px 12px', color: '#555' }}>
                          {store.ownerPhone || '-'}
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          {isLifetime ? (
                            <span style={{ backgroundColor: '#E8F5E9', color: '#2E7D32', padding: '3px 8px', borderRadius: '4px', fontWeight: 'bold', fontSize: '11.5px' }}>
                              [ตลอดชีพ Lifetime]
                            </span>
                          ) : store.subscriptionPlan === 'FullYearly' ? (
                            <span style={{ backgroundColor: '#E1F5FE', color: '#0277BD', padding: '3px 8px', borderRadius: '4px', fontWeight: 'bold', fontSize: '11.5px' }}>
                              [รายปี Yearly]
                            </span>
                          ) : (
                            <span style={{ backgroundColor: '#FFF3E0', color: '#E65100', padding: '3px 8px', borderRadius: '4px', fontWeight: 'bold', fontSize: '11.5px' }}>
                              [ทดลองใช้ Trial]
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          {isLifetime ? (
                            <span style={{ color: '#2E7D32', fontWeight: 'bold' }}>ไม่มีวันหมดอายุ</span>
                          ) : expires ? (
                            <div>
                              <div style={{ color: isExpired ? '#D32F2F' : '#333', fontWeight: isExpired ? 'bold' : 'normal' }}>
                                {expires.toLocaleDateString('th-TH')}
                              </div>
                              <div style={{ fontSize: '11px', color: isExpired ? '#D32F2F' : '#757575' }}>
                                {isExpired ? '[หมดอายุแล้ว]' : `เหลืออีก ${diffDays} วัน`}
                              </div>
                            </div>
                          ) : (
                            <span style={{ color: '#9E9E9E' }}>-</span>
                          )}
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          {store.isActive ? (
                            <span style={{ backgroundColor: '#E8F5E9', color: '#2E7D32', padding: '2px 6px', borderRadius: '3px', fontWeight: 'bold', fontSize: '11px' }}>
                              [เปิดใช้งาน]
                            </span>
                          ) : (
                            <span style={{ backgroundColor: '#FFEBEE', color: '#C62828', padding: '2px 6px', borderRadius: '3px', fontWeight: 'bold', fontSize: '11px' }}>
                              [ระงับสิทธิ์]
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '8px 12px' }}>
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center', flexWrap: 'wrap' }}>
                            {/* 1-Click Upgrade to Lifetime */}
                            <button
                              type="button"
                              onClick={() => handleUpgradeStore(store.storeCode, 'FullLifetime', 0)}
                              disabled={actionLoading === `upgrade-${store.storeCode}`}
                              title="ปลดล็อกเป็นเวอร์ชันเต็มตลอดชีพทันที"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: 'none',
                                backgroundColor: '#2E7D32',
                                color: '#FFF',
                                fontSize: '11px',
                                fontWeight: 'bold',
                                cursor: 'pointer'
                              }}
                            >
                              [ปลดล็อกตลอดชีพ]
                            </button>

                            {/* Extend 1 Year */}
                            <button
                              type="button"
                              onClick={() => handleUpgradeStore(store.storeCode, 'FullYearly', 365)}
                              disabled={actionLoading === `upgrade-${store.storeCode}`}
                              title="ต่ออายุ 1 ปี"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: '1px solid #1976D2',
                                backgroundColor: '#FFF',
                                color: '#1976D2',
                                fontSize: '11px',
                                fontWeight: 'bold',
                                cursor: 'pointer'
                              }}
                            >
                              [+1 ปี]
                            </button>

                            {/* Extend Trial 14 Days */}
                            <button
                              type="button"
                              onClick={() => handleUpgradeStore(store.storeCode, 'Trial', 14)}
                              disabled={actionLoading === `upgrade-${store.storeCode}`}
                              title="ต่อเวลาทดลองใช้เพิ่ม 14 วัน"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: '1px solid #FFA000',
                                backgroundColor: '#FFF8E1',
                                color: '#E65100',
                                fontSize: '11px',
                                fontWeight: 'bold',
                                cursor: 'pointer'
                              }}
                            >
                              [+14 วัน]
                            </button>

                            {/* Quick KeyGen for this store */}
                            <button
                              type="button"
                              onClick={() => {
                                setTargetStoreCode(store.storeCode);
                                handleGenerateKey(store.storeCode, 'FullLifetime');
                              }}
                              title="สร้าง Activation Key ส่งให้ร้านนี้"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: 'none',
                                backgroundColor: '#0D47A1',
                                color: '#FFF',
                                fontSize: '11px',
                                fontWeight: 'bold',
                                cursor: 'pointer'
                              }}
                            >
                              [สร้างคีย์ KeyGen]
                            </button>

                            {/* Suspend / Resume Status */}
                            <button
                              type="button"
                              onClick={() => handleToggleStatus(store.storeCode, store.isActive)}
                              disabled={actionLoading === `status-${store.storeCode}`}
                              title={store.isActive ? "ระงับการใช้งานร้านนี้ชั่วคราว" : "เปิดให้ใช้งานตามปกติ"}
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: 'none',
                                backgroundColor: store.isActive ? '#C62828' : '#388E3C',
                                color: '#FFF',
                                fontSize: '11px',
                                fontWeight: 'bold',
                                cursor: 'pointer'
                              }}
                            >
                              {store.isActive ? '[ระงับ]' : '[เปิด]'}
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
        </div>

        {/* Modal Footer */}
        <div style={{
          backgroundColor: '#ECEFF1',
          padding: '12px 20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          borderTop: '1px solid #CFD8DC'
        }}>
          <div style={{ fontSize: '12px', color: '#546E7A' }}>
            * ระบบ Activation Key ใช้อัลกอริทึม HMAC-SHA256 ป้องกันการปลอมแปลงและไม่ต้องพึ่งพาโปรแกรม Keygen ภายนอก
          </div>
          <button
            onClick={onClose}
            style={{
              padding: '6px 18px',
              backgroundColor: '#37474F',
              color: '#FFF',
              border: 'none',
              borderRadius: '4px',
              fontSize: '13px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            ปิดหน้าต่าง
          </button>
        </div>
      </div>
    </div>
  );
}
