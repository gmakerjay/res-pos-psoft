import React, { useState } from 'react';
import { registerStore, setStoredTenantCode } from '../services/api';
import type { StoreInfo } from '../services/api';
import { getServerUrl } from '../services/logger';

interface RegisterStorePageProps {
  onBackToHome?: () => void;
  onOpenLogin?: (storeCode?: string) => void;
  onStoreCreated?: (store: StoreInfo) => void;
}

function generateRandomShopCode(): string {
  const num = Math.floor(1000 + Math.random() * 9000);
  return `SHOP${num}`;
}

function generateSecureStoreCode(): string {
  const chars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
  const getChunk = (len: number) => {
    let s = "";
    for (let i = 0; i < len; i++) {
      s += chars[Math.floor(Math.random() * chars.length)];
    }
    return s;
  };
  return `RPOS-${getChunk(4)}-${getChunk(4)}`;
}

export function RegisterStorePage({ onBackToHome, onOpenLogin, onStoreCreated }: RegisterStorePageProps) {
  // Form States
  const [storeName, setStoreName] = useState<string>('');
  const [storeCode, setStoreCode] = useState<string>(() => generateRandomShopCode());
  const [adminPassword, setAdminPassword] = useState<string>('123456');
  const [confirmPassword, setConfirmPassword] = useState<string>('123456');
  const [address, setAddress] = useState<string>('');
  const [ownerPhone, setOwnerPhone] = useState<string>('');
  const [ownerName, setOwnerName] = useState<string>('');

  // UI States
  const [showPassword, setShowPassword] = useState<boolean>(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState<boolean>(false);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [createdStore, setCreatedStore] = useState<StoreInfo | null>(null);

  // Copy feedback states
  const [copiedApiUrl, setCopiedApiUrl] = useState<boolean>(false);
  const [copiedStoreCode, setCopiedStoreCode] = useState<boolean>(false);
  const [copiedPassword, setCopiedPassword] = useState<boolean>(false);
  const [copiedAllInfo, setCopiedAllInfo] = useState<boolean>(false);

  const serverApiUrl = typeof window !== 'undefined' ? (getServerUrl() || window.location.origin) : 'https://spk.p-services.net';

  const copyToClipboard = (text: string, type: 'api' | 'code' | 'pass' | 'all') => {
    if (!navigator.clipboard) return;
    navigator.clipboard.writeText(text);
    if (type === 'api') {
      setCopiedApiUrl(true);
      setTimeout(() => setCopiedApiUrl(false), 2500);
    } else if (type === 'code') {
      setCopiedStoreCode(true);
      setTimeout(() => setCopiedStoreCode(false), 2500);
    } else if (type === 'pass') {
      setCopiedPassword(true);
      setTimeout(() => setCopiedPassword(false), 2500);
    } else if (type === 'all') {
      setCopiedAllInfo(true);
      setTimeout(() => setCopiedAllInfo(false), 3000);
    }
  };

  const handleRandomizeShopCode = () => {
    setStoreCode(generateRandomShopCode());
  };

  const handleRandomizeRposCode = () => {
    setStoreCode(generateSecureStoreCode());
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    const cleanName = storeName.trim();
    const cleanCode = storeCode.trim().toUpperCase();
    const cleanPass = adminPassword.trim();
    const cleanConfirm = confirmPassword.trim();
    const cleanAddress = address.trim();
    const cleanPhone = ownerPhone.trim() || '080-000-0000';
    const cleanOwner = ownerName.trim() || 'เจ้าของร้าน';

    if (!cleanName) {
      setErrorMessage('กรุณาระบุชื่อร้านอาหารของคุณ');
      return;
    }

    if (!cleanCode) {
      setErrorMessage('กรุณาระบุหรือสุ่มรหัสร้านค้า (Store ID)');
      return;
    }

    if (!/^[A-Z0-9_-]{3,30}$/.test(cleanCode)) {
      setErrorMessage('รหัสร้านค้าต้องเป็นตัวอักษรภาษาอังกฤษ ตัวเลข ขีดกลาง หรืออันเดอร์สกอร์ 3-30 ตัวอักษร');
      return;
    }

    if (!cleanPass || cleanPass.length < 4) {
      setErrorMessage('กรุณากำหนดพาสเวิร์ดอย่างน้อย 4 ตัวอักษร');
      return;
    }

    if (cleanPass !== cleanConfirm) {
      setErrorMessage('พาสเวิร์ดและคอนเฟิมพาสเวิร์ดไม่ตรงกัน กรุณาตรวจสอบอีกครั้ง');
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await registerStore({
        storeCode: cleanCode,
        storeName: cleanName,
        adminUsername: 'admin',
        adminPassword: cleanPass,
        confirmPassword: cleanConfirm,
        address: cleanAddress,
        ownerPhone: cleanPhone,
        ownerName: cleanOwner
      });

      setCreatedStore(res);
      setStoredTenantCode(cleanCode);
      if (onStoreCreated) {
        onStoreCreated(res);
      }
    } catch (err: any) {
      setErrorMessage(err.message || 'เกิดข้อผิดพลาดในการลงทะเบียนร้านค้า กรุณาลองใหม่อีกครั้ง');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCopyAllSummary = () => {
    if (!createdStore) return;
    const summary = [
      '========================================',
      `ข้อมูลการลงทะเบียนร้านค้า: ${createdStore.storeName}`,
      '========================================',
      `ไอดีร้านค้า (Store ID): ${createdStore.storeCode}`,
      `พาสเวิร์ด (Password): ${adminPassword}`,
      `ชื่อผู้ใช้เริ่มต้น (Username): admin (หรือใช้ไอดีร้านค้า)`,
      `API เซิร์ฟเวอร์สำหรับเชื่อมต่อโปรแกรม: ${serverApiUrl}`,
      `ที่อยู่ร้านค้า: ${address || '-'}`,
      `ลิงก์สั่งอาหาร QR โต๊ะ 1: ${window.location.origin}/?store=${createdStore.storeCode}&table=T01`,
      '----------------------------------------',
      'คำแนะนำการใช้งาน: เวลาใช้งาน เพียงนำไอดีร้านและพาสเวิร์ดที่ลงทะเบียนไว้ ไปกรอกล็อกอินในโปรแกรม Restaurant POS บน Windows หน้าร้านเพื่อเริ่มขายอาหารได้ทันที',
      '========================================'
    ].join('\n');
    copyToClipboard(summary, 'all');
  };

  const handleResetForAnotherStore = () => {
    setCreatedStore(null);
    setStoreName('');
    setStoreCode(generateRandomShopCode());
    setAdminPassword('123456');
    setConfirmPassword('123456');
    setAddress('');
    setOwnerPhone('');
    setOwnerName('');
    setErrorMessage('');
  };

  return (
    <div style={{ position: 'relative', width: '100%', minHeight: '100vh', backgroundColor: '#F0F4F8' }}>
      {/* Background Atmosphere Z-Layer */}
      <div
        aria-hidden="true"
        style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 0,
          backgroundImage: 'radial-gradient(circle at 50% 30%, rgba(255, 255, 255, 0.82) 0%, rgba(240, 244, 248, 0.92) 100%), url(/restaurant_bg.jpg)',
          backgroundSize: 'cover',
          backgroundPosition: 'center center',
          backgroundRepeat: 'no-repeat',
          pointerEvents: 'none'
        }}
      />

      {/* Main Content Area */}
      <div style={{ position: 'relative', zIndex: 1, display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
        {/* Top Navigation Bar */}
        <header style={{
          backgroundColor: '#0D47A1',
          color: '#FFF',
          padding: '12px 20px',
          boxShadow: '0 2px 8px rgba(0,0,0,0.2)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{ fontWeight: 'bold', fontSize: '18px', letterSpacing: '0.5px' }}>
              RESTAURANT POS
            </span>
            <span style={{
              fontSize: '11px',
              backgroundColor: 'rgba(255,255,255,0.2)',
              padding: '3px 8px',
              borderRadius: '4px',
              fontWeight: 600
            }}>
              [ ลงทะเบียนร้านค้าใหม่ ]
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
            {onBackToHome && (
              <button
                type="button"
                onClick={onBackToHome}
                style={{
                  backgroundColor: 'transparent',
                  color: '#FFF',
                  border: '1px solid rgba(255,255,255,0.5)',
                  padding: '6px 14px',
                  borderRadius: '4px',
                  fontSize: '12.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                [ กลับหน้าหลัก ]
              </button>
            )}

            <button
              type="button"
              onClick={() => {
                if (onOpenLogin) {
                  onOpenLogin(createdStore ? createdStore.storeCode : '');
                } else {
                  window.location.href = '/';
                }
              }}
              style={{
                backgroundColor: '#FFD54F',
                color: '#0D47A1',
                border: 'none',
                padding: '6px 14px',
                borderRadius: '4px',
                fontSize: '12.5px',
                fontWeight: 'bold',
                cursor: 'pointer',
                boxShadow: '0 2px 4px rgba(0,0,0,0.15)'
              }}
            >
              [ เข้าสู่ระบบร้านค้า ]
            </button>
          </div>
        </header>

        {/* Page Container */}
        <main style={{
          flex: 1,
          width: '100%',
          maxWidth: '780px',
          margin: '28px auto 40px auto',
          padding: '0 16px',
          boxSizing: 'border-box'
        }}>

          {/* Quick Guidance Hero Banner */}
          <div style={{
            backgroundColor: '#0D47A1',
            backgroundImage: 'linear-gradient(135deg, #0D47A1 0%, #1565C0 100%)',
            color: '#FFF',
            padding: '20px 24px',
            borderRadius: '12px',
            boxShadow: '0 4px 16px rgba(13, 71, 161, 0.25)',
            marginBottom: '24px'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#FFEB3B',
              color: '#0D47A1',
              padding: '3px 10px',
              borderRadius: '12px',
              fontSize: '11px',
              fontWeight: 'bold',
              marginBottom: '8px'
            }}>
              [ ระบบเชื่อมต่อแบบไร้รอยต่อ ]
            </div>
            <h1 style={{ fontSize: '22px', fontWeight: 800, margin: '0 0 8px 0', lineHeight: '1.3' }}>
              ลงทะเบียนร้านค้าใหม่ (Store Registration)
            </h1>
            <p style={{ fontSize: '13.5px', color: '#E3F2FD', margin: 0, lineHeight: '1.6' }}>
              กรอกข้อมูลร้านของคุณเพื่อสร้างฐานข้อมูลประจำร้าน พร้อมรับ API สำหรับเชื่อมต่อโปรแกรมขายหน้าร้าน (POS) บน Windows 
              และระบบ QR Code สั่งอาหารประจำโต๊ะ
            </p>

            {/* Core Usage Guideline Callout */}
            <div style={{
              marginTop: '14px',
              padding: '10px 14px',
              backgroundColor: 'rgba(255, 255, 255, 0.12)',
              border: '1px solid rgba(255, 255, 255, 0.3)',
              borderRadius: '6px',
              fontSize: '12.5px',
              color: '#FFF'
            }}>
              <strong>คำแนะนำการใช้งาน:</strong> เวลาใช้งานจริงในโปรแกรมขายหน้าร้าน หรือบนเว็บจัดการร้าน 
              <strong> แค่นำไอดีร้านค้า (รหัสร้าน) กับ พาสเวิร์ดที่ลงทะเบียนไว้นี้ ไปล็อกอินเข้าใช้งานได้ทันที</strong>
            </div>
          </div>

          {/* Success Screen OR Registration Form */}
          {createdStore ? (
            <div style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              padding: '28px',
              boxShadow: '0 6px 24px rgba(0,0,0,0.1)',
              border: '2px solid #66BB6A'
            }}>
              {/* Success Header */}
              <div style={{
                textAlign: 'center',
                paddingBottom: '20px',
                borderBottom: '1px solid #E0E0E0',
                marginBottom: '24px'
              }}>
                <div style={{
                  display: 'inline-block',
                  backgroundColor: '#E8F5E9',
                  color: '#2E7D32',
                  padding: '6px 16px',
                  borderRadius: '20px',
                  fontSize: '13px',
                  fontWeight: 'bold',
                  marginBottom: '10px',
                  border: '1px solid #A5D6A7'
                }}>
                  [ ลงทะเบียนร้านค้าสำเร็จเรียบร้อยแล้ว ]
                </div>
                <h2 style={{ fontSize: '24px', fontWeight: 800, color: '#1B5E20', margin: '0 0 6px 0' }}>
                  ยินดีต้อนรับร้าน "{createdStore.storeName}"
                </h2>
                <p style={{ fontSize: '13.5px', color: '#555', margin: 0 }}>
                  ฐานข้อมูลประจำร้านของคุณถูกสร้างเรียบร้อยแล้ว และพร้อมสำหรับเริ่มขายอาหารได้ทันที
                </p>
              </div>

              {/* Core Highlight: How to Login Banner */}
              <div style={{
                backgroundColor: '#FFF9C4',
                border: '2px solid #FBC02D',
                borderRadius: '8px',
                padding: '16px 20px',
                marginBottom: '24px'
              }}>
                <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#E65100', marginBottom: '6px' }}>
                  [ คำแนะนำการเข้าใช้งาน ]
                </div>
                <div style={{ fontSize: '13.5px', color: '#37474F', lineHeight: '1.6' }}>
                  เวลาใช้งานจริง แค่นำ <strong>ไอดีร้าน: <span style={{ color: '#0D47A1', fontFamily: 'monospace', fontSize: '15px' }}>{createdStore.storeCode}</span></strong> และ <strong>พาสเวิร์ด: <span style={{ color: '#2E7D32', fontFamily: 'monospace', fontSize: '15px' }}>{adminPassword}</span></strong> ที่สมัครลงทะเบียนไว้นี้ ไปกรอกล็อกอินในโปรแกรม Restaurant POS บนคอมพิวเตอร์ของคุณ หรือล็อกอินบนเว็บนี้ได้ทันที!
                </div>
              </div>

              {/* Credentials Summary Table / Grid */}
              <div style={{
                backgroundColor: '#F8F9FA',
                border: '1px solid #CFD8DC',
                borderRadius: '8px',
                padding: '20px',
                marginBottom: '24px'
              }}>
                <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#0D47A1', marginBottom: '14px', borderBottom: '1px solid #ECEFF1', paddingBottom: '8px' }}>
                  ข้อมูลประจำร้านค้าของคุณ (บันทึกหรือคัดลอกเก็บไว้):
                </div>

                {/* 1. Store Name */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 0', borderBottom: '1px solid #ECEFF1' }}>
                  <span style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600 }}>ชื่อร้านอาหาร:</span>
                  <span style={{ fontSize: '14px', fontWeight: 'bold', color: '#212121' }}>{createdStore.storeName}</span>
                </div>

                {/* 2. Store Code (ID) */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 0', borderBottom: '1px solid #ECEFF1', flexWrap: 'wrap', gap: '8px' }}>
                  <div>
                    <span style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600 }}>รหัสร้านค้า / Store ID (สำหรับล็อกอิน):</span>
                    <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#0D47A1', fontFamily: 'Consolas, monospace', letterSpacing: '1px' }}>
                      {createdStore.storeCode}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(createdStore.storeCode, 'code')}
                    style={{
                      backgroundColor: copiedStoreCode ? '#2E7D32' : '#1976D2',
                      color: '#FFF',
                      border: 'none',
                      padding: '6px 14px',
                      borderRadius: '4px',
                      fontSize: '12px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    {copiedStoreCode ? '[ คัดลอกไอดีแล้ว ]' : '[ คัดลอก Store ID ]'}
                  </button>
                </div>

                {/* 3. Password */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 0', borderBottom: '1px solid #ECEFF1', flexWrap: 'wrap', gap: '8px' }}>
                  <div>
                    <span style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600 }}>พาสเวิร์ด (Password สำหรับล็อกอิน):</span>
                    <div style={{ fontSize: '18px', fontWeight: 'bold', color: '#2E7D32', fontFamily: 'Consolas, monospace' }}>
                      {adminPassword}
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(adminPassword, 'pass')}
                    style={{
                      backgroundColor: copiedPassword ? '#2E7D32' : '#1976D2',
                      color: '#FFF',
                      border: 'none',
                      padding: '6px 14px',
                      borderRadius: '4px',
                      fontSize: '12px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    {copiedPassword ? '[ คัดลอกพาสเวิร์ดแล้ว ]' : '[ คัดลอก Password ]'}
                  </button>
                </div>

                {/* 4. API Endpoint URL */}
                <div style={{ padding: '12px 0', borderBottom: '1px solid #ECEFF1' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px', flexWrap: 'wrap', gap: '8px' }}>
                    <span style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600 }}>
                      API สำหรับเชื่อมต่อโปรแกรม POS หน้าร้าน:
                    </span>
                    <button
                      type="button"
                      onClick={() => copyToClipboard(serverApiUrl, 'api')}
                      style={{
                        backgroundColor: copiedApiUrl ? '#2E7D32' : '#0D47A1',
                        color: '#FFF',
                        border: 'none',
                        padding: '6px 14px',
                        borderRadius: '4px',
                        fontSize: '12px',
                        fontWeight: 'bold',
                        cursor: 'pointer'
                      }}
                    >
                      {copiedApiUrl ? '[ คัดลอก API แล้ว ]' : '[ คัดลอก API URL ]'}
                    </button>
                  </div>
                  <div style={{
                    backgroundColor: '#ECEFF1',
                    padding: '8px 12px',
                    borderRadius: '4px',
                    fontFamily: 'Consolas, monospace',
                    fontSize: '13px',
                    color: '#263238',
                    wordBreak: 'break-all',
                    border: '1px solid #CFD8DC'
                  }}>
                    {serverApiUrl}
                  </div>
                  <div style={{ fontSize: '11.5px', color: '#78909C', marginTop: '4px' }}>
                    * นำ URL นี้ไปใส่ในช่อง "Server IP / API Endpoint" ของโปรแกรม POS บน Windows
                  </div>
                </div>

                {/* 5. Address */}
                {address && (
                  <div style={{ padding: '10px 0', borderBottom: '1px solid #ECEFF1' }}>
                    <span style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
                      ที่อยู่ร้านค้า (แสดงบนใบเสร็จ):
                    </span>
                    <span style={{ fontSize: '13px', color: '#37474F' }}>{address}</span>
                  </div>
                )}

                {/* 6. Customer QR Menu URL */}
                <div style={{ padding: '10px 0' }}>
                  <div style={{ fontSize: '13px', color: '#607D8B', fontWeight: 600, marginBottom: '4px' }}>
                    ลิงก์สำหรับให้ลูกค้าสแกนสั่งอาหารประจำโต๊ะ 1:
                  </div>
                  <div style={{
                    backgroundColor: '#E8F5E9',
                    padding: '8px 12px',
                    borderRadius: '4px',
                    fontFamily: 'Consolas, monospace',
                    fontSize: '12.5px',
                    color: '#1B5E20',
                    wordBreak: 'break-all',
                    border: '1px solid #C8E6C9'
                  }}>
                    {`${window.location.origin}/?store=${createdStore.storeCode}&table=T01`}
                  </div>
                </div>
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                <button
                  type="button"
                  onClick={handleCopyAllSummary}
                  style={{
                    width: '100%',
                    padding: '12px',
                    backgroundColor: copiedAllInfo ? '#2E7D32' : '#37474F',
                    color: '#FFF',
                    border: 'none',
                    borderRadius: '6px',
                    fontSize: '14px',
                    fontWeight: 'bold',
                    cursor: 'pointer',
                    boxShadow: '0 2px 6px rgba(0,0,0,0.15)'
                  }}
                >
                  {copiedAllInfo ? '[ คัดลอกข้อมูลสรุปทั้งหมดเรียบร้อยแล้ว ]' : '[ คัดลอกข้อมูลสรุปทั้งหมดเพื่อเก็บบันทึก ]'}
                </button>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={() => {
                      if (onOpenLogin) {
                        onOpenLogin(createdStore.storeCode);
                      } else {
                        window.location.href = `/?store=${createdStore.storeCode}`;
                      }
                    }}
                    style={{
                      padding: '12px',
                      backgroundColor: '#1565C0',
                      color: '#FFF',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13.5px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    [ เข้าสู่ระบบร้านค้านี้ทันที ]
                  </button>

                  <a
                    href={`/?store=${createdStore.storeCode}&table=T01`}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      padding: '12px',
                      backgroundColor: '#2E7D32',
                      color: '#FFF',
                      textDecoration: 'none',
                      borderRadius: '6px',
                      fontSize: '13.5px',
                      fontWeight: 'bold',
                      cursor: 'pointer'
                    }}
                  >
                    [ เปิดดูหน้าสั่งอาหาร QR โต๊ะ 1 ]
                  </a>
                </div>

                <button
                  type="button"
                  onClick={handleResetForAnotherStore}
                  style={{
                    padding: '10px',
                    backgroundColor: 'transparent',
                    color: '#555',
                    border: '1px solid #CCC',
                    borderRadius: '6px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    marginTop: '6px'
                  }}
                >
                  + ลงทะเบียนร้านค้าอื่นเพิ่มเติม
                </button>
              </div>
            </div>
          ) : (
            /* Registration Form */
            <div style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              padding: '28px',
              boxShadow: '0 6px 24px rgba(0,0,0,0.08)',
              border: '1px solid #CFD8DC'
            }}>
              {errorMessage && (
                <div style={{
                  backgroundColor: '#FFEBEE',
                  border: '1.5px solid #EF5350',
                  color: '#C62828',
                  padding: '12px 16px',
                  borderRadius: '6px',
                  fontSize: '13px',
                  fontWeight: 'bold',
                  marginBottom: '20px'
                }}>
                  {errorMessage}
                </div>
              )}

              <form onSubmit={handleSubmit}>
                {/* 1. ชื่อร้าน (Store Name) */}
                <div style={{ marginBottom: '18px' }}>
                  <label style={{ fontSize: '13.5px', fontWeight: 'bold', color: '#212121', display: 'block', marginBottom: '6px' }}>
                    ชื่อร้านอาหารของคุณ *
                  </label>
                  <input
                    type="text"
                    required
                    value={storeName}
                    onChange={(e) => setStoreName(e.target.value)}
                    placeholder="เช่น ครัวต้นทอง, ส้มตำคุณแม่, Cafe & Bistro"
                    style={{
                      width: '100%',
                      padding: '11px 14px',
                      borderRadius: '6px',
                      border: '1.5px solid #B0BEC5',
                      fontSize: '14px',
                      boxSizing: 'border-box',
                      outline: 'none',
                      transition: 'border-color 0.2s'
                    }}
                    onFocus={(e) => e.target.style.borderColor = '#1976D2'}
                    onBlur={(e) => e.target.style.borderColor = '#B0BEC5'}
                  />
                  <span style={{ fontSize: '11.5px', color: '#78909C', marginTop: '4px', display: 'block' }}>
                    ระบุชื่อร้านอาหารที่จะแสดงบนหัวบิลและหน้าเมนูสั่งอาหารของลูกค้า
                  </span>
                </div>

                {/* 2. รหัสร้านค้า (Store Code / ID) */}
                <div style={{ marginBottom: '18px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px', flexWrap: 'wrap', gap: '6px' }}>
                    <label style={{ fontSize: '13.5px', fontWeight: 'bold', color: '#212121' }}>
                      รหัสร้านค้า (Store ID / รหัสเชื่อมต่อโปรแกรม) *
                    </label>
                    <div style={{ display: 'flex', gap: '6px' }}>
                      <button
                        type="button"
                        onClick={handleRandomizeShopCode}
                        style={{
                          backgroundColor: '#E3F2FD',
                          color: '#1565C0',
                          border: '1px solid #90CAF9',
                          padding: '3px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 'bold',
                          cursor: 'pointer'
                        }}
                        title="สุ่มรหัสสั้นแบบ SHOP"
                      >
                        [ สุ่มรหัส SHOP ]
                      </button>
                      <button
                        type="button"
                        onClick={handleRandomizeRposCode}
                        style={{
                          backgroundColor: '#ECEFF1',
                          color: '#37474F',
                          border: '1px solid #CFD8DC',
                          padding: '3px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 'bold',
                          cursor: 'pointer'
                        }}
                        title="สุ่มรหัสมาตรฐาน RPOS"
                      >
                        [ สุ่มรหัส RPOS ]
                      </button>
                    </div>
                  </div>
                  <input
                    type="text"
                    required
                    value={storeCode}
                    onChange={(e) => setStoreCode(e.target.value.toUpperCase())}
                    placeholder="เช่น SHOP8821 หรือ RPOS-XXXX-XXXX"
                    style={{
                      width: '100%',
                      padding: '11px 14px',
                      borderRadius: '6px',
                      border: '1.5px solid #1976D2',
                      backgroundColor: '#F4F9FF',
                      fontSize: '15px',
                      fontFamily: 'Consolas, monospace',
                      fontWeight: 'bold',
                      color: '#0D47A1',
                      letterSpacing: '1px',
                      boxSizing: 'border-box'
                    }}
                  />
                  <span style={{ fontSize: '11.5px', color: '#546E7A', marginTop: '4px', display: 'block' }}>
                    รหัสนี้คือ <strong>"ไอดีร้านค้า"</strong> สำหรับนำไปกรอกล็อกอินในโปรแกรม POS หน้าร้าน และเป็น ID ร้านของคุณ
                  </span>
                </div>

                {/* 3. พาสเวิร์ด (Password) & 4. คอนเฟิมพาสเวิร์ด (Confirm Password) */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '14px', marginBottom: '14px' }}>
                  {/* Password */}
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                      <label style={{ fontSize: '13.5px', fontWeight: 'bold', color: '#212121' }}>
                        พาสเวิร์ด (Password) *
                      </label>
                      <button
                        type="button"
                        onClick={() => setShowPassword(p => !p)}
                        style={{
                          background: 'none',
                          border: 'none',
                          color: '#1565C0',
                          fontSize: '11px',
                          fontWeight: 'bold',
                          cursor: 'pointer',
                          padding: 0
                        }}
                      >
                        {showPassword ? '[ ซ่อน ]' : '[ แสดงรหัส ]'}
                      </button>
                    </div>
                    <input
                      type={showPassword ? 'text' : 'password'}
                      required
                      value={adminPassword}
                      onChange={(e) => setAdminPassword(e.target.value)}
                      placeholder="อย่างน้อย 4 ตัวอักษร"
                      style={{
                        width: '100%',
                        padding: '11px 14px',
                        borderRadius: '6px',
                        border: '1.5px solid #B0BEC5',
                        fontSize: '14px',
                        boxSizing: 'border-box'
                      }}
                    />
                  </div>

                  {/* Confirm Password */}
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                      <label style={{ fontSize: '13.5px', fontWeight: 'bold', color: '#212121' }}>
                        คอนเฟิมพาสเวิร์ด (Confirm Password) *
                      </label>
                      <button
                        type="button"
                        onClick={() => setShowConfirmPassword(p => !p)}
                        style={{
                          background: 'none',
                          border: 'none',
                          color: '#1565C0',
                          fontSize: '11px',
                          fontWeight: 'bold',
                          cursor: 'pointer',
                          padding: 0
                        }}
                      >
                        {showConfirmPassword ? '[ ซ่อน ]' : '[ แสดงรหัส ]'}
                      </button>
                    </div>
                    <input
                      type={showConfirmPassword ? 'text' : 'password'}
                      required
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="กรอกพาสเวิร์ดซ้ำให้ตรงกัน"
                      style={{
                        width: '100%',
                        padding: '11px 14px',
                        borderRadius: '6px',
                        border: `1.5px solid ${confirmPassword ? (confirmPassword === adminPassword ? '#4CAF50' : '#E53935') : '#B0BEC5'}`,
                        fontSize: '14px',
                        boxSizing: 'border-box'
                      }}
                    />
                  </div>
                </div>

                {/* Password Match Status Pill */}
                <div style={{ marginBottom: '18px' }}>
                  {confirmPassword ? (
                    confirmPassword === adminPassword ? (
                      <span style={{ fontSize: '11.5px', color: '#2E7D32', fontWeight: 'bold' }}>
                        [ รหัสผ่านตรงกันสมบูรณ์ ]
                      </span>
                    ) : (
                      <span style={{ fontSize: '11.5px', color: '#C62828', fontWeight: 'bold' }}>
                        [ พาสเวิร์ดและคอนเฟิมพาสเวิร์ดยังไม่ตรงกัน กรุณาตรวจสอบ ]
                      </span>
                    )
                  ) : (
                    <span style={{ fontSize: '11.5px', color: '#78909C' }}>
                      * ใช้สำหรับนำไปล็อกอินคู่กับไอดีร้านค้าในโปรแกรม POS
                    </span>
                  )}
                </div>

                {/* 5. ที่อยู่ร้านค้า (Store Address) */}
                <div style={{ marginBottom: '20px' }}>
                  <label style={{ fontSize: '13.5px', fontWeight: 'bold', color: '#212121', display: 'block', marginBottom: '6px' }}>
                    ที่อยู่ร้านค้า (Store Address)
                  </label>
                  <textarea
                    rows={3}
                    value={address}
                    onChange={(e) => setAddress(e.target.value)}
                    placeholder="เลขที่, ถนน, แขวง/ตำบล, เขต/อำเภอ, จังหวัด, รหัสไปรษณีย์ (สำหรับพิมพ์หัวบิลสลิปใบเสร็จและใบกำกับภาษี)"
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '6px',
                      border: '1.5px solid #B0BEC5',
                      fontSize: '13px',
                      boxSizing: 'border-box',
                      lineHeight: '1.5',
                      resize: 'vertical'
                    }}
                  />
                  <span style={{ fontSize: '11.5px', color: '#78909C', marginTop: '4px', display: 'block' }}>
                    ที่อยู่นี้จะปรากฏบนหัวสลิปใบเสร็จรับเงินที่พิมพ์ออกจากเครื่องคิดเงินหน้าร้าน
                  </span>
                </div>

                {/* 6. api สำหรับเชื่อมต่อระหว่างตัวโปรแกรมกับร้านค้า แบบคัดลอกได้ด้วย */}
                <div style={{
                  backgroundColor: '#E8EAF6',
                  border: '1.5px solid #9FA8DA',
                  borderRadius: '8px',
                  padding: '16px',
                  marginBottom: '20px'
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px', flexWrap: 'wrap', gap: '8px' }}>
                    <div>
                      <span style={{ fontSize: '13px', fontWeight: 'bold', color: '#1A237E' }}>
                        API สำหรับเชื่อมต่อระหว่างตัวโปรแกรมกับร้านค้า (Server Connection URL):
                      </span>
                      <div style={{ fontSize: '11.5px', color: '#3949AB', marginTop: '2px' }}>
                        นำ URL นี้ไปใส่ในช่อง Server IP ในการตั้งค่าโปรแกรม POS บน Windows
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => copyToClipboard(serverApiUrl, 'api')}
                      style={{
                        backgroundColor: copiedApiUrl ? '#2E7D32' : '#283593',
                        color: '#FFF',
                        border: 'none',
                        padding: '6px 14px',
                        borderRadius: '4px',
                        fontSize: '12px',
                        fontWeight: 'bold',
                        cursor: 'pointer',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.15)'
                      }}
                    >
                      {copiedApiUrl ? '[ คัดลอกสำเร็จแล้ว ]' : '[ คัดลอก API URL ]'}
                    </button>
                  </div>

                  <div style={{
                    backgroundColor: '#FFFFFF',
                    border: '1px solid #C5CAE9',
                    borderRadius: '6px',
                    padding: '10px 14px',
                    fontFamily: 'Consolas, monospace',
                    fontSize: '13.5px',
                    color: '#1A237E',
                    fontWeight: 'bold',
                    wordBreak: 'break-all'
                  }}>
                    {serverApiUrl}
                  </div>
                </div>

                {/* Owner Optional Details */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '14px', marginBottom: '22px' }}>
                  <div>
                    <label style={{ fontSize: '12.5px', fontWeight: 600, color: '#455A64', display: 'block', marginBottom: '4px' }}>
                      เบอร์โทรศัพท์ติดต่อร้านค้า
                    </label>
                    <input
                      type="tel"
                      value={ownerPhone}
                      onChange={(e) => setOwnerPhone(e.target.value)}
                      placeholder="เช่น 081-234-5678"
                      style={{
                        width: '100%',
                        padding: '9px 12px',
                        borderRadius: '6px',
                        border: '1px solid #CFD8DC',
                        fontSize: '13px',
                        boxSizing: 'border-box'
                      }}
                    />
                  </div>

                  <div>
                    <label style={{ fontSize: '12.5px', fontWeight: 600, color: '#455A64', display: 'block', marginBottom: '4px' }}>
                      ชื่อเจ้าของร้าน / ผู้ติดต่อ
                    </label>
                    <input
                      type="text"
                      value={ownerName}
                      onChange={(e) => setOwnerName(e.target.value)}
                      placeholder="เช่น คุณสมชาย"
                      style={{
                        width: '100%',
                        padding: '9px 12px',
                        borderRadius: '6px',
                        border: '1px solid #CFD8DC',
                        fontSize: '13px',
                        boxSizing: 'border-box'
                      }}
                    />
                  </div>
                </div>

                {/* Login Guidance Callout Before Submit */}
                <div style={{
                  backgroundColor: '#E8F5E9',
                  border: '1px solid #A5D6A7',
                  borderRadius: '6px',
                  padding: '12px 14px',
                  marginBottom: '20px',
                  fontSize: '12.5px',
                  color: '#1B5E20',
                  lineHeight: '1.5'
                }}>
                  <strong>พร้อมใช้งานทันที:</strong> เมื่อกดลงทะเบียนเสร็จสิ้น 
                  <strong> "เวลาใช้งานก็แค่เอาไอดี กับ พาสที่สมัครลงทะเบียนไว้ ไปล็อกอิน"</strong> ในโปรแกรม POS หน้าร้าน หรือบนเว็บจัดการร้านได้ทันที
                </div>

                {/* Submit Button */}
                <button
                  type="submit"
                  disabled={isSubmitting}
                  style={{
                    width: '100%',
                    padding: '14px',
                    backgroundColor: isSubmitting ? '#9E9E9E' : '#0D47A1',
                    color: '#FFF',
                    border: 'none',
                    borderRadius: '8px',
                    fontSize: '16px',
                    fontWeight: 'bold',
                    cursor: isSubmitting ? 'not-allowed' : 'pointer',
                    boxShadow: '0 4px 12px rgba(13, 71, 161, 0.3)',
                    transition: 'background-color 0.2s'
                  }}
                >
                  {isSubmitting ? '[ กำลังลงทะเบียนและสร้างฐานข้อมูลประจำร้าน... ]' : '[ ลงทะเบียนและเปิดร้านค้าทันที ]'}
                </button>
              </form>
            </div>
          )}
        </main>

        {/* Footer */}
        <footer style={{
          textAlign: 'center',
          padding: '16px',
          color: '#78909C',
          fontSize: '12px',
          borderTop: '1px solid rgba(0,0,0,0.06)'
        }}>
          RESTAURANT POS &mdash; ระบบจัดการร้านอาหารและจุดขาย POS หน้าร้าน &copy; 2026
        </footer>
      </div>
    </div>
  );
}
