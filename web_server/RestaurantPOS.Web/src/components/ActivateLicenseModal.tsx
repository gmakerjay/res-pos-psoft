import { useState, type FormEvent } from 'react';
import { activateStoreLicense, getStoredTenantCode } from '../services/api';
import type { StoreInfo } from '../services/api';

interface ActivateLicenseModalProps {
  isOpen: boolean;
  onClose: () => void;
  storeInfo: StoreInfo | null;
  onActivated?: () => void;
}

export function ActivateLicenseModal({ isOpen, onClose, storeInfo, onActivated }: ActivateLicenseModalProps) {
  const currentCode = storeInfo?.storeCode || getStoredTenantCode();
  const [storeCode, setStoreCode] = useState(currentCode);
  const [licenseKey, setLicenseKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    const cleanKey = licenseKey.trim().toUpperCase();
    if (!cleanKey) {
      setError('กรุณากรอกรหัสเปิดใช้งาน (Activation Key)');
      return;
    }

    setLoading(true);
    try {
      await activateStoreLicense({
        storeCode: storeCode.trim().toUpperCase(),
        licenseKey: cleanKey
      });
      setSuccess(true);
      if (onActivated) {
        onActivated();
      }
    } catch (err: any) {
      setError(err.message || 'รหัสเปิดใช้งานไม่ถูกต้อง หรือไม่ตรงกับรหัสร้านค้านี้');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      backgroundColor: 'rgba(0,0,0,0.6)',
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
        maxWidth: '520px',
        boxShadow: '0 8px 30px rgba(0,0,0,0.25)',
        overflow: 'hidden'
      }}>
        {/* Header */}
        <div style={{
          backgroundColor: '#0D47A1',
          color: '#FFF',
          padding: '14px 18px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <div style={{ fontSize: '16px', fontWeight: 'bold' }}>
            [ เปิดใช้งานสิทธิ์ Restaurant POS (Activate License) ]
          </div>
          <button
            onClick={onClose}
            style={{
              backgroundColor: 'rgba(255,255,255,0.2)',
              border: 'none',
              color: '#FFF',
              padding: '4px 10px',
              borderRadius: '4px',
              fontSize: '12px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            [ปิด]
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: '20px' }}>
          {success ? (
            <div style={{ textAlign: 'center', padding: '16px 0' }}>
              <div style={{
                backgroundColor: '#E8F5E9',
                color: '#2E7D32',
                padding: '16px',
                borderRadius: '8px',
                border: '1px solid #A5D6A7',
                marginBottom: '16px',
                fontWeight: 'bold',
                fontSize: '15px'
              }}>
                [ เปิดใช้งานสิทธิ์สำเร็จเรียบร้อยแล้ว ]
              </div>
              <p style={{ fontSize: '13.5px', color: '#555', lineHeight: '1.6' }}>
                ระบบได้อัปเดตสิทธิ์การใช้งานของร้าน <strong>{storeCode}</strong> เรียบร้อยแล้ว
                คุณสามารถใช้งานซอฟต์แวร์ได้ตามสิทธิ์ที่ได้รับทันที
              </p>
              <button
                type="button"
                onClick={() => {
                  onClose();
                  window.location.reload();
                }}
                style={{
                  marginTop: '12px',
                  padding: '10px 24px',
                  backgroundColor: '#2E7D32',
                  color: '#FFF',
                  border: 'none',
                  borderRadius: '4px',
                  fontSize: '14px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                ตกลง / รีเฟรชระบบ
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              {/* Current Status Box */}
              {storeInfo && (
                <div style={{
                  backgroundColor: '#F5F5F5',
                  borderRadius: '6px',
                  padding: '12px',
                  marginBottom: '16px',
                  fontSize: '12.5px',
                  border: '1px solid #E0E0E0'
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                    <span style={{ color: '#666' }}>ร้านค้า:</span>
                    <strong style={{ color: '#0D47A1' }}>{storeInfo.storeName} ({storeInfo.storeCode})</strong>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                    <span style={{ color: '#666' }}>สิทธิ์ปัจจุบัน:</span>
                    <strong style={{ color: storeInfo.subscriptionPlan === 'FullLifetime' ? '#2E7D32' : '#E65100' }}>
                      {storeInfo.subscriptionPlan === 'FullLifetime' ? '[เวอร์ชันเต็ม ตลอดชีพ]' : (storeInfo.subscriptionPlan === 'FullYearly' ? '[เวอร์ชันเต็ม รายปี]' : `[ทดลองใช้: เหลือ ${storeInfo.daysRemaining || 0} วัน]`)}
                    </strong>
                  </div>
                  {storeInfo.isExpired && (
                    <div style={{ color: '#D32F2F', fontWeight: 'bold', marginTop: '4px' }}>
                      * สิทธิ์ทดลองใช้งานหมดอายุแล้ว กรุณากรอกรหัสเปิดใช้งานเพื่อปลดล็อก
                    </div>
                  )}
                </div>
              )}

              {error && (
                <div style={{
                  backgroundColor: '#FFEBEE',
                  border: '1px solid #FFCDD2',
                  color: '#C62828',
                  padding: '10px',
                  borderRadius: '4px',
                  fontSize: '13px',
                  marginBottom: '14px',
                  fontWeight: 'bold'
                }}>
                  {error}
                </div>
              )}

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
                  รหัสร้านค้า (Store Code):
                </label>
                <input
                  type="text"
                  value={storeCode}
                  onChange={(e) => setStoreCode(e.target.value.toUpperCase())}
                  style={{
                    width: '100%',
                    padding: '9px 12px',
                    borderRadius: '4px',
                    border: '1px solid #B0BEC5',
                    fontSize: '14px',
                    fontFamily: 'monospace',
                    fontWeight: 'bold',
                    boxSizing: 'border-box'
                  }}
                />
              </div>

              <div style={{ marginBottom: '20px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
                  รหัสเปิดใช้งาน (Activation Key):
                </label>
                <input
                  type="text"
                  placeholder="เช่น ACT-LFE-XXXX-XXXX"
                  value={licenseKey}
                  onChange={(e) => setLicenseKey(e.target.value.toUpperCase())}
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    borderRadius: '4px',
                    border: '2px solid #1976D2',
                    fontSize: '15px',
                    fontFamily: 'Consolas, monospace',
                    fontWeight: 'bold',
                    color: '#0D47A1',
                    letterSpacing: '1px',
                    boxSizing: 'border-box'
                  }}
                />
                <div style={{ fontSize: '11.5px', color: '#666', marginTop: '6px' }}>
                  * รหัส Activation Key ได้รับจากผู้พัฒนาหรือฝ่ายขายสำหรับร้านของคุณโดยเฉพาะ
                </div>
              </div>

              <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  onClick={onClose}
                  style={{
                    padding: '9px 18px',
                    borderRadius: '4px',
                    border: '1px solid #B0BEC5',
                    backgroundColor: '#FFF',
                    color: '#555',
                    fontSize: '13px',
                    fontWeight: 'bold',
                    cursor: 'pointer'
                  }}
                >
                  ยกเลิก
                </button>
                <button
                  type="submit"
                  disabled={loading || !licenseKey.trim()}
                  style={{
                    padding: '9px 24px',
                    borderRadius: '4px',
                    border: 'none',
                    backgroundColor: !licenseKey.trim() ? '#B0BEC5' : '#2E7D32',
                    color: '#FFF',
                    fontSize: '13px',
                    fontWeight: 'bold',
                    cursor: !licenseKey.trim() ? 'not-allowed' : 'pointer'
                  }}
                >
                  {loading ? 'กำลังตรวจสอบคีย์...' : 'เปิดใช้งาน (Activate)'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
