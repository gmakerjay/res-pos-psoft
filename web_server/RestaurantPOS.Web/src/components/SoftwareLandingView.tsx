import React, { useState } from 'react';
import { registerStore } from '../services/api';
import type { StoreInfo } from '../services/api';

interface SoftwareLandingViewProps {
  onOpenLogin: (storeCode?: string) => void;
  onStoreCreated?: (store: StoreInfo) => void;
  onEnterDemo?: (tableOrType?: string) => void;
}

function generateSecureStoreCodeClient(): string {
  const chars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
  const getChunk = (len: number) => {
    let s = "";
    for (let i = 0; i < len; i++) {
      s += chars[Math.floor(Math.random() * chars.length)];
    }
    return s;
  };
  return `RPOS-${getChunk(4)}-${getChunk(4)}-${getChunk(4)}`;
}

export function SoftwareLandingView({ onOpenLogin, onStoreCreated, onEnterDemo }: SoftwareLandingViewProps) {
  const [storeCode, setStoreCode] = useState(() => generateSecureStoreCodeClient());
  const [storeName, setStoreName] = useState('');
  const [ownerName, setOwnerName] = useState('');
  const [ownerPhone, setOwnerPhone] = useState('');
  const [adminPassword, setAdminPassword] = useState('123456');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [createdStore, setCreatedStore] = useState<StoreInfo | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);

  const randomizeCode = () => {
    setStoreCode(generateSecureStoreCodeClient());
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
      setError('กรุณากรอกหรือสุ่มรหัสความปลอดภัยเชื่อมต่อร้าน');
      return;
    }
    if (!/^[A-Z0-9_-]{3,30}$/.test(cleanCode)) {
      setError('รหัสความปลอดภัยต้องเป็นตัวอักษรภาษาอังกฤษหรือตัวเลข 3-30 ตัวอักษร');
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
      if (onStoreCreated) {
        onStoreCreated(res);
      }
    } catch (err: any) {
      setError(err.message || 'เกิดข้อผิดพลาดในการลงทะเบียนร้านค้า กรุณาลองใหม่อีกครั้ง');
    } finally {
      setIsSubmitting(false);
    }
  };

  const resetForm = () => {
    setCreatedStore(null);
    setStoreName('');
    setOwnerName('');
    setOwnerPhone('');
    randomizeCode();
  };

  return (
    <div style={{ width: '100%', padding: '0 12px 40px 12px', boxSizing: 'border-box' }}>
      {/* Hero Section */}
      <section style={{
        textAlign: 'center',
        padding: '32px 12px 24px 12px',
        maxWidth: '860px',
        margin: '0 auto'
      }}>
        <div style={{
          display: 'inline-block',
          backgroundColor: '#E3F2FD',
          color: '#1565C0',
          padding: '4px 14px',
          borderRadius: '20px',
          fontSize: '12px',
          fontWeight: 'bold',
          marginBottom: '14px',
          border: '1px solid #BBDEFB'
        }}>
          [ Windows Desktop POS &amp; Cloud Hub System ]
        </div>

        <h1 style={{
          fontSize: 'clamp(22px, 5vw, 34px)',
          fontWeight: 800,
          color: '#0D47A1',
          lineHeight: '1.3',
          marginBottom: '12px'
        }}>
          ระบบบริหารจัดการร้านอาหาร และจุดขาย POS หน้าร้าน
        </h1>

        <p style={{
          fontSize: 'clamp(14px, 3.5vw, 16px)',
          color: '#455A64',
          lineHeight: '1.6',
          maxWidth: '680px',
          margin: '0 auto 20px auto'
        }}>
          ซอฟต์แวร์ขายหน้าร้านบน Windows สั่งพิมพ์สลิปและใบสั่งครัวอัตโนมัติ 
          พร้อมระบบ QR Code สั่งอาหารประจำโต๊ะ และจอแสดงออเดอร์ในครัว (KDS) เรียลไทม์
        </p>

        {/* Feature Pills */}
        <div style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'center',
          gap: '8px',
          marginBottom: '28px'
        }}>
          <span style={{ backgroundColor: '#ECEFF1', color: '#37474F', padding: '5px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
            [สำหรับ Windows PC]
          </span>
          <span style={{ backgroundColor: '#ECEFF1', color: '#37474F', padding: '5px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
            [พิมพ์สลิป &amp; ใบสั่งครัว]
          </span>
          <span style={{ backgroundColor: '#ECEFF1', color: '#37474F', padding: '5px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
            [QR Code สั่งอาหารประจำโต๊ะ]
          </span>
          <span style={{ backgroundColor: '#ECEFF1', color: '#37474F', padding: '5px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
            [จอครัว KDS เรียลไทม์]
          </span>
          <span style={{ backgroundColor: '#E8F5E9', color: '#2E7D32', padding: '5px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
            [ทดสอบผ่าน Cloud Server ฟรี]
          </span>
        </div>
      </section>

      {/* Live Demo Showcase Section (Dedicated for Sales Pitch & Client Demonstration) */}
      <section style={{
        maxWidth: '920px',
        margin: '0 auto 36px auto',
        backgroundColor: '#FFFFFF',
        borderRadius: '12px',
        boxShadow: '0 4px 16px rgba(0,0,0,0.06)',
        border: '2px solid #90CAF9',
        overflow: 'hidden'
      }}>
        {/* Banner Header */}
        <div style={{
          backgroundColor: '#1565C0',
          color: '#FFF',
          padding: '16px 20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '10px'
        }}>
          <div>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#FFD54F',
              color: '#0D47A1',
              padding: '2px 10px',
              borderRadius: '12px',
              fontSize: '11.5px',
              fontWeight: 'bold',
              marginBottom: '4px'
            }}>
              [ สำหรับการสาธิตและทดลองขายจริง (Live Demo) ]
            </div>
            <h2 style={{ fontSize: '18px', fontWeight: 'bold', margin: '2px 0' }}>
              สัมผัสประสบการณ์ใช้งานระบบจริง (Interactive Live Demo)
            </h2>
            <div style={{ fontSize: '12.5px', color: '#E3F2FD' }}>
              ทดลองสั่งอาหารในมุมมองของลูกค้า หรือเปิดดูจอรับออเดอร์ในครัวแบบเรียลไทม์
            </div>
          </div>

          <button
            type="button"
            onClick={() => onEnterDemo ? onEnterDemo('T01') : (window.location.href = '/?store=DEFAULT&table=T01')}
            style={{
              backgroundColor: '#FFEB3B',
              color: '#0D47A1',
              border: 'none',
              padding: '10px 20px',
              borderRadius: '6px',
              fontSize: '13.5px',
              fontWeight: 'bold',
              cursor: 'pointer',
              boxShadow: '0 2px 6px rgba(0,0,0,0.2)',
              whiteSpace: 'nowrap'
            }}
          >
            [ เปิดหน้าร้านตัวอย่างทันที ]
          </button>
        </div>

        {/* 3 Showcase Action Cards */}
        <div style={{
          padding: '20px',
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
          gap: '16px',
          backgroundColor: '#F8F9FA'
        }}>
          {/* Card 1: Dine-In Table QR */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #BBDEFB',
            borderRadius: '8px',
            padding: '16px',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between'
          }}>
            <div>
              <div style={{
                display: 'inline-block',
                backgroundColor: '#E3F2FD',
                color: '#1565C0',
                padding: '3px 8px',
                borderRadius: '4px',
                fontSize: '11px',
                fontWeight: 'bold',
                marginBottom: '8px'
              }}>
                [ สแกนสั่งอาหารประจำโต๊ะ T01 ]
              </div>
              <h3 style={{ fontSize: '15px', fontWeight: 'bold', color: '#0D47A1', margin: '0 0 6px 0' }}>
                สั่งอาหารที่โต๊ะ (Dine-In QR)
              </h3>
              <p style={{ fontSize: '12.5px', color: '#555', lineHeight: '1.5', margin: '0 0 14px 0' }}>
                จำลองเป็นลูกค้านั่งโต๊ะ 1 สแกน QR ดูรูปภาพเมนู ปรับระดับความเผ็ด และสั่งลงครัว
              </p>
            </div>
            <button
              type="button"
              onClick={() => onEnterDemo ? onEnterDemo('T01') : (window.location.href = '/?store=DEFAULT&table=T01')}
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '6px',
                border: 'none',
                backgroundColor: '#1976D2',
                color: '#FFF',
                fontSize: '13px',
                fontWeight: 'bold',
                cursor: 'pointer'
              }}
            >
              ทดลองสั่งอาหารโต๊ะ 1
            </button>
          </div>

          {/* Card 2: Takeaway Ordering */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #C8E6C9',
            borderRadius: '8px',
            padding: '16px',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between'
          }}>
            <div>
              <div style={{
                display: 'inline-block',
                backgroundColor: '#E8F5E9',
                color: '#2E7D32',
                padding: '3px 8px',
                borderRadius: '4px',
                fontSize: '11px',
                fontWeight: 'bold',
                marginBottom: '8px'
              }}>
                [ สั่งกลับบ้าน / สั่งล่วงหน้า ]
              </div>
              <h3 style={{ fontSize: '15px', fontWeight: 'bold', color: '#1B5E20', margin: '0 0 6px 0' }}>
                สั่งกลับบ้าน (Takeaway QR)
              </h3>
              <p style={{ fontSize: '12.5px', color: '#555', lineHeight: '1.5', margin: '0 0 14px 0' }}>
                สำหรับลูกค้าสั่งล่วงหน้าจากที่บ้าน หรือสั่งกลับบ้านหน้าร้านโดยไม่ต้องระบุเลขโต๊ะ
              </p>
            </div>
            <button
              type="button"
              onClick={() => onEnterDemo ? onEnterDemo('takeaway') : (window.location.href = '/?store=DEFAULT&type=takeaway')}
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '6px',
                border: 'none',
                backgroundColor: '#2E7D32',
                color: '#FFF',
                fontSize: '13px',
                fontWeight: 'bold',
                cursor: 'pointer'
              }}
            >
              ทดลองสั่งกลับบ้าน
            </button>
          </div>

          {/* Card 3: Kitchen KDS & Management */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #FFE082',
            borderRadius: '8px',
            padding: '16px',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between'
          }}>
            <div>
              <div style={{
                display: 'inline-block',
                backgroundColor: '#FFF8E1',
                color: '#F57F17',
                padding: '3px 8px',
                borderRadius: '4px',
                fontSize: '11px',
                fontWeight: 'bold',
                marginBottom: '8px'
              }}>
                [ รหัสทดลอง: admin / psoft123 ]
              </div>
              <h3 style={{ fontSize: '15px', fontWeight: 'bold', color: '#E65100', margin: '0 0 6px 0' }}>
                จอครัว KDS &amp; ระบบจัดการร้าน
              </h3>
              <p style={{ fontSize: '12.5px', color: '#555', lineHeight: '1.5', margin: '0 0 14px 0' }}>
                เข้าดูจอรับออเดอร์ในครัวแบบเรียลไทม์ และระบบสต๊อกวัตถุดิบ/รายงานยอดขาย
              </p>
            </div>
            <button
              type="button"
              onClick={() => onOpenLogin('DEFAULT')}
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '6px',
                border: '1px solid #FFA000',
                backgroundColor: '#FFF8E1',
                color: '#E65100',
                fontSize: '13px',
                fontWeight: 'bold',
                cursor: 'pointer'
              }}
            >
              เข้าดูจอครัว &amp; ระบบจัดการ
            </button>
          </div>
        </div>
      </section>

      {/* Main Registration Card (Front & Center) */}
      <section style={{
        maxWidth: '600px',
        margin: '0 auto 40px auto',
        backgroundColor: '#FFF',
        borderRadius: '12px',
        boxShadow: '0 4px 20px rgba(0,0,0,0.08)',
        border: '1px solid #E0E0E0',
        overflow: 'hidden'
      }}>
        {/* Card Header */}
        <div style={{
          backgroundColor: '#0D47A1',
          color: '#FFF',
          padding: '16px 20px',
          textAlign: 'center'
        }}>
          <h2 style={{ fontSize: '18px', fontWeight: 'bold', margin: '0 0 4px 0' }}>
            {createdStore ? 'ลงทะเบียนร้านค้าสำเร็จแล้ว' : 'ลงทะเบียนขอรับรหัสความปลอดภัย ทดลองใช้ฟรี'}
          </h2>
          <p style={{ fontSize: '12.5px', color: '#BBDEFB', margin: 0 }}>
            {createdStore 
              ? 'นำรหัสความปลอดภัยนี้ไปเชื่อมต่อในโปรแกรม POS บนคอมพิวเตอร์ของคุณ'
              : 'กรอกข้อมูลง่ายๆ เพื่อรับรหัสเชื่อมต่อ (Store Code) เริ่มต้นใช้งานทันที'}
          </p>
        </div>

        {/* Card Body */}
        <div style={{ padding: '24px 20px' }}>
          {createdStore ? (
            <div>
              {/* Success Banner */}
              <div style={{
                backgroundColor: '#E8F5E9',
                border: '1px solid #A5D6A7',
                borderRadius: '8px',
                padding: '14px',
                marginBottom: '20px',
                textAlign: 'center'
              }}>
                <div style={{ fontSize: '16px', fontWeight: 'bold', color: '#2E7D32', marginBottom: '4px' }}>
                  [ ลงทะเบียนสำเร็จ ] ยินดีต้อนรับร้าน "{createdStore.storeName}"
                </div>
                <div style={{ fontSize: '12.5px', color: '#1B5E20' }}>
                  ระบบได้สร้างฐานข้อมูลแยกเฉพาะสำหรับร้านของคุณเรียบร้อยแล้ว
                </div>
              </div>

              {/* Security Key Box */}
              <div style={{
                backgroundColor: '#E3F2FD',
                border: '2px dashed #1976D2',
                borderRadius: '10px',
                padding: '18px',
                marginBottom: '20px',
                textAlign: 'center'
              }}>
                <div style={{ fontSize: '12.5px', fontWeight: 'bold', color: '#0D47A1', marginBottom: '6px' }}>
                  รหัสความปลอดภัยสำหรับเชื่อมต่อโปรแกรม (Security Key / Store Code):
                </div>
                <div style={{
                  fontFamily: 'Consolas, monospace',
                  fontSize: 'clamp(24px, 6vw, 32px)',
                  fontWeight: 'bold',
                  color: '#1565C0',
                  letterSpacing: '2px',
                  marginBottom: '10px'
                }}>
                  {createdStore.storeCode}
                </div>

                <div style={{ display: 'flex', justifyContent: 'center', gap: '8px', flexWrap: 'wrap', marginBottom: '14px' }}>
                  <span style={{ backgroundColor: '#E8F5E9', color: '#2E7D32', border: '1px solid #A5D6A7', padding: '4px 12px', borderRadius: '12px', fontSize: '12px', fontWeight: 'bold' }}>
                    [ สิทธิ์ใช้งาน: ทดลองใช้ฟรี 14 วัน (เต็มทุกฟังก์ชัน) ]
                  </span>
                  <span style={{ backgroundColor: '#FFF3E0', color: '#E65100', border: '1px solid #FFE082', padding: '4px 12px', borderRadius: '12px', fontSize: '12px', fontWeight: 'bold' }}>
                    [ ฐานข้อมูลแยกเฉพาะร้าน ]
                  </span>
                </div>

                <button
                  type="button"
                  onClick={() => copyToClipboard(createdStore.storeCode)}
                  style={{
                    padding: '8px 20px',
                    borderRadius: '20px',
                    border: 'none',
                    backgroundColor: copiedKey ? '#2E7D32' : '#1976D2',
                    color: '#FFF',
                    fontSize: '13px',
                    fontWeight: 'bold',
                    cursor: 'pointer'
                  }}
                >
                  {copiedKey ? 'คัดลอกรหัสแล้ว!' : 'คัดลอกรหัสความปลอดภัย'}
                </button>
              </div>

              {/* How to Connect 3 Easy Steps */}
              <div style={{
                backgroundColor: '#F8F9FA',
                border: '1px solid #E0E0E0',
                borderRadius: '8px',
                padding: '16px',
                marginBottom: '20px',
                fontSize: '13px'
              }}>
                <div style={{ fontWeight: 'bold', color: '#333', marginBottom: '10px', fontSize: '13.5px' }}>
                  วิธีนำไปเชื่อมต่อกับโปรแกรม POS (3 ขั้นตอนง่ายๆ):
                </div>
                <ol style={{ margin: 0, paddingLeft: '20px', lineHeight: '1.8', color: '#444' }}>
                  <li>
                    เปิดโปรแกรม <strong>Restaurant POS</strong> บนเครื่องคอมพิวเตอร์ Windows หน้าร้าน
                  </li>
                  <li>
                    ในหน้าต่างตั้งค่าการเชื่อมต่อ ให้กรอกรหัสร้าน: <strong style={{ color: '#1565C0' }}>{createdStore.storeCode}</strong> 
                    {' '}และเซิร์ฟเวอร์: <code style={{ backgroundColor: '#E0E0E0', padding: '2px 5px', borderRadius: '3px' }}>https://spk.p-services.net</code>
                  </li>
                  <li>
                    เข้าสู่ระบบด้วยรหัสผ่านที่คุณตั้งไว้ (<strong style={{ color: '#2E7D32' }}>{adminPassword}</strong>) และเริ่มขายอาหารได้ทันที!
                  </li>
                </ol>

                <div style={{ marginTop: '14px', paddingTop: '12px', borderTop: '1px solid #E0E0E0' }}>
                  <div style={{ fontSize: '12px', fontWeight: 'bold', color: '#555', marginBottom: '4px' }}>
                    ลิงก์สำหรับให้ลูกค้าสแกนสั่งอาหารประจำโต๊ะ (โต๊ะ 1):
                  </div>
                  <div style={{
                    padding: '8px 10px',
                    backgroundColor: '#FFF',
                    border: '1px solid #DDD',
                    borderRadius: '4px',
                    fontFamily: 'monospace',
                    fontSize: '12px',
                    wordBreak: 'break-all',
                    color: '#1565C0',
                    marginBottom: '8px'
                  }}>
                    {window.location.origin}/?store={createdStore.storeCode}&amp;table=T01
                  </div>
                  <div style={{ fontSize: '11.5px', color: '#666' }}>
                    * ในโปรแกรม POS หน้าร้าน คุณสามารถสั่งพิมพ์สติ๊กเกอร์ QR Code ติดโต๊ะสำหรับโต๊ะต่างๆ ได้อัตโนมัติ
                  </div>
                </div>
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                <a
                  href={`/?store=${createdStore.storeCode}&table=T01`}
                  style={{
                    display: 'block',
                    textAlign: 'center',
                    padding: '12px',
                    borderRadius: '6px',
                    backgroundColor: '#2E7D32',
                    color: '#FFF',
                    fontWeight: 'bold',
                    fontSize: '14px',
                    textDecoration: 'none'
                  }}
                >
                  ทดลองเปิดหน้าสั่งอาหารของร้านนี้ (โต๊ะ T01)
                </a>

                <a
                  href={`/?store=${createdStore.storeCode}&view=qr`}
                  style={{
                    display: 'block',
                    textAlign: 'center',
                    padding: '12px',
                    borderRadius: '6px',
                    backgroundColor: '#0D47A1',
                    color: '#FFF',
                    fontWeight: 'bold',
                    fontSize: '14px',
                    textDecoration: 'none'
                  }}
                >
                  [ พิมพ์ป้าย QR Code ติดโต๊ะอาหาร ]
                </a>


                <button
                  type="button"
                  onClick={() => onOpenLogin(createdStore.storeCode)}
                  style={{
                    padding: '12px',
                    borderRadius: '6px',
                    border: '1px solid #1976D2',
                    backgroundColor: '#FFF',
                    color: '#1976D2',
                    fontWeight: 'bold',
                    fontSize: '14px'
                  }}
                >
                  เข้าสู่ระบบจัดการร้านบนเว็บ (Login)
                </button>

                <button
                  type="button"
                  onClick={resetForm}
                  style={{
                    padding: '10px',
                    borderRadius: '6px',
                    border: 'none',
                    backgroundColor: '#F5F5F5',
                    color: '#666',
                    fontSize: '12.5px'
                  }}
                >
                  + ลงทะเบียนร้านอื่นเพิ่มเติม
                </button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              {error && (
                <div style={{
                  backgroundColor: '#FFEBEE',
                  border: '1px solid #FFCDD2',
                  color: '#C62828',
                  padding: '10px 14px',
                  borderRadius: '6px',
                  fontSize: '13px',
                  marginBottom: '16px',
                  fontWeight: 600
                }}>
                  {error}
                </div>
              )}

              {/* Store Name Field */}
              <div style={{ marginBottom: '16px' }}>
                <label style={{ fontSize: '13px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '6px' }}>
                  ชื่อร้านอาหารของคุณ: <span style={{ color: '#D32F2F' }}>*</span>
                </label>
                <input
                  type="text"
                  placeholder="เช่น ร้านครัวริมน้ำ, ข้าวมันไก่สยาม"
                  value={storeName}
                  onChange={(e) => setStoreName(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    borderRadius: '6px',
                    border: '1px solid #B0BEC5',
                    fontSize: '14px',
                    backgroundColor: '#FAFAFA'
                  }}
                />
              </div>

              {/* Owner Phone Field */}
              <div style={{ marginBottom: '16px' }}>
                <label style={{ fontSize: '13px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '6px' }}>
                  เบอร์โทรศัพท์ติดต่อ: <span style={{ color: '#D32F2F' }}>*</span>
                </label>
                <input
                  type="tel"
                  placeholder="เช่น 081-234-5678"
                  value={ownerPhone}
                  onChange={(e) => setOwnerPhone(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    borderRadius: '6px',
                    border: '1px solid #B0BEC5',
                    fontSize: '14px',
                    backgroundColor: '#FAFAFA'
                  }}
                />
              </div>

              {/* Owner Name & Admin Password Row */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '14px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '6px' }}>
                    ชื่อเจ้าของร้าน / ผู้ดูแล:
                  </label>
                  <input
                    type="text"
                    placeholder="เช่น คุณสมชาย"
                    value={ownerName}
                    onChange={(e) => setOwnerName(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '10px 12px',
                      borderRadius: '6px',
                      border: '1px solid #B0BEC5',
                      fontSize: '14px',
                      backgroundColor: '#FAFAFA'
                    }}
                  />
                </div>

                <div>
                  <label style={{ fontSize: '13px', fontWeight: 'bold', color: '#333', display: 'block', marginBottom: '6px' }}>
                    รหัสผ่านสำหรับเข้า POS:
                  </label>
                  <input
                    type="password"
                    placeholder="ค่าเริ่มต้น 123456"
                    value={adminPassword}
                    onChange={(e) => setAdminPassword(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '10px 12px',
                      borderRadius: '6px',
                      border: '1px solid #B0BEC5',
                      fontSize: '14px',
                      backgroundColor: '#FAFAFA'
                    }}
                  />
                </div>
              </div>

              {/* Security Key / Store Code Field */}
              <div style={{
                backgroundColor: '#F1F8E9',
                border: '1px solid #C5E1A5',
                borderRadius: '8px',
                padding: '14px',
                marginBottom: '20px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px', flexWrap: 'wrap', gap: '6px' }}>
                  <label style={{ fontSize: '13px', fontWeight: 'bold', color: '#2E7D32' }}>
                    รหัสความปลอดภัยสำหรับเชื่อมต่อ (Store Code):
                  </label>
                  <button
                    type="button"
                    onClick={randomizeCode}
                    style={{
                      padding: '4px 10px',
                      borderRadius: '4px',
                      border: '1px solid #81C784',
                      backgroundColor: '#FFF',
                      color: '#2E7D32',
                      fontSize: '11.5px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    สุ่มรหัสใหม่
                  </button>
                </div>

                <input
                  type="text"
                  value={storeCode}
                  onChange={(e) => setStoreCode(e.target.value.toUpperCase())}
                  style={{
                    width: '100%',
                    padding: '10px 12px',
                    borderRadius: '6px',
                    border: '1px solid #A5D6A7',
                    fontSize: '16px',
                    fontFamily: 'Consolas, monospace',
                    fontWeight: 'bold',
                    color: '#1B5E20',
                    backgroundColor: '#FFF',
                    letterSpacing: '1px'
                  }}
                />
                <div style={{ fontSize: '11.5px', color: '#2E7D32', marginTop: '6px', lineHeight: '1.5' }}>
                  * รหัสความปลอดภัยความยาวสูง (รูปแบบ RPOS-XXXX-XXXX-XXXX) เพื่อความปลอดภัยสูงสุด ป้องกันการสุ่มเดา พร้อมสิทธิ์ทดลองใช้งานฟรี 14 วันเต็มรูปแบบ
                </div>
              </div>

              {/* Submit Button */}
              <button
                type="submit"
                disabled={isSubmitting}
                style={{
                  width: '100%',
                  padding: '14px',
                  borderRadius: '6px',
                  border: 'none',
                  backgroundColor: '#0D47A1',
                  color: '#FFF',
                  fontSize: '15px',
                  fontWeight: 'bold',
                  boxShadow: '0 3px 10px rgba(13, 71, 161, 0.3)',
                  cursor: isSubmitting ? 'not-allowed' : 'pointer',
                  opacity: isSubmitting ? 0.75 : 1
                }}
              >
                {isSubmitting ? 'กำลังลงทะเบียนร้านค้า...' : 'ลงทะเบียนและรับรหัสความปลอดภัยทันที'}
              </button>
            </form>
          )}
        </div>
      </section>

      {/* Workflow & Architecture Concept Section */}
      <section style={{
        maxWidth: '960px',
        margin: '0 auto 40px auto',
        padding: '0 10px'
      }}>
        <div style={{ textAlign: 'center', marginBottom: '24px' }}>
          <h2 style={{ fontSize: '20px', fontWeight: 800, color: '#0D47A1', marginBottom: '6px' }}>
            โฟลวการทำงานของระบบ Restaurant POS
          </h2>
          <p style={{ fontSize: '13px', color: '#546E7A', margin: 0 }}>
            ขั้นตอนง่ายๆ ตั้งแต่ลงทะเบียน เชื่อมต่อโปรแกรม จนถึงลูกค้าสแกนสั่งอาหาร
          </p>
        </div>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
          gap: '14px',
          marginBottom: '24px'
        }}>
          {/* Step 1 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '16px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.04)'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#E3F2FD',
              color: '#1565C0',
              padding: '3px 8px',
              borderRadius: '4px',
              fontSize: '11.5px',
              fontWeight: 'bold',
              marginBottom: '8px'
            }}>
              ขั้นตอนที่ 1
            </div>
            <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
              ลงทะเบียนรับรหัส
            </div>
            <div style={{ fontSize: '12.5px', color: '#666', lineHeight: '1.5' }}>
              สมัครสมาชิกบนหน้าเว็บเพื่อรับรหัสความปลอดภัย (Security Key) ประจำร้านของคุณ
            </div>
          </div>

          {/* Step 2 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '16px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.04)'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#E8F5E9',
              color: '#2E7D32',
              padding: '3px 8px',
              borderRadius: '4px',
              fontSize: '11.5px',
              fontWeight: 'bold',
              marginBottom: '8px'
            }}>
              ขั้นตอนที่ 2
            </div>
            <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
              เชื่อมต่อโปรแกรม POS
            </div>
            <div style={{ fontSize: '12.5px', color: '#666', lineHeight: '1.5' }}>
              นำรหัสที่ได้ไปกรอกในโปรแกรม Restaurant POS บนเครื่องคอมพิวเตอร์ Windows หน้าร้าน
            </div>
          </div>

          {/* Step 3 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '16px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.04)'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#FFF3E0',
              color: '#E65100',
              padding: '3px 8px',
              borderRadius: '4px',
              fontSize: '11.5px',
              fontWeight: 'bold',
              marginBottom: '8px'
            }}>
              ขั้นตอนที่ 3
            </div>
            <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
              พิมพ์ QR Code ติดโต๊ะ
            </div>
            <div style={{ fontSize: '12.5px', color: '#666', lineHeight: '1.5' }}>
              โปรแกรม POS จะพิมพ์สติ๊กเกอร์ QR Code ประจำแต่ละโต๊ะให้ลูกค้านั่งที่ร้านสแกนสั่ง
            </div>
          </div>

          {/* Step 4 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '16px',
            boxShadow: '0 2px 6px rgba(0,0,0,0.04)'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#F3E5F5',
              color: '#6A1B9A',
              padding: '3px 8px',
              borderRadius: '4px',
              fontSize: '11.5px',
              fontWeight: 'bold',
              marginBottom: '8px'
            }}>
              ขั้นตอนที่ 4
            </div>
            <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#333', marginBottom: '4px' }}>
              รับออเดอร์ &amp; ตัดสต็อก
            </div>
            <div style={{ fontSize: '12.5px', color: '#666', lineHeight: '1.5' }}>
              ลูกค้าสแกนสั่งจากมือถือ ออเดอร์เด้งเข้าหน้าแคชเชียร์และจอครัว KDS อัตโนมัติทันที
            </div>
          </div>
        </div>

        {/* Important Platform Clarification Notice */}
        <div style={{
          backgroundColor: '#FFFDE7',
          border: '1px solid #FFF59D',
          borderRadius: '8px',
          padding: '14px 18px',
          fontSize: '13px',
          color: '#F57F17',
          lineHeight: '1.6'
        }}>
          <strong>[ ข้อชี้แจงสำคัญ ]</strong> เราคือผู้พัฒนาซอฟต์แวร์ระบบขายหน้าร้าน (POS) 
          และไม่ใช่แพลตฟอร์มสั่งอาหารเดลิเวอรี่ส่วนกลางที่รวมทุกร้านค้าเข้าด้วยกัน 
          แต่ละร้านค้าจะมีรหัสความปลอดภัยและลิงก์ QR Code แยกออกจากกันอย่างเป็นส่วนตัว 
          ลูกค้าของร้านจะเห็นเฉพาะเมนูของร้านนั้นๆ เมื่อสแกน QR Code ประจำโต๊ะที่ร้านจัดเตรียมไว้ให้เท่านั้น
        </div>
      </section>

      {/* Feature Showcase Grid */}
      <section style={{
        maxWidth: '960px',
        margin: '0 auto 40px auto',
        padding: '0 10px'
      }}>
        <div style={{ textAlign: 'center', marginBottom: '24px' }}>
          <h2 style={{ fontSize: '20px', fontWeight: 800, color: '#0D47A1', marginBottom: '6px' }}>
            จุดเด่นสำคัญของ Restaurant POS
          </h2>
          <p style={{ fontSize: '13px', color: '#546E7A', margin: 0 }}>
            ออกแบบมาเพื่อร้านอาหารขนาดเล็กจนถึงร้านอาหารขนาดใหญ่
          </p>
        </div>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
          gap: '16px'
        }}>
          {/* Card 1 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '18px',
            boxShadow: '0 2px 8px rgba(0,0,0,0.04)'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#0D47A1', marginBottom: '6px' }}>
              [ โปรแกรม Windows Desktop POS ]
            </div>
            <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
              สไตล์ Classic รวดเร็ว แม่นยำ ไม่หน่วง รองรับเครื่องพิมพ์สลิปความร้อน (ESC/POS) 
              สั่งพิมพ์ใบเสร็จและใบแจ้งครัวแยกตามสเตชั่น คุมสถานะโต๊ะ เช็คบิล และปิดกะรายวัน
            </p>
          </div>

          {/* Card 2 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '18px',
            boxShadow: '0 2px 8px rgba(0,0,0,0.04)'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#1565C0', marginBottom: '6px' }}>
              [ QR Code สั่งอาหารประจำโต๊ะ ]
            </div>
            <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
              ลูกค้านั่งที่โต๊ะเปิดกล้องมือถือสแกนสั่งอาหารได้ทันที โดยไม่ต้องดาวน์โหลดแอปพลิเคชัน 
              ลดภาระพนักงานเสิร์ฟและลดความผิดพลาดในการจดรายการอาหาร
            </p>
          </div>

          {/* Card 3 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '18px',
            boxShadow: '0 2px 8px rgba(0,0,0,0.04)'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#2E7D32', marginBottom: '6px' }}>
              [ จอแสดงออเดอร์ในครัว KDS ]
            </div>
            <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
              หน้าจอสำหรับแผนกครัวและบาร์น้ำ อัปเดตออเดอร์ใหม่แบบเรียลไทม์ผ่าน SignalR 
              พร้อมเสียงเตือนอัตโนมัติ กดรับออเดอร์และแจ้งอาหารปรุงเสร็จได้อย่างรวดเร็ว
            </p>
          </div>

          {/* Card 4 */}
          <div style={{
            backgroundColor: '#FFF',
            border: '1px solid #E0E0E0',
            borderRadius: '8px',
            padding: '18px',
            boxShadow: '0 2px 8px rgba(0,0,0,0.04)'
          }}>
            <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#E65100', marginBottom: '6px' }}>
              [ ระบบตัดสต็อก &amp; รายงานยอดขาย ]
            </div>
            <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
              ตัดสต็อกวัตถุดิบอัตโนมัติตามสัดส่วนที่ตั้งไว้ในเมนู พร้อมรายงานยอดขายรายวัน 
              สรุปช่วงเวลาขายดี และประวัติการทำรายการอย่างครบถ้วน
            </p>
          </div>
        </div>
      </section>

      {/* Demo Callout Banner */}
      <section style={{
        maxWidth: '800px',
        margin: '0 auto',
        backgroundColor: '#E8EAF6',
        border: '1px solid #C5CAE9',
        borderRadius: '10px',
        padding: '20px',
        textAlign: 'center'
      }}>
        <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#283593', marginBottom: '6px' }}>
          ต้องการทดสอบระบบสั่งอาหารของลูกค้าจำลองใช่หรือไม่?
        </div>
        <p style={{ fontSize: '13px', color: '#3949AB', marginBottom: '14px' }}>
          คุณสามารถทดลองใช้งานระบบสั่งอาหารผ่าน QR Code ในมุมมองของลูกค้าที่นั่งโต๊ะ 1 ได้ทันที
        </p>
        <a
          href="/?store=DEFAULT&amp;table=T01"
          style={{
            display: 'inline-block',
            padding: '10px 24px',
            backgroundColor: '#303F9F',
            color: '#FFF',
            textDecoration: 'none',
            borderRadius: '6px',
            fontWeight: 'bold',
            fontSize: '13.5px'
          }}
        >
          ทดลองเปิดหน้าสั่งอาหารของร้านตัวอย่าง (DEMO Store)
        </a>
      </section>
    </div>
  );
}
