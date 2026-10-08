import React, { useState, useEffect } from 'react';
import { getTables, reserveTable, type TableItem, type StoreInfo } from '../services/api';
import { realtimeService } from '../services/realtime';

interface TableReservationModalProps {
  storeInfo: StoreInfo | null;
  onClose: () => void;
  onSuccess?: (table: TableItem) => void;
}

export function TableReservationModal({ storeInfo, onClose, onSuccess }: TableReservationModalProps) {
  const [tables, setTables] = useState<TableItem[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [selectedTable, setSelectedTable] = useState<TableItem | null>(null);

  // Form fields
  const [customerName, setCustomerName] = useState<string>('');
  const [customerPhone, setCustomerPhone] = useState<string>('');
  const [resDate, setResDate] = useState<string>(() => {
    const today = new Date();
    return today.toISOString().split('T')[0];
  });
  const [resTime, setResTime] = useState<string>(() => {
    const now = new Date();
    now.setHours(now.getHours() + 1);
    const hh = String(now.getHours()).padStart(2, '0');
    const mm = String(Math.floor(now.getMinutes() / 15) * 15).padStart(2, '0');
    return `${hh}:${mm}`;
  });
  const [partySize, setPartySize] = useState<number>(2);
  const [notes, setNotes] = useState<string>('');

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [successInfo, setSuccessInfo] = useState<string>('');

  const loadTables = async () => {
    try {
      setIsLoading(true);
      const data = await getTables();
      setTables(data);
    } catch (err: any) {
      setErrorMessage(err.message || 'ไม่สามารถโหลดข้อมูลโต๊ะได้');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadTables();

    // Real-time table status sync via SignalR
    const unsub = realtimeService.onTableStatusChanged((updatedTable) => {
      setTables((prev) =>
        prev.map((t) => (t.id === updatedTable.id ? { ...t, ...updatedTable } : t))
      );
      // If the selected table was booked by someone else while looking
      setSelectedTable((curr) => {
        if (curr && curr.id === updatedTable.id) {
          return { ...curr, ...updatedTable };
        }
        return curr;
      });
    });

    return () => unsub();
  }, []);

  const handleSelectTable = (table: TableItem) => {
    if (table.status !== 0) return; // Only allow selecting Available tables
    setSelectedTable(table);
    setPartySize(table.capacity);
    setErrorMessage('');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTable) {
      setErrorMessage('กรุณาคลิกเลือกโต๊ะที่ว่างในผังโต๊ะด้านล่าง');
      return;
    }
    if (!customerName.trim()) {
      setErrorMessage('กรุณากรอกชื่อผู้จองโต๊ะ');
      return;
    }
    const cleanPhone = customerPhone.replace(/\D/g, '');
    if (!cleanPhone || cleanPhone.length < 9) {
      setErrorMessage('กรุณากรอกเบอร์โทรศัพท์ที่ติดต่อได้ (อย่างน้อย 9-10 หลัก)');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage('');

    try {
      // Parse ISO datetime
      const [year, month, day] = resDate.split('-').map(Number);
      const [hours, minutes] = resTime.split(':').map(Number);
      const bookingDateTime = new Date(year, month - 1, day, hours, minutes);

      const updated = await reserveTable(selectedTable.id, {
        customerName: customerName.trim(),
        customerPhone: cleanPhone,
        reservationTime: bookingDateTime.toISOString(),
        partySize: partySize > 0 ? partySize : selectedTable.capacity,
        notes: notes.trim() || undefined,
      });

      setSuccessInfo(`จองโต๊ะ ${selectedTable.tableNumber} เรียบร้อยแล้ว!`);
      if (onSuccess) {
        onSuccess(updated);
      }
      setTimeout(() => {
        onClose();
      }, 1800);
    } catch (err: any) {
      setErrorMessage(err.message || 'ไม่สามารถทำการจองโต๊ะได้ กรุณาลองใหม่อีกครั้ง');
    } finally {
      setIsSubmitting(false);
    }
  };

  const countAvailable = tables.filter((t) => t.status === 0).length;
  const countReserved = tables.filter((t) => t.status === 2).length;
  const countOccupied = tables.filter((t) => t.status === 1 || t.status === 3).length;

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(15, 23, 42, 0.75)',
        backdropFilter: 'blur(3px)',
        zIndex: 9999,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '16px',
        boxSizing: 'border-box',
      }}
    >
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: '10px',
          width: '100%',
          maxWidth: '840px',
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.2), 0 8px 10px -6px rgba(0, 0, 0, 0.2)',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            backgroundColor: '#1E3A8A',
            color: '#FFFFFF',
            padding: '14px 20px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div>
            <div style={{ fontWeight: 'bold', fontSize: '16px', letterSpacing: '0.3px' }}>
              [ จองโต๊ะอาหารล่วงหน้า ]
            </div>
            <div style={{ fontSize: '12px', color: '#BFDBFE', marginTop: '2px' }}>
              {storeInfo?.storeName || 'ร้านอาหาร'} — เลือกโต๊ะและเวลาที่สะดวก ข้อมูลจะซิงค์เข้าสู่ระบบ POS หน้าร้านทันที
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            style={{
              background: 'rgba(255,255,255,0.15)',
              border: 'none',
              color: '#FFFFFF',
              borderRadius: '4px',
              padding: '6px 12px',
              fontSize: '13px',
              fontWeight: 'bold',
              cursor: 'pointer',
            }}
          >
            [ ปิด ]
          </button>
        </div>

        {/* Color Legend Bar */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            borderBottom: '1px solid #E2E8F0',
            padding: '10px 20px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '8px',
            fontSize: '12px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '14px', flexWrap: 'wrap' }}>
            <span style={{ fontWeight: 'bold', color: '#475569' }}>สถานะโต๊ะ:</span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span
                style={{
                  display: 'inline-block',
                  width: '14px',
                  height: '14px',
                  borderRadius: '3px',
                  backgroundColor: '#FFFFFF',
                  border: '1.5px solid #CBD5E1',
                }}
              />
              <span style={{ color: '#334155', fontWeight: 600 }}>สีปกติ = โต๊ะว่าง ({countAvailable})</span>
            </span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span
                style={{
                  display: 'inline-block',
                  width: '14px',
                  height: '14px',
                  borderRadius: '3px',
                  backgroundColor: '#FEF08A',
                  border: '1.5px solid #EAB308',
                }}
              />
              <span style={{ color: '#854D0E', fontWeight: 600 }}>สีเหลือง = จองแล้ว ({countReserved})</span>
            </span>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span
                style={{
                  display: 'inline-block',
                  width: '14px',
                  height: '14px',
                  borderRadius: '3px',
                  backgroundColor: '#FEE2E2',
                  border: '1.5px solid #DC2626',
                }}
              />
              <span style={{ color: '#991B1B', fontWeight: 600 }}>สีแดง = มีลูกค้า ({countOccupied})</span>
            </span>
          </div>
          <button
            type="button"
            onClick={loadTables}
            style={{
              background: '#F1F5F9',
              border: '1px solid #CBD5E1',
              color: '#334155',
              padding: '3px 8px',
              borderRadius: '4px',
              fontSize: '11px',
              cursor: 'pointer',
            }}
          >
            [ รีเฟรชสถานะ ]
          </button>
        </div>

        {/* Body Container */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '18px 20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {errorMessage && (
            <div
              style={{
                backgroundColor: '#FEF2F2',
                border: '1px solid #F87171',
                borderRadius: '6px',
                padding: '10px 14px',
                color: '#991B1B',
                fontSize: '13px',
                fontWeight: 600,
              }}
            >
              [ ข้อผิดพลาด ] {errorMessage}
            </div>
          )}

          {successInfo && (
            <div
              style={{
                backgroundColor: '#F0FDF4',
                border: '1px solid #4ADE80',
                borderRadius: '6px',
                padding: '12px 14px',
                color: '#166534',
                fontSize: '14px',
                fontWeight: 'bold',
                textAlign: 'center',
              }}
            >
              [ สำเร็จ ] {successInfo}
            </div>
          )}

          {/* Step 1: Select Available Table */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <span style={{ fontWeight: 'bold', fontSize: '13.5px', color: '#1E293B' }}>
                ขั้นตอนที่ 1: เลือกโต๊ะอาหารที่ต้องการจอง (เฉพาะโต๊ะสีปกติที่ยังว่าง)
              </span>
              {selectedTable && (
                <span
                  style={{
                    backgroundColor: '#EFF6FF',
                    color: '#1D4ED8',
                    border: '1px solid #93C5FD',
                    padding: '2px 8px',
                    borderRadius: '4px',
                    fontSize: '12px',
                    fontWeight: 'bold',
                  }}
                >
                  เลือกแล้ว: โต๊ะ {selectedTable.tableNumber} ({selectedTable.capacity} ที่นั่ง)
                </span>
              )}
            </div>

            {isLoading ? (
              <div style={{ textAlign: 'center', padding: '30px', color: '#64748B', fontSize: '13px' }}>
                กำลังตรวจสอบสถานะโต๊ะอาหาร...
              </div>
            ) : tables.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '24px', backgroundColor: '#F8FAFC', borderRadius: '6px', color: '#64748B', fontSize: '13px' }}>
                ยังไม่มีข้อมูลโต๊ะอาหารในระบบ
              </div>
            ) : (
              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fill, minmax(130px, 1fr))',
                  gap: '10px',
                  maxHeight: '220px',
                  overflowY: 'auto',
                  padding: '4px',
                  border: '1px solid #E2E8F0',
                  borderRadius: '6px',
                  backgroundColor: '#FAFAFA',
                }}
              >
                {tables.map((t) => {
                  const isAvail = t.status === 0;
                  const isRes = t.status === 2;
                  const isOcc = t.status === 1 || t.status === 3;
                  const isCurrent = selectedTable?.id === t.id;

                  // Border and background colors matching strict requirements
                  let bg = '#FFFFFF';
                  let border = '#CBD5E1';
                  let textColor = '#1E293B';
                  let badgeBg = '#F1F5F9';
                  let badgeText = '#475569';
                  let badgeLabel = '[ว่าง]';

                  if (isOcc) {
                    bg = '#FEE2E2';
                    border = '#DC2626';
                    textColor = '#991B1B';
                    badgeBg = '#FECACA';
                    badgeText = '#991B1B';
                    badgeLabel = '[มีลูกค้า]';
                  } else if (isRes) {
                    bg = '#FEF08A';
                    border = '#EAB308';
                    textColor = '#854D0E';
                    badgeBg = '#FDE68A';
                    badgeText = '#854D0E';
                    badgeLabel = '[จองแล้ว]';
                  }

                  if (isCurrent) {
                    border = '#2563EB';
                    bg = '#EFF6FF';
                  }

                  return (
                    <div
                      key={t.id}
                      onClick={() => isAvail && handleSelectTable(t)}
                      style={{
                        backgroundColor: bg,
                        border: isCurrent ? '2.5px solid #2563EB' : `1.5px solid ${border}`,
                        borderRadius: '6px',
                        padding: '10px 8px',
                        cursor: isAvail ? 'pointer' : 'not-allowed',
                        opacity: isAvail ? 1 : 0.75,
                        transition: 'transform 0.1s ease',
                        boxShadow: isCurrent ? '0 0 0 2px rgba(37,99,235,0.2)' : 'none',
                        position: 'relative',
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <span style={{ fontWeight: 'bold', fontSize: '15px', color: textColor }}>
                          {t.tableNumber}
                        </span>
                        <span style={{ fontSize: '11px', color: '#64748B' }}>
                          {t.capacity} ที่
                        </span>
                      </div>
                      <div style={{ fontSize: '11px', color: '#64748B', margin: '4px 0', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                        {t.name || `โต๊ะ ${t.tableNumber}`}
                      </div>
                      <div style={{ marginTop: '4px' }}>
                        <span
                          style={{
                            display: 'inline-block',
                            backgroundColor: badgeBg,
                            color: badgeText,
                            borderRadius: '3px',
                            padding: '1px 5px',
                            fontSize: '10px',
                            fontWeight: 'bold',
                          }}
                        >
                          {badgeLabel}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Step 2: Reservation Details Form */}
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div style={{ fontWeight: 'bold', fontSize: '13.5px', color: '#1E293B', borderTop: '1px dashed #CBD5E1', paddingTop: '12px' }}>
              ขั้นตอนที่ 2: กรอกข้อมูลผู้จองและเวลาที่ต้องการรับประทาน
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                  ชื่อผู้จอง *
                </label>
                <input
                  type="text"
                  required
                  placeholder="เช่น คุณสมชาย"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '5px',
                    border: '1px solid #CBD5E1',
                    fontSize: '13px',
                    boxSizing: 'border-box',
                  }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                  เบอร์โทรศัพท์ติดต่อ *
                </label>
                <input
                  type="tel"
                  required
                  placeholder="เช่น 0812345678"
                  value={customerPhone}
                  onChange={(e) => setCustomerPhone(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '5px',
                    border: '1px solid #CBD5E1',
                    fontSize: '13px',
                    boxSizing: 'border-box',
                  }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                  วันที่จอง
                </label>
                <input
                  type="date"
                  required
                  value={resDate}
                  onChange={(e) => setResDate(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '5px',
                    border: '1px solid #CBD5E1',
                    fontSize: '13px',
                    boxSizing: 'border-box',
                  }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                  เวลาที่ต้องการเข้าโต๊ะ
                </label>
                <input
                  type="time"
                  required
                  value={resTime}
                  onChange={(e) => setResTime(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '5px',
                    border: '1px solid #CBD5E1',
                    fontSize: '13px',
                    boxSizing: 'border-box',
                  }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                  จำนวนลูกค้า (ท่าน)
                </label>
                <input
                  type="number"
                  min={1}
                  max={selectedTable ? selectedTable.capacity * 2 : 20}
                  value={partySize}
                  onChange={(e) => setPartySize(Number(e.target.value))}
                  style={{
                    width: '100%',
                    padding: '8px 10px',
                    borderRadius: '5px',
                    border: '1px solid #CBD5E1',
                    fontSize: '13px',
                    boxSizing: 'border-box',
                  }}
                />
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 'bold', color: '#334155', marginBottom: '4px' }}>
                หมายเหตุเพิ่มเติม / คำขอพิเศษ (ถ้ามี)
              </label>
              <input
                type="text"
                placeholder="เช่น ขอโต๊ะริมหน้าต่าง, เก้าอี้เด็ก 1 ตัว"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 10px',
                  borderRadius: '5px',
                  border: '1px solid #CBD5E1',
                  fontSize: '13px',
                  boxSizing: 'border-box',
                }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '10px' }}>
              <button
                type="button"
                onClick={onClose}
                style={{
                  backgroundColor: '#F1F5F9',
                  border: '1px solid #CBD5E1',
                  color: '#475569',
                  padding: '9px 18px',
                  borderRadius: '6px',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                ยกเลิก
              </button>
              <button
                type="submit"
                disabled={isSubmitting || !selectedTable}
                style={{
                  backgroundColor: !selectedTable ? '#94A3B8' : '#D97706',
                  border: 'none',
                  color: '#FFFFFF',
                  padding: '9px 24px',
                  borderRadius: '6px',
                  fontSize: '13.5px',
                  fontWeight: 'bold',
                  cursor: !selectedTable ? 'not-allowed' : 'pointer',
                  boxShadow: '0 2px 4px rgba(0,0,0,0.15)',
                }}
              >
                {isSubmitting ? 'กำลังบันทึกการจอง...' : '[ ยืนยันการจองโต๊ะ (สีเหลือง) ]'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
