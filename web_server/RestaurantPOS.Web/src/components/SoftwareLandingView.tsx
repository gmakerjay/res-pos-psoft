import { useState } from 'react';
import type { StoreInfo } from '../services/api';
import { getServerUrl } from '../services/logger';

interface SoftwareLandingViewProps {
  onOpenLogin: (storeCode?: string) => void;
  onStoreCreated?: (store: StoreInfo) => void;
  onEnterDemo?: (tableOrType?: string) => void;
  onOpenRegister?: () => void;
}

export function SoftwareLandingView({ onOpenLogin, onEnterDemo, onOpenRegister }: SoftwareLandingViewProps) {
  const [copiedApiUrl, setCopiedApiUrl] = useState(false);

  const serverApiUrl = typeof window !== 'undefined' ? (getServerUrl() || window.location.origin) : 'https://spk.p-services.net';

  const handleOpenRegister = () => {
    if (onOpenRegister) {
      onOpenRegister();
    } else {
      window.open('/?page=register', '_blank');
    }
  };

  return (
    <div style={{ position: 'relative', width: '100%', minHeight: '88vh', boxSizing: 'border-box' }}>
      {/* Realistic Restaurant Background Z-Layer (Subtle, Atmospheric, Non-Intrusive) */}
      <div
        aria-hidden="true"
        style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 0,
          backgroundImage: 'radial-gradient(circle at 50% 30%, rgba(255, 255, 255, 0.76) 0%, rgba(244, 246, 249, 0.86) 100%), url(/restaurant_bg.jpg)',
          backgroundSize: 'cover',
          backgroundPosition: 'center center',
          backgroundRepeat: 'no-repeat',
          pointerEvents: 'none',
          filter: 'contrast(1.04) saturate(1.08)'
        }}
      />

      {/* Main Content Area (Z-Index 1 above background) */}
      <div style={{ position: 'relative', zIndex: 1, width: '100%', padding: '0 12px 40px 12px', boxSizing: 'border-box' }}>
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
            marginBottom: '24px'
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

          {/* Hero Call-to-Action Actions */}
          <div style={{
            display: 'flex',
            justifyContent: 'center',
            gap: '12px',
            flexWrap: 'wrap',
            marginBottom: '10px'
          }}>
            <button
              type="button"
              onClick={handleOpenRegister}
              style={{
                backgroundColor: '#0D47A1',
                color: '#FFF',
                border: 'none',
                padding: '12px 24px',
                borderRadius: '8px',
                fontSize: '15px',
                fontWeight: 'bold',
                cursor: 'pointer',
                boxShadow: '0 4px 14px rgba(13, 71, 161, 0.35)',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px'
              }}
            >
              [ + ลงทะเบียนร้านค้าใหม่ (เปิดหน้าใหม่) ]
            </button>

            <button
              type="button"
              onClick={() => onOpenLogin('DEFAULT')}
              style={{
                backgroundColor: '#FFF',
                color: '#0D47A1',
                border: '2px solid #0D47A1',
                padding: '10px 20px',
                borderRadius: '8px',
                fontSize: '14px',
                fontWeight: 'bold',
                cursor: 'pointer',
                boxShadow: '0 2px 6px rgba(0,0,0,0.06)'
              }}
            >
              [ เข้าสู่ระบบร้านค้า ]
            </button>
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

        {/* Comprehensive POS Modules & Capabilities Showcase (Replacing old inline form) */}
        <section style={{
          maxWidth: '960px',
          margin: '0 auto 40px auto',
          padding: '0 10px'
        }}>
          <div style={{ textAlign: 'center', marginBottom: '24px' }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#E8F5E9',
              color: '#2E7D32',
              padding: '4px 14px',
              borderRadius: '20px',
              fontSize: '12px',
              fontWeight: 'bold',
              marginBottom: '8px',
              border: '1px solid #C8E6C9'
            }}>
              [ ฟังก์ชันครบวงจรสำหรับร้านอาหารทุกระดับ ]
            </div>
            <h2 style={{ fontSize: '22px', fontWeight: 800, color: '#0D47A1', margin: '0 0 6px 0' }}>
              โมดูลการทำงานอัจฉริยะ เชื่อมต่อทุกจุดในร้านอาหาร
            </h2>
            <p style={{ fontSize: '13.5px', color: '#546E7A', margin: 0, lineHeight: '1.6' }}>
              ออกแบบมาเพื่อความเร็ว ความแม่นยำ และความเสถียรสูงสุด ครอบคลุมทั้งหน้าร้าน ในครัว ลูกค้า และฝ่ายบริหาร
            </p>
          </div>

          {/* 6 Key Modules Grid */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
            gap: '16px',
            marginBottom: '28px'
          }}>
            {/* Module 1: Desktop POS */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #BBDEFB',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#E3F2FD', color: '#1565C0', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ หน้าร้าน ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#0D47A1', margin: '0 0 8px 0' }}>
                  Windows Desktop POS หน้าร้าน
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  โปรแกรมขายหน้าร้าน C# WPF Classic XP Theme ทำงานรวดเร็ว รองรับจอสัมผัส ย้ายโต๊ะ รวมบิล พักโต๊ะ และสั่งพิมพ์สลิปใบเสร็จอัตโนมัติผ่าน Windows Spooler / ESC-POS
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ เสถียรสูง ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ คีย์ลัด F1-F12 ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ พิมพ์สลิปทันที ]</span>
              </div>
            </div>

            {/* Module 2: Customer QR */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #C8E6C9',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#E8F5E9', color: '#2E7D32', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ ลูกค้า ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#1B5E20', margin: '0 0 8px 0' }}>
                  สั่งอาหารผ่าน QR Code (Customer QR)
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  ลูกค้าสแกน QR Code ประจำโต๊ะด้วยสมาร์ทโฟน ไม่ต้องติดตั้งแอป สั่งอาหารได้เองอย่างสะดวก รวดเร็ว พร้อมเลือกออปชันพิเศษและระดับความเผ็ดได้ตามใจชอบ
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ทานที่ร้าน (Dine-In) ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ สั่งกลับบ้าน (Takeaway) ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ติดตามสถานะ ]</span>
              </div>
            </div>

            {/* Module 3: Kitchen KDS */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #FFE082',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#FFF8E1', color: '#F57F17', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ ในครัว ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#E65100', margin: '0 0 8px 0' }}>
                  จอครัวอัจฉริยะ (Kitchen Display KDS)
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  จอแสดงรายการออเดอร์ในครัวแบบ SignalR Real-Time เด้งเตือนเสียงทันทีเมื่อมีออเดอร์ใหม่ แยกสเตชันครัว/บาร์น้ำ ปรุงเสร็จแตะเปลี่ยนสถานะ [กำลังปรุง] ถึง [พร้อมเสิร์ฟ]
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ อัปเดตเสี้ยววินาที ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ แจ้งเตือนเสียง ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ เตือนออเดอร์ค้าง ]</span>
              </div>
            </div>

            {/* Module 4: Inventory & Recipe */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #D1C4E9',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#EDE7F6', color: '#512DA8', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ คลังสต๊อก ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#4A148C', margin: '0 0 8px 0' }}>
                  คลังวัตถุดิบ &amp; สูตรอาหาร (Recipe BOM)
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  ผูกสูตรอาหาร (Bill of Materials) คำนวณและตัดสต๊อกวัตถุดิบอัตโนมัติตามยอดขายจริง พร้อมระบบแจ้งเตือนเมื่อวัตถุดิบใกล้หมด ป้องกันของขาดช่วงเวลาเร่งด่วน
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ตัดสต๊อกอัตโนมัติ ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ แจ้งเตือนใกล้หมด ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ประวัติเบิกจ่าย ]</span>
              </div>
            </div>

            {/* Module 5: Sales Reports & Backup */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #B2DFDB',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#E0F2F1', color: '#00695C', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ การเงิน &amp; บัญชี ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#004D40', margin: '0 0 8px 0' }}>
                  รายงานยอดขาย สรุปบัญชี และสำรองข้อมูล
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  สรุปยอดขายรายวัน รายเดือน รายหมวดหมู่อาหาร คำนวณกำไร/ต้นทุน และภาษีมูลค่าเพิ่ม (VAT 7%) พร้อมระบบสำรองข้อมูลร้านค้า (Export / Backup) ก้อนเดียวจบ นำไปใช้งานต่อได้ทันที
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ รายงานยอดขาย ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ปิดกะ/ตัดรอบ ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ 1-Click Backup ]</span>
              </div>
            </div>

            {/* Module 6: Multi-Tenant Cloud */}
            <div style={{
              backgroundColor: '#FFF',
              border: '1px solid #CFD8DC',
              borderRadius: '10px',
              padding: '20px',
              boxShadow: '0 4px 12px rgba(0,0,0,0.04)',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ display: 'inline-block', backgroundColor: '#ECEFF1', color: '#37474F', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 'bold', marginBottom: '10px' }}>
                  [ คลาวด์ &amp; ความปลอดภัย ]
                </div>
                <h3 style={{ fontSize: '16px', fontWeight: 'bold', color: '#263238', margin: '0 0 8px 0' }}>
                  สถาปัตยกรรมคลาวด์แยกฐานข้อมูล 100%
                </h3>
                <p style={{ fontSize: '13px', color: '#555', lineHeight: '1.6', margin: '0 0 14px 0' }}>
                  ความปลอดภัยระดับสูงสุด แยกฐานข้อมูล 1 ร้าน = 1 ฐานข้อมูล 100% ไม่ปะปนกับร้านอื่น ล็อกอินด้วย Store Code และ Password ผ่าน Cloud API พร้อมระบบทดลองใช้ฟรี 14 วันจริง
                </p>
              </div>
              <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ฐานข้อมูลแยกเฉพาะ ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ เข้ารหัสรหัสผ่าน ]</span>
                <span style={{ backgroundColor: '#F5F5F5', color: '#455A64', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>[ ทดลองฟรี 14 วัน ]</span>
              </div>
            </div>
          </div>

          {/* Quick Launch & Dedicated Registration Action Banner */}
          <div style={{
            background: 'linear-gradient(135deg, #0D47A1 0%, #1565C0 100%)',
            color: '#FFF',
            borderRadius: '12px',
            padding: '28px 24px',
            boxShadow: '0 8px 24px rgba(13, 71, 161, 0.25)',
            textAlign: 'center',
            border: '1px solid #1976D2'
          }}>
            <div style={{
              display: 'inline-block',
              backgroundColor: '#FFD54F',
              color: '#0D47A1',
              padding: '4px 14px',
              borderRadius: '20px',
              fontSize: '12px',
              fontWeight: 'bold',
              marginBottom: '12px'
            }}>
              [ เริ่มต้นเปิดร้านค้าของคุณวันนี้ ]
            </div>

            <h2 style={{ fontSize: 'clamp(20px, 4vw, 26px)', fontWeight: 800, margin: '0 0 8px 0', color: '#FFF' }}>
              พร้อมยกระดับการบริหารจัดการร้านอาหารของคุณแล้วหรือยัง?
            </h2>

            <p style={{ fontSize: '14px', color: '#BBDEFB', maxWidth: '640px', margin: '0 auto 20px auto', lineHeight: '1.6' }}>
              ลงทะเบียนง่ายๆ ภายใน 1 นาที เพื่อรับรหัสร้านค้า (Store Code) พร้อมนำไปเชื่อมต่อและเปิดใช้งานโปรแกรมขายหน้าร้านบน Windows ได้ทันที
            </p>

            <div style={{ display: 'flex', justifyContent: 'center', gap: '10px', flexWrap: 'wrap', marginBottom: '22px' }}>
              <span style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#FFF', padding: '5px 12px', borderRadius: '20px', fontSize: '12px' }}>
                [ 1. ลงทะเบียนรับรหัสร้าน ]
              </span>
              <span style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#FFF', padding: '5px 12px', borderRadius: '20px', fontSize: '12px' }}>
                [ 2. นำไอดี/พาสไปล็อกอิน ]
              </span>
              <span style={{ backgroundColor: 'rgba(255,255,255,0.15)', color: '#FFF', padding: '5px 12px', borderRadius: '20px', fontSize: '12px' }}>
                [ 3. เริ่มขายหน้าร้านได้ทันที ]
              </span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'center', gap: '14px', flexWrap: 'wrap', marginBottom: '20px' }}>
              <button
                type="button"
                onClick={handleOpenRegister}
                style={{
                  backgroundColor: '#FFEB3B',
                  color: '#0D47A1',
                  border: 'none',
                  padding: '14px 28px',
                  borderRadius: '8px',
                  fontSize: '15.5px',
                  fontWeight: 'bold',
                  cursor: 'pointer',
                  boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px'
                }}
              >
                [ + เปิดหน้าต่างลงทะเบียนร้านค้าใหม่ ]
              </button>

              <button
                type="button"
                onClick={() => onOpenLogin('DEFAULT')}
                style={{
                  backgroundColor: 'transparent',
                  color: '#FFF',
                  border: '2px solid rgba(255,255,255,0.8)',
                  padding: '12px 24px',
                  borderRadius: '8px',
                  fontSize: '14px',
                  fontWeight: 'bold',
                  cursor: 'pointer'
                }}
              >
                [ เข้าสู่ระบบร้านค้า ]
              </button>
            </div>

            {/* Connection API URL Info Box */}
            <div style={{
              maxWidth: '560px',
              margin: '0 auto',
              backgroundColor: 'rgba(0, 0, 0, 0.22)',
              borderRadius: '8px',
              padding: '12px 16px',
              textAlign: 'left'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px', flexWrap: 'wrap', gap: '6px' }}>
                <span style={{ fontSize: '12px', color: '#BBDEFB', fontWeight: 'bold' }}>
                  Server API URL สำหรับเชื่อมต่อโปรแกรม POS:
                </span>
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(serverApiUrl);
                    setCopiedApiUrl(true);
                    setTimeout(() => setCopiedApiUrl(false), 2500);
                  }}
                  style={{
                    backgroundColor: copiedApiUrl ? '#2E7D32' : '#FFD54F',
                    color: copiedApiUrl ? '#FFF' : '#0D47A1',
                    border: 'none',
                    padding: '3px 10px',
                    borderRadius: '4px',
                    fontSize: '11px',
                    fontWeight: 'bold',
                    cursor: 'pointer'
                  }}
                >
                  {copiedApiUrl ? '[ คัดลอกสำเร็จ ]' : '[ คัดลอก API URL ]'}
                </button>
              </div>
              <div style={{
                fontFamily: 'Consolas, monospace',
                fontSize: '13px',
                color: '#FFF',
                fontWeight: 'bold',
                wordBreak: 'break-all'
              }}>
                {serverApiUrl}
              </div>
              <div style={{ fontSize: '11px', color: '#90CAF9', marginTop: '4px' }}>
                * คำแนะนำการใช้งาน: เวลาใช้งานจริง แค่เอาไอดี กับ พาสที่สมัครลงทะเบียนไว้ ไปล็อกอินในโปรแกรม POS บนคอมพิวเตอร์ หรือบนเว็บได้ทันที
              </div>
            </div>
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
            color: '#795548',
            lineHeight: '1.6'
          }}>
            <strong>ข้อแนะนำการใช้งาน:</strong> เพื่อความเสถียรและประสิทธิภาพสูงสุดในการพิมพ์ใบเสร็จและควบคุมแคชเชียร์ 
            แนะนำให้ใช้งานตัวโปรแกรม <strong>Windows Desktop Client (C# WPF)</strong> บนเครื่องคอมพิวเตอร์หน้าร้าน 
            โดยระบบเว็บนี้จะทำหน้าที่เป็นศูนย์กลางคลาวด์รับออเดอร์จากมือถือของลูกค้าและส่งต่อข้อมูลแบบเรียลไทม์
          </div>
        </section>

        {/* Key Features Grid */}
        <section style={{
          maxWidth: '960px',
          margin: '0 auto 40px auto',
          padding: '0 10px'
        }}>
          <div style={{ textAlign: 'center', marginBottom: '24px' }}>
            <h2 style={{ fontSize: '20px', fontWeight: 800, color: '#0D47A1', marginBottom: '6px' }}>
              จุดเด่นของซอฟต์แวร์ Restaurant POS
            </h2>
            <p style={{ fontSize: '13px', color: '#546E7A', margin: 0 }}>
              ครบทุกฟังก์ชันที่ร้านอาหารยุคใหม่ต้องการ ออกแบบตามมาตรฐานร้านอาหารชั้นนำ
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
                [ หน้าจอขายหน้าร้านบน Windows ]
              </div>
              <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
                อินเทอร์เฟซสไตล์ Classic XP ทำงานรวดเร็ว รองรับจอสัมผัส (Touch Screen) 
                สั่งพิมพ์ใบเสร็จ สลิป และใบสั่งครัวผ่านเครื่องพิมพ์ความร้อน (Thermal Printer) โดยตรง
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
              <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#1B5E20', marginBottom: '6px' }}>
                [ ลูกค้าสั่งอาหารผ่าน QR Code ]
              </div>
              <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
                ลดภาระพนักงานเสิร์ฟ ลูกค้าสแกน QR Code บนโต๊ะเพื่อเลือกดูเมนู 
                และสั่งอาหารได้เองทันทีผ่านเบราว์เซอร์บนมือถือ ไม่ต้องลงแอปพลิเคชัน
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
              <div style={{ fontSize: '15px', fontWeight: 'bold', color: '#B71C1C', marginBottom: '6px' }}>
                [ จอแสดงผลในครัว (KDS) เรียลไทม์ ]
              </div>
              <p style={{ fontSize: '13px', color: '#555', margin: 0, lineHeight: '1.6' }}>
                ออเดอร์ที่ลูกค้าสั่งจะถูกส่งตรงเข้าจอในครัวทันทีแบบวินาทีต่อวินาที 
                พร้อมแจ้งเตือนด้วยเสียง และระบุสถานะอาหาร (รอทำ, กำลังทำ, พร้อมเสิร์ฟ)
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
    </div>
  );
}
