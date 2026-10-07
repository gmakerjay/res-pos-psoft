import React, { useState, useEffect } from 'react';
import QRCode from 'qrcode';
import { getTables, createTable, deleteTable } from '../services/api';
import type { TableItem, StoreInfo } from '../services/api';

interface TableQrManagerModalProps {
  storeInfo: StoreInfo | null;
  onClose: () => void;
}

export function TableQrManagerModal({ storeInfo, onClose }: TableQrManagerModalProps) {
  const [tables, setTables] = useState<TableItem[]>([]);
  const [qrImages, setQrImages] = useState<{ [key: string]: string }>({});
  const [takeawayQr, setTakeawayQr] = useState<string>('');
  const [activeTab, setActiveTab] = useState<'tables' | 'takeaway'>('tables');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  
  // New Table Form
  const [showAddTable, setShowAddTable] = useState(false);
  const [newTableNo, setNewTableNo] = useState('');
  const [newTableName, setNewTableName] = useState('');
  const [isAdding, setIsAdding] = useState(false);

  const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'https://spk.p-services.net';
  const storeCode = storeInfo?.storeCode || '';
  const storeName = storeInfo?.storeName || 'ร้านอาหาร';

  // Load tables and generate QRs
  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await getTables();
      setTables(data);
      await generateAllQrs(data);
    } catch (err: any) {
      setError(err.message || 'ไม่สามารถโหลดข้อมูลโต๊ะได้');
    } finally {
      setIsLoading(false);
    }
  };

  const generateAllQrs = async (tableList: TableItem[]) => {
    const images: { [key: string]: string } = {};

    // 1. Generate Table QRs
    for (const t of tableList) {
      const tableUrl = `${baseUrl}/?store=${encodeURIComponent(storeCode)}&table=${encodeURIComponent(t.tableNumber)}`;
      try {
        const dataUrl = await QRCode.toDataURL(tableUrl, {
          width: 320,
          margin: 1,
          color: {
            dark: '#0D47A1',
            light: '#FFFFFF'
          }
        });
        images[t.tableNumber] = dataUrl;
      } catch (e) {
        console.error('Failed to generate QR for table', t.tableNumber, e);
      }
    }
    setQrImages(images);

    // 2. Generate Store / Takeaway QR
    const takeawayUrl = `${baseUrl}/?store=${encodeURIComponent(storeCode)}&type=takeaway`;
    try {
      const takeawayDataUrl = await QRCode.toDataURL(takeawayUrl, {
        width: 320,
        margin: 1,
        color: {
          dark: '#E65100',
          light: '#FFFFFF'
        }
      });
      setTakeawayQr(takeawayDataUrl);
    } catch (e) {
      console.error('Failed to generate takeaway QR', e);
    }
  };

  useEffect(() => {
    loadData();
  }, [storeCode]);

  const handleAddTable = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTableNo.trim()) return;

    setIsAdding(true);
    setError('');
    try {
      await createTable({
        tableNumber: newTableNo.trim().toUpperCase(),
        name: newTableName.trim() || `โต๊ะ ${newTableNo.trim().toUpperCase()}`,
        capacity: 4
      });
      setNewTableNo('');
      setNewTableName('');
      setShowAddTable(false);
      await loadData();
    } catch (err: any) {
      setError(err.message || 'ไม่สามารถเพิ่มโต๊ะได้');
    } finally {
      setIsAdding(false);
    }
  };

  const handleDeleteTable = async (tableId: number, tableNo: string) => {
    if (!confirm(`คุณต้องการลบโต๊ะ ${tableNo} ใช่หรือไม่?`)) return;
    try {
      await deleteTable(tableId);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'ไม่สามารถลบโต๊ะได้');
    }
  };

  const downloadQr = (dataUrl: string, filename: string) => {
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = `${filename}.png`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  const handlePrint = () => {
    window.print();
  };

  return (
    <div style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.65)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 9999,
      padding: '16px',
      boxSizing: 'border-box'
    }}>
      <style>{`
        @media print {
          body * {
            visibility: hidden;
          }
          #printable-qr-area, #printable-qr-area * {
            visibility: visible;
          }
          #printable-qr-area {
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
            padding: 0;
            margin: 0;
          }
          .no-print {
            display: none !important;
          }
          .table-sticker-card {
            page-break-inside: avoid;
            border: 2px dashed #999 !important;
            margin-bottom: 20px !important;
            box-shadow: none !important;
          }
        }
      `}</style>

      <div style={{
        backgroundColor: '#FFF',
        borderRadius: '10px',
        width: '100%',
        maxWidth: '920px',
        maxHeight: '90vh',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        boxShadow: '0 10px 40px rgba(0,0,0,0.3)'
      }}>
        {/* Modal Header */}
        <div className="no-print" style={{
          backgroundColor: '#0D47A1',
          color: '#FFF',
          padding: '14px 20px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between'
        }}>
          <div>
            <div style={{ fontSize: '17px', fontWeight: 'bold' }}>
              [ ระบบจัดการและพิมพ์ป้าย QR Code ติดโต๊ะอาหาร ]
            </div>
            <div style={{ fontSize: '12px', color: '#BBDEFB', marginTop: '2px' }}>
              ร้าน: {storeName} (รหัสร้าน: {storeCode || 'ยังไม่ได้ระบุ'})
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'none',
              border: 'none',
              color: '#FFF',
              fontSize: '22px',
              cursor: 'pointer',
              padding: '0 6px'
            }}
          >
            x
          </button>
        </div>

        {/* Action & Tab Bar */}
        <div className="no-print" style={{
          padding: '12px 20px',
          backgroundColor: '#F5F5F5',
          borderBottom: '1px solid #E0E0E0',
          display: 'flex',
          flexWrap: 'wrap',
          gap: '10px',
          alignItems: 'center',
          justifyContent: 'space-between'
        }}>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              onClick={() => setActiveTab('tables')}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: '1px solid #1976D2',
                backgroundColor: activeTab === 'tables' ? '#1976D2' : '#FFF',
                color: activeTab === 'tables' ? '#FFF' : '#1976D2',
                fontWeight: 'bold',
                fontSize: '13px',
                cursor: 'pointer'
              }}
            >
              [ ป้าย QR ประจำโต๊ะ ({tables.length} โต๊ะ) ]
            </button>
            <button
              onClick={() => setActiveTab('takeaway')}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: '1px solid #E65100',
                backgroundColor: activeTab === 'takeaway' ? '#E65100' : '#FFF',
                color: activeTab === 'takeaway' ? '#FFF' : '#E65100',
                fontWeight: 'bold',
                fontSize: '13px',
                cursor: 'pointer'
              }}
            >
              [ ป้าย QR ร้าน (สั่งกลับบ้าน / ทางบ้าน) ]
            </button>
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            {activeTab === 'tables' && (
              <button
                onClick={() => setShowAddTable(!showAddTable)}
                style={{
                  padding: '8px 14px',
                  borderRadius: '6px',
                  border: '1px solid #4CAF50',
                  backgroundColor: '#4CAF50',
                  color: '#FFF',
                  fontWeight: 'bold',
                  fontSize: '13px',
                  cursor: 'pointer'
                }}
              >
                + เพิ่มโต๊ะใหม่
              </button>
            )}
            <button
              onClick={handlePrint}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: '1px solid #0D47A1',
                backgroundColor: '#0D47A1',
                color: '#FFF',
                fontWeight: 'bold',
                fontSize: '13px',
                cursor: 'pointer'
              }}
            >
              [ พิมพ์ป้ายทั้งหมด (Print Sheet) ]
            </button>
          </div>
        </div>

        {/* Add Table Form Dropdown */}
        {showAddTable && activeTab === 'tables' && (
          <form
            onSubmit={handleAddTable}
            className="no-print"
            style={{
              padding: '14px 20px',
              backgroundColor: '#E8F5E9',
              borderBottom: '1px solid #C8E6C9',
              display: 'flex',
              gap: '10px',
              alignItems: 'center',
              flexWrap: 'wrap'
            }}
          >
            <span style={{ fontWeight: 'bold', fontSize: '13px', color: '#2E7D32' }}>
              เพิ่มโต๊ะใหม่:
            </span>
            <input
              type="text"
              placeholder="รหัสโต๊ะ (เช่น T13, VIP1)"
              value={newTableNo}
              onChange={(e) => setNewTableNo(e.target.value)}
              style={{
                padding: '6px 10px',
                borderRadius: '4px',
                border: '1px solid #A5D6A7',
                fontSize: '13px',
                width: '160px'
              }}
              required
            />
            <input
              type="text"
              placeholder="ชื่อแสดง (เช่น โต๊ะ 13, ห้อง VIP)"
              value={newTableName}
              onChange={(e) => setNewTableName(e.target.value)}
              style={{
                padding: '6px 10px',
                borderRadius: '4px',
                border: '1px solid #A5D6A7',
                fontSize: '13px',
                width: '180px'
              }}
            />
            <button
              type="submit"
              disabled={isAdding}
              style={{
                padding: '6px 16px',
                borderRadius: '4px',
                backgroundColor: '#2E7D32',
                color: '#FFF',
                border: 'none',
                fontWeight: 'bold',
                cursor: 'pointer'
              }}
            >
              {isAdding ? 'กำลังบันทึก...' : 'บันทึกโต๊ะ'}
            </button>
            <button
              type="button"
              onClick={() => setShowAddTable(false)}
              style={{
                padding: '6px 12px',
                borderRadius: '4px',
                backgroundColor: '#FFF',
                color: '#666',
                border: '1px solid #CCC',
                cursor: 'pointer'
              }}
            >
              ยกเลิก
            </button>
          </form>
        )}

        {/* Modal Body / Printable Grid */}
        <div style={{
          flex: 1,
          overflowY: 'auto',
          padding: '20px'
        }}>
          {error && (
            <div style={{
              padding: '10px',
              backgroundColor: '#FFEBEE',
              color: '#C62828',
              borderRadius: '6px',
              marginBottom: '16px',
              fontSize: '13px'
            }}>
              [ ข้อผิดพลาด ] {error}
            </div>
          )}

          {isLoading ? (
            <div style={{ textAlign: 'center', padding: '40px', color: '#666' }}>
              กำลังโหลดข้อมูลโต๊ะและประมวลผล QR Code...
            </div>
          ) : (
            <div id="printable-qr-area">
              {activeTab === 'tables' ? (
                <div>
                  <div className="no-print" style={{
                    marginBottom: '16px',
                    fontSize: '13px',
                    color: '#555',
                    backgroundColor: '#E3F2FD',
                    padding: '10px 14px',
                    borderRadius: '6px'
                  }}>
                    คำแนะนำ: ป้ายเหล่านี้สามารถกดสั่งพิมพ์ออกเครื่องพิมพ์กระดาษ A4 หรือเครื่องพิมพ์สติ๊กเกอร์ แล้วนำไปตัดตามเส้นประเพื่อติดไว้ที่โต๊ะอาหารประจำจุดได้ทันที
                  </div>

                  <div style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
                    gap: '20px'
                  }}>
                    {tables.map((table) => {
                      const qr = qrImages[table.tableNumber];
                      return (
                        <div
                          key={table.id}
                          className="table-sticker-card"
                          style={{
                            backgroundColor: '#FFF',
                            borderRadius: '10px',
                            border: '2px solid #0D47A1',
                            padding: '16px',
                            textAlign: 'center',
                            boxShadow: '0 2px 8px rgba(0,0,0,0.06)',
                            display: 'flex',
                            flexDirection: 'column',
                            alignItems: 'center',
                            position: 'relative'
                          }}
                        >
                          {/* Store Name Badge */}
                          <div style={{
                            fontSize: '12px',
                            fontWeight: 'bold',
                            color: '#1565C0',
                            backgroundColor: '#E3F2FD',
                            padding: '3px 12px',
                            borderRadius: '12px',
                            marginBottom: '6px'
                          }}>
                            {storeName}
                          </div>

                          {/* Table Label */}
                          <div style={{
                            fontSize: '22px',
                            fontWeight: 900,
                            color: '#0D47A1',
                            marginBottom: '10px'
                          }}>
                            {table.name || `โต๊ะ ${table.tableNumber}`}
                          </div>

                          {/* QR Code Container */}
                          <div style={{
                            backgroundColor: '#FFF',
                            padding: '6px',
                            borderRadius: '8px',
                            border: '1px solid #E0E0E0',
                            marginBottom: '10px',
                            width: '180px',
                            height: '180px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                          }}>
                            {qr ? (
                              <img
                                src={qr}
                                alt={`QR โต๊ะ ${table.tableNumber}`}
                                style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                              />
                            ) : (
                              <span style={{ fontSize: '12px', color: '#999' }}>กำลังสร้าง QR...</span>
                            )}
                          </div>

                          {/* Guidance Text */}
                          <div style={{
                            fontSize: '12px',
                            fontWeight: 'bold',
                            color: '#2E7D32',
                            marginBottom: '2px'
                          }}>
                            [ สแกนเพื่อดูเมนู &amp; สั่งอาหาร ]
                          </div>
                          <div style={{
                            fontSize: '10.5px',
                            color: '#666',
                            marginBottom: '12px'
                          }}>
                            ออเดอร์ส่งตรงเข้าครัวและแคชเชียร์ทันที
                          </div>

                          {/* Action Buttons (Hidden when printing) */}
                          <div className="no-print" style={{
                            display: 'flex',
                            gap: '6px',
                            width: '100%',
                            marginTop: 'auto',
                            paddingTop: '8px',
                            borderTop: '1px solid #EEE'
                          }}>
                            <button
                              type="button"
                              onClick={() => qr && downloadQr(qr, `QR_${storeCode}_Table_${table.tableNumber}`)}
                              style={{
                                flex: 1,
                                padding: '5px',
                                fontSize: '11px',
                                borderRadius: '4px',
                                border: '1px solid #1976D2',
                                backgroundColor: '#FFF',
                                color: '#1976D2',
                                cursor: 'pointer',
                                fontWeight: 600
                              }}
                            >
                              ดาวน์โหลด
                            </button>
                            <button
                              type="button"
                              onClick={() => handleDeleteTable(table.id, table.tableNumber)}
                              style={{
                                padding: '5px 8px',
                                fontSize: '11px',
                                borderRadius: '4px',
                                border: '1px solid #FFCDD2',
                                backgroundColor: '#FFEBEE',
                                color: '#C62828',
                                cursor: 'pointer'
                              }}
                              title="ลบโต๊ะนี้"
                            >
                              ลบ
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ) : (
                /* Takeaway / Home Order QR */
                <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0' }}>
                  <div
                    className="table-sticker-card"
                    style={{
                      backgroundColor: '#FFF',
                      borderRadius: '12px',
                      border: '2px solid #E65100',
                      padding: '24px',
                      textAlign: 'center',
                      maxWidth: '380px',
                      width: '100%',
                      boxShadow: '0 4px 14px rgba(0,0,0,0.08)'
                    }}
                  >
                    <div style={{
                      display: 'inline-block',
                      backgroundColor: '#FFF3E0',
                      color: '#E65100',
                      padding: '4px 14px',
                      borderRadius: '14px',
                      fontSize: '12px',
                      fontWeight: 'bold',
                      marginBottom: '8px'
                    }}>
                      [ บริการสั่งกลับบ้าน / Takeaway Order ]
                    </div>

                    <div style={{ fontSize: '20px', fontWeight: 800, color: '#333', marginBottom: '4px' }}>
                      {storeName}
                    </div>

                    <div style={{ fontSize: '14px', color: '#666', marginBottom: '16px' }}>
                      สแกนสั่งจากที่บ้าน หรือสั่งล่วงหน้าแล้วมารับที่ร้าน
                    </div>

                    <div style={{
                      backgroundColor: '#FFF',
                      padding: '10px',
                      borderRadius: '10px',
                      border: '1px solid #FFE0B2',
                      margin: '0 auto 16px auto',
                      width: '240px',
                      height: '240px',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center'
                    }}>
                      {takeawayQr ? (
                        <img
                          src={takeawayQr}
                          alt="QR สั่งกลับบ้าน"
                          style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                        />
                      ) : (
                        <span>กำลังสร้าง QR...</span>
                      )}
                    </div>

                    <div style={{
                      fontSize: '13px',
                      fontWeight: 'bold',
                      color: '#E65100',
                      marginBottom: '4px'
                    }}>
                      [ สแกนสั่งอาหารล่วงหน้าไม่ต้องรอคิว ]
                    </div>
                    <div style={{ fontSize: '11px', color: '#777', marginBottom: '20px' }}>
                      ลิงก์: {baseUrl}/?store={storeCode}&amp;type=takeaway
                    </div>

                    <div className="no-print" style={{ display: 'flex', gap: '8px', justifyContent: 'center' }}>
                      <button
                        type="button"
                        onClick={() => takeawayQr && downloadQr(takeawayQr, `QR_${storeCode}_Takeaway`)}
                        style={{
                          padding: '8px 18px',
                          borderRadius: '6px',
                          backgroundColor: '#E65100',
                          color: '#FFF',
                          border: 'none',
                          fontWeight: 'bold',
                          fontSize: '13px',
                          cursor: 'pointer'
                        }}
                      >
                        ดาวน์โหลดรูปภาพ QR
                      </button>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
