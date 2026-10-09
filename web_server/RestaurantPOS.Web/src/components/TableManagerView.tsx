import React, { useState, useEffect } from 'react';
import {
  getTables,
  createTable,
  deleteTable,
  reserveTable,
  checkInTable,
  cancelTableReservation,
  updateTableStatus,
  type TableItem,
  type StoreInfo,
} from '../services/api';
import { realtimeService } from '../services/realtime';

interface TableManagerViewProps {
  storeInfo: StoreInfo | null;
}

export function TableManagerView({ storeInfo }: TableManagerViewProps) {
  const [tables, setTables] = useState<TableItem[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [selectedTable, setSelectedTable] = useState<TableItem | null>(null);
  const [filterMode, setFilterMode] = useState<'all' | 'available' | 'reserved' | 'occupied'>('all');

  // New Table Form
  const [newTableNo, setNewTableNo] = useState<string>('');
  const [newTableName, setNewTableName] = useState<string>('');
  const [newTableCap, setNewTableCap] = useState<number>(4);
  const [isAddingTable, setIsAddingTable] = useState<boolean>(false);

  // Reservation Form for Staff
  const [staffCustName, setStaffCustName] = useState<string>('');
  const [staffCustPhone, setStaffCustPhone] = useState<string>('');
  const [staffResTime, setStaffResTime] = useState<string>(() => {
    const d = new Date();
    d.setHours(d.getHours() + 1);
    return `${String(d.getHours()).padStart(2, '0')}:${String(Math.floor(d.getMinutes() / 15) * 15).padStart(2, '0')}`;
  });
  const [staffPartySize, setStaffPartySize] = useState<number>(4);
  const [staffNotes, setStaffNotes] = useState<string>('');
  const [isSubmittingAction, setIsSubmittingAction] = useState<boolean>(false);

  const [notification, setNotification] = useState<{ text: string; isError?: boolean } | null>(null);

  const showNotice = (text: string, isError = false) => {
    setNotification({ text, isError });
    setTimeout(() => setNotification(null), 4000);
  };

  const loadTables = async () => {
    try {
      setIsLoading(true);
      const data = await getTables();
      setTables(data);
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถโหลดข้อมูลโต๊ะได้', true);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadTables();

    // SignalR Real-Time update
    const unsub = realtimeService.onTableStatusChanged((updated) => {
      setTables((prev) =>
        prev.map((t) => (t.id === updated.id ? { ...t, ...updated } : t))
      );
      setSelectedTable((curr) => (curr && curr.id === updated.id ? { ...curr, ...updated } : curr));
    });

    const unsubForce = realtimeService.onForceSync(() => {
      loadTables();
    });

    return () => {
      unsub();
      unsubForce();
    };
  }, []);

  const handleSelectTable = (table: TableItem) => {
    setSelectedTable(table);
    setStaffCustName('');
    setStaffCustPhone('');
    setStaffPartySize(table.capacity);
    setStaffNotes('');
  };

  const handleAddTable = async (e: React.FormEvent) => {
    e.preventDefault();
    const cleanNo = newTableNo.trim().toUpperCase();
    if (!cleanNo) {
      showNotice('กรุณากรอกเลขที่โต๊ะ', true);
      return;
    }

    try {
      setIsAddingTable(true);
      await createTable({
        tableNumber: cleanNo,
        name: newTableName.trim() || `โต๊ะ ${cleanNo}`,
        capacity: newTableCap > 0 ? newTableCap : 4,
      });
      setNewTableNo('');
      setNewTableName('');
      setNewTableCap(4);
      showNotice(`เพิ่มโต๊ะ ${cleanNo} เรียบร้อยแล้ว`);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถเพิ่มโต๊ะได้', true);
    } finally {
      setIsAddingTable(false);
    }
  };

  const handleDeleteTable = async (id: number, tableNo: string) => {
    if (!window.confirm(`ต้องการลบโต๊ะ ${tableNo} ใช่หรือไม่?`)) return;
    try {
      await deleteTable(id);
      showNotice(`ลบโต๊ะ ${tableNo} เรียบร้อยแล้ว`);
      if (selectedTable?.id === id) setSelectedTable(null);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถลบโต๊ะได้', true);
    }
  };

  const handleStaffReserve = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTable) return;
    if (!staffCustName.trim() || !staffCustPhone.trim()) {
      showNotice('กรุณากรอกชื่อและเบอร์โทรศัพท์ลูกค้า', true);
      return;
    }

    try {
      setIsSubmittingAction(true);
      const now = new Date();
      const [hh, mm] = staffResTime.split(':').map(Number);
      const resDateTime = new Date(now.getFullYear(), now.getMonth(), now.getDate(), hh || 18, mm || 0, 0);

      await reserveTable(selectedTable.id, {
        customerName: staffCustName.trim(),
        customerPhone: staffCustPhone.trim(),
        reservationTime: resDateTime.toISOString(),
        partySize: staffPartySize > 0 ? staffPartySize : selectedTable.capacity,
        notes: staffNotes.trim() || undefined,
      });

      showNotice(`บันทึกการจองโต๊ะ ${selectedTable.tableNumber} เรียบร้อยแล้ว (สีเหลือง)`);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถบันทึกการจองได้', true);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleCheckIn = async () => {
    if (!selectedTable) return;
    if (!window.confirm(`ต้องการเช็คอินลูกค้าเข้าโต๊ะ ${selectedTable.tableNumber} ใช่หรือไม่? สถานะจะเปลี่ยนเป็นมีลูกค้า (สีแดง)`)) return;

    try {
      setIsSubmittingAction(true);
      await checkInTable(selectedTable.id);
      showNotice(`เช็คอินโต๊ะ ${selectedTable.tableNumber} เรียบร้อยแล้ว (สีแดง)`);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถเช็คอินได้', true);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleCancelReservation = async () => {
    if (!selectedTable) return;
    if (!window.confirm(`ต้องการยกเลิกการจองโต๊ะ ${selectedTable.tableNumber} ใช่หรือไม่? สถานะจะเปลี่ยนกลับเป็นโต๊ะว่าง (สีปกติ)`)) return;

    try {
      setIsSubmittingAction(true);
      await cancelTableReservation(selectedTable.id);
      showNotice(`ยกเลิกการจองโต๊ะ ${selectedTable.tableNumber} เรียบร้อยแล้ว`);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถยกเลิกการจองได้', true);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const handleToggleOccupied = async (status: number) => {
    if (!selectedTable) return;
    try {
      setIsSubmittingAction(true);
      await updateTableStatus(selectedTable.id, status);
      showNotice(`อัปเดตสถานะโต๊ะ ${selectedTable.tableNumber} เรียบร้อยแล้ว`);
      await loadTables();
    } catch (err: any) {
      showNotice(err.message || 'ไม่สามารถเปลี่ยนสถานะโต๊ะได้', true);
    } finally {
      setIsSubmittingAction(false);
    }
  };

  const countAvailable = tables.filter((t) => t.status === 0).length;
  const countReserved = tables.filter((t) => t.status === 2).length;
  const countOccupied = tables.filter((t) => t.status === 1 || t.status === 3).length;

  const filteredTables = tables.filter((t) => {
    if (filterMode === 'available') return t.status === 0;
    if (filterMode === 'reserved') return t.status === 2;
    if (filterMode === 'occupied') return t.status === 1 || t.status === 3;
    return true;
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Top Banner */}
      <div
        style={{
          backgroundColor: '#1E3A8A',
          color: '#FFFFFF',
          borderRadius: '8px',
          padding: '14px 20px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div>
          <div style={{ fontWeight: 'bold', fontSize: '16px' }}>
            [ ผังโต๊ะอาหาร &amp; ระบบจัดการจองโต๊ะ (Floor &amp; Reservation Manager) ]
          </div>
          <div style={{ fontSize: '12px', color: '#BFDBFE', marginTop: '2px' }}>
            {storeInfo?.storeName || 'ร้านอาหาร'} — อัปเดตสดแบบ Real-Time ร่วมกับ Windows POS และหน้าเว็บลูกค้า
          </div>
        </div>

        {/* Status Count Badges */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
          <span
            style={{
              backgroundColor: '#F1F5F9',
              color: '#334155',
              padding: '4px 10px',
              borderRadius: '6px',
              fontSize: '12px',
              fontWeight: 'bold',
            }}
          >
            [ ว่าง: {countAvailable} ]
          </span>
          <span
            style={{
              backgroundColor: '#FEF08A',
              color: '#854D0E',
              padding: '4px 10px',
              borderRadius: '6px',
              fontSize: '12px',
              fontWeight: 'bold',
            }}
          >
            [ จองแล้ว: {countReserved} ]
          </span>
          <span
            style={{
              backgroundColor: '#FEE2E2',
              color: '#991B1B',
              padding: '4px 10px',
              borderRadius: '6px',
              fontSize: '12px',
              fontWeight: 'bold',
            }}
          >
            [ ทำงานอยู่: {countOccupied} ]
          </span>
          <button
            type="button"
            onClick={loadTables}
            style={{
              backgroundColor: '#2563EB',
              color: '#FFFFFF',
              border: 'none',
              padding: '5px 12px',
              borderRadius: '6px',
              fontSize: '12px',
              fontWeight: 'bold',
              cursor: 'pointer',
            }}
          >
            [ รีเฟรชข้อมูล ]
          </button>
        </div>
      </div>

      {notification && (
        <div
          style={{
            backgroundColor: notification.isError ? '#FEF2F2' : '#F0FDF4',
            border: `1px solid ${notification.isError ? '#F87171' : '#4ADE80'}`,
            color: notification.isError ? '#991B1B' : '#166534',
            borderRadius: '6px',
            padding: '10px 14px',
            fontSize: '13px',
            fontWeight: 'bold',
          }}
        >
          {notification.isError ? '[ ข้อผิดพลาด ] ' : '[ สำเร็จ ] '}
          {notification.text}
        </div>
      )}

      {/* Main 2-Column Layout */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 340px', gap: '16px', alignItems: 'start' }}>
        {/* Left Column: Floor Cards */}
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '1px solid #E2E8F0',
            borderRadius: '8px',
            padding: '16px',
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}
        >
          {/* Filter Bar */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
            <span style={{ fontWeight: 'bold', fontSize: '14px', color: '#1E293B' }}>
              ผังโต๊ะอาหาร ({filteredTables.length} โต๊ะ)
            </span>
            <div style={{ display: 'flex', gap: '6px', fontSize: '12px' }}>
              <button
                type="button"
                onClick={() => setFilterMode('all')}
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  border: filterMode === 'all' ? '1.5px solid #2563EB' : '1px solid #CBD5E1',
                  backgroundColor: filterMode === 'all' ? '#EFF6FF' : '#FFFFFF',
                  color: filterMode === 'all' ? '#1D4ED8' : '#475569',
                  fontWeight: filterMode === 'all' ? 'bold' : 'normal',
                  cursor: 'pointer',
                }}
              >
                ทั้งหมด
              </button>
              <button
                type="button"
                onClick={() => setFilterMode('available')}
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  border: filterMode === 'available' ? '1.5px solid #2563EB' : '1px solid #CBD5E1',
                  backgroundColor: filterMode === 'available' ? '#EFF6FF' : '#FFFFFF',
                  color: filterMode === 'available' ? '#1D4ED8' : '#475569',
                  fontWeight: filterMode === 'available' ? 'bold' : 'normal',
                  cursor: 'pointer',
                }}
              >
                ว่าง (สีปกติ)
              </button>
              <button
                type="button"
                onClick={() => setFilterMode('reserved')}
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  border: filterMode === 'reserved' ? '1.5px solid #EAB308' : '1px solid #CBD5E1',
                  backgroundColor: filterMode === 'reserved' ? '#FEF08A' : '#FFFFFF',
                  color: filterMode === 'reserved' ? '#854D0E' : '#475569',
                  fontWeight: filterMode === 'reserved' ? 'bold' : 'normal',
                  cursor: 'pointer',
                }}
              >
                จองแล้ว (สีเหลือง)
              </button>
              <button
                type="button"
                onClick={() => setFilterMode('occupied')}
                style={{
                  padding: '4px 10px',
                  borderRadius: '4px',
                  border: filterMode === 'occupied' ? '1.5px solid #DC2626' : '1px solid #CBD5E1',
                  backgroundColor: filterMode === 'occupied' ? '#FEE2E2' : '#FFFFFF',
                  color: filterMode === 'occupied' ? '#991B1B' : '#475569',
                  fontWeight: filterMode === 'occupied' ? 'bold' : 'normal',
                  cursor: 'pointer',
                }}
              >
                ทำงานอยู่ (สีแดง)
              </button>
            </div>
          </div>

          {/* Cards Grid */}
          {isLoading ? (
            <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>กำลังโหลดผังโต๊ะ...</div>
          ) : filteredTables.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '30px', backgroundColor: '#F8FAFC', borderRadius: '6px', color: '#64748B' }}>
              ไม่พบโต๊ะในหมวดหมู่นี้
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))', gap: '10px' }}>
              {filteredTables.map((t) => {
                const isRes = t.status === 2;
                const isOcc = t.status === 1 || t.status === 3;
                const isCurrent = selectedTable?.id === t.id;

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

                return (
                  <div
                    key={t.id}
                    onClick={() => handleSelectTable(t)}
                    style={{
                      backgroundColor: bg,
                      border: isCurrent ? '2.5px solid #1D4ED8' : `1.5px solid ${border}`,
                      borderRadius: '6px',
                      padding: '10px',
                      cursor: 'pointer',
                      boxShadow: isCurrent ? '0 0 0 2px rgba(29, 78, 216, 0.25)' : 'none',
                      display: 'flex',
                      flexDirection: 'column',
                      justifyContent: 'space-between',
                      minHeight: '95px',
                    }}
                  >
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <span style={{ fontWeight: 'bold', fontSize: '15px', color: textColor }}>
                          {t.tableNumber}
                        </span>
                        <span style={{ fontSize: '11px', color: '#64748B' }}>
                          {t.capacity} ที่
                        </span>
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#475569', marginTop: '2px', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                        {t.name || `โต๊ะ ${t.tableNumber}`}
                      </div>

                      {/* Extra info for reserved / occupied */}
                      {isRes && (
                        <div style={{ fontSize: '11px', color: '#854D0E', fontWeight: 'bold', marginTop: '3px', textOverflow: 'ellipsis', overflow: 'hidden', whiteSpace: 'nowrap' }}>
                          {t.reservationCustomerName || 'จองแล้ว'}
                        </div>
                      )}
                      {isOcc && t.currentBillAmount > 0 && (
                        <div style={{ fontSize: '11px', color: '#991B1B', fontWeight: 'bold', marginTop: '3px' }}>
                          ยอด: {t.currentBillAmount.toFixed(0)} บ.
                        </div>
                      )}
                    </div>

                    <div style={{ marginTop: '6px' }}>
                      <span
                        style={{
                          backgroundColor: badgeBg,
                          color: badgeText,
                          borderRadius: '3px',
                          padding: '1px 6px',
                          fontSize: '10.5px',
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

          {/* Quick Add Table Bar */}
          <form
            onSubmit={handleAddTable}
            style={{
              marginTop: '12px',
              borderTop: '1px dashed #CBD5E1',
              paddingTop: '12px',
              display: 'flex',
              gap: '8px',
              alignItems: 'center',
              flexWrap: 'wrap',
            }}
          >
            <span style={{ fontWeight: 'bold', fontSize: '12.5px', color: '#334155' }}>+ เพิ่มโต๊ะใหม่:</span>
            <input
              type="text"
              placeholder="เลขที่โต๊ะ (เช่น T09)"
              value={newTableNo}
              onChange={(e) => setNewTableNo(e.target.value)}
              style={{ width: '130px', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12.5px' }}
            />
            <input
              type="text"
              placeholder="ชื่อเรียกโต๊ะ (ถ้ามี)"
              value={newTableName}
              onChange={(e) => setNewTableName(e.target.value)}
              style={{ flex: 1, minWidth: '120px', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12.5px' }}
            />
            <input
              type="number"
              min={1}
              max={50}
              placeholder="ที่นั่ง"
              value={newTableCap}
              onChange={(e) => setNewTableCap(Number(e.target.value))}
              style={{ width: '65px', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12.5px' }}
            />
            <button
              type="submit"
              disabled={isAddingTable}
              style={{
                backgroundColor: '#1D4ED8',
                color: '#FFFFFF',
                border: 'none',
                padding: '6px 14px',
                borderRadius: '4px',
                fontSize: '12.5px',
                fontWeight: 'bold',
                cursor: 'pointer',
              }}
            >
              [ + บันทึกโต๊ะ ]
            </button>
          </form>
        </div>

        {/* Right Column: Selected Table Action Details */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            border: '1px solid #E2E8F0',
            borderRadius: '8px',
            padding: '16px',
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}
        >
          {selectedTable ? (
            <>
              {/* Selected Table Header Card */}
              <div
                style={{
                  backgroundColor: '#FFFFFF',
                  border: '1px solid #CBD5E1',
                  borderRadius: '6px',
                  padding: '12px',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 'bold', fontSize: '16px', color: '#0F172A' }}>
                    {selectedTable.tableNumber} ({selectedTable.name})
                  </span>
                  <span
                    style={{
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 'bold',
                      backgroundColor:
                        selectedTable.status === 0
                          ? '#F1F5F9'
                          : selectedTable.status === 2
                          ? '#FEF08A'
                          : '#FEE2E2',
                      color:
                        selectedTable.status === 0
                          ? '#334155'
                          : selectedTable.status === 2
                          ? '#854D0E'
                          : '#991B1B',
                    }}
                  >
                    {selectedTable.status === 0
                      ? '[ว่าง]'
                      : selectedTable.status === 2
                      ? '[จองแล้ว]'
                      : '[มีลูกค้า]'}
                  </span>
                </div>
                <div style={{ fontSize: '12px', color: '#64748B', marginTop: '4px' }}>
                  ความจุรองรับ: {selectedTable.capacity} ที่นั่ง
                </div>
              </div>

              {/* Status Specific Actions */}
              {selectedTable.status === 2 && (
                /* Reserved Table Actions */
                <div
                  style={{
                    backgroundColor: '#FEF08A',
                    border: '1.5px solid #EAB308',
                    borderRadius: '6px',
                    padding: '12px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                  }}
                >
                  <div style={{ fontWeight: 'bold', fontSize: '13px', color: '#854D0E' }}>
                    [ ข้อมูลการจองโต๊ะ ]
                  </div>
                  <div style={{ fontSize: '12px', color: '#713F12' }}>
                    <div><strong>ผู้จอง:</strong> {selectedTable.reservationCustomerName || '-'}</div>
                    <div><strong>เบอร์โทร:</strong> {selectedTable.reservationCustomerPhone || '-'}</div>
                    <div>
                      <strong>เวลาที่จอง:</strong>{' '}
                      {selectedTable.reservationTime
                        ? new Date(selectedTable.reservationTime).toLocaleString('th-TH')
                        : '-'}
                    </div>
                    <div><strong>จำนวน:</strong> {selectedTable.reservationPartySize || selectedTable.capacity} ท่าน</div>
                    {selectedTable.reservationNotes && (
                      <div><strong>หมายเหตุ:</strong> {selectedTable.reservationNotes}</div>
                    )}
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', marginTop: '8px' }}>
                    <button
                      type="button"
                      disabled={isSubmittingAction}
                      onClick={handleCheckIn}
                      style={{
                        backgroundColor: '#16A34A',
                        color: '#FFFFFF',
                        border: 'none',
                        padding: '9px',
                        borderRadius: '5px',
                        fontWeight: 'bold',
                        fontSize: '13px',
                        cursor: 'pointer',
                      }}
                    >
                      [ เช็คอินเข้าโต๊ะ (เปลี่ยนเป็นสีแดง) ]
                    </button>
                    <button
                      type="button"
                      disabled={isSubmittingAction}
                      onClick={handleCancelReservation}
                      style={{
                        backgroundColor: '#FEE2E2',
                        color: '#991B1B',
                        border: '1px solid #FCA5A5',
                        padding: '7px',
                        borderRadius: '5px',
                        fontWeight: 'bold',
                        fontSize: '12px',
                        cursor: 'pointer',
                      }}
                    >
                      [ ยกเลิกการจอง (เปลี่ยนเป็นสีปกติ) ]
                    </button>
                  </div>
                </div>
              )}

              {selectedTable.status === 0 && (
                /* Available Table Reservation Form */
                <form
                  onSubmit={handleStaffReserve}
                  style={{
                    backgroundColor: '#FFFFFF',
                    border: '1px solid #CBD5E1',
                    borderRadius: '6px',
                    padding: '12px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                  }}
                >
                  <div style={{ fontWeight: 'bold', fontSize: '13px', color: '#1E293B' }}>
                    [ บันทึกการจองโต๊ะใหม่ ]
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 'bold', color: '#475569', marginBottom: '2px' }}>
                      ชื่อผู้จอง *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="ชื่อลูกค้า"
                      value={staffCustName}
                      onChange={(e) => setStaffCustName(e.target.value)}
                      style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12px', boxSizing: 'border-box' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 'bold', color: '#475569', marginBottom: '2px' }}>
                      เบอร์โทรศัพท์ *
                    </label>
                    <input
                      type="tel"
                      required
                      placeholder="เบอร์โทรศัพท์"
                      value={staffCustPhone}
                      onChange={(e) => setStaffCustPhone(e.target.value)}
                      style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12px', boxSizing: 'border-box' }}
                    />
                  </div>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <div style={{ flex: 1 }}>
                      <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 'bold', color: '#475569', marginBottom: '2px' }}>
                        เวลาที่จอง
                      </label>
                      <input
                        type="time"
                        required
                        value={staffResTime}
                        onChange={(e) => setStaffResTime(e.target.value)}
                        style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12px', boxSizing: 'border-box' }}
                      />
                    </div>
                    <div style={{ width: '80px' }}>
                      <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 'bold', color: '#475569', marginBottom: '2px' }}>
                        ที่นั่ง
                      </label>
                      <input
                        type="number"
                        min={1}
                        value={staffPartySize}
                        onChange={(e) => setStaffPartySize(Number(e.target.value))}
                        style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12px', boxSizing: 'border-box' }}
                      />
                    </div>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 'bold', color: '#475569', marginBottom: '2px' }}>
                      หมายเหตุ (ถ้ามี)
                    </label>
                    <input
                      type="text"
                      placeholder="คำขอพิเศษ"
                      value={staffNotes}
                      onChange={(e) => setStaffNotes(e.target.value)}
                      style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #CBD5E1', fontSize: '12px', boxSizing: 'border-box' }}
                    />
                  </div>

                  <button
                    type="submit"
                    disabled={isSubmittingAction}
                    style={{
                      marginTop: '4px',
                      backgroundColor: '#D97706',
                      color: '#FFFFFF',
                      border: 'none',
                      padding: '8px',
                      borderRadius: '5px',
                      fontWeight: 'bold',
                      fontSize: '12.5px',
                      cursor: 'pointer',
                    }}
                  >
                    [ บันทึกการจอง (เปลี่ยนเป็นสีเหลือง) ]
                  </button>

                  <button
                    type="button"
                    disabled={isSubmittingAction}
                    onClick={() => handleToggleOccupied(1)}
                    style={{
                      backgroundColor: '#DC2626',
                      color: '#FFFFFF',
                      border: 'none',
                      padding: '7px',
                      borderRadius: '5px',
                      fontWeight: 'bold',
                      fontSize: '12px',
                      cursor: 'pointer',
                    }}
                  >
                    [ เปิดโต๊ะมีลูกค้านั่ง (สีแดง) ]
                  </button>
                </form>
              )}

              {(selectedTable.status === 1 || selectedTable.status === 3) && (
                /* Occupied Table Actions */
                <div
                  style={{
                    backgroundColor: '#FEE2E2',
                    border: '1.5px solid #DC2626',
                    borderRadius: '6px',
                    padding: '12px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                  }}
                >
                  <div style={{ fontWeight: 'bold', fontSize: '13px', color: '#991B1B' }}>
                    [ โต๊ะทำงานอยู่ / มีลูกค้า ]
                  </div>
                  <div style={{ fontSize: '12.5px', color: '#7F1D1D' }}>
                    <div><strong>ยอดรวมบิล:</strong> {selectedTable.currentBillAmount?.toFixed(2) || '0.00'} บ.</div>
                    {selectedTable.seatedAt && (
                      <div><strong>เริ่มนั่งเมื่อ:</strong> {new Date(selectedTable.seatedAt).toLocaleTimeString('th-TH')}</div>
                    )}
                  </div>
                  <button
                    type="button"
                    disabled={isSubmittingAction}
                    onClick={() => handleToggleOccupied(0)}
                    style={{
                      marginTop: '6px',
                      backgroundColor: '#F1F5F9',
                      color: '#334155',
                      border: '1px solid #CBD5E1',
                      padding: '8px',
                      borderRadius: '5px',
                      fontWeight: 'bold',
                      fontSize: '12.5px',
                      cursor: 'pointer',
                    }}
                  >
                    [ สลับเป็นโต๊ะว่าง (เคลียร์โต๊ะ) ]
                  </button>
                </div>
              )}

              {/* Danger Zone: Delete Table */}
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: '10px' }}>
                <button
                  type="button"
                  onClick={() => handleDeleteTable(selectedTable.id, selectedTable.tableNumber)}
                  style={{
                    width: '100%',
                    backgroundColor: '#FFF1F2',
                    color: '#BE123C',
                    border: '1px solid #FECDD3',
                    padding: '6px',
                    borderRadius: '4px',
                    fontSize: '11.5px',
                    fontWeight: 'bold',
                    cursor: 'pointer',
                  }}
                >
                  ลบโต๊ะ {selectedTable.tableNumber} ออกจากระบบ
                </button>
              </div>
            </>
          ) : (
            <div style={{ textAlign: 'center', padding: '40px 10px', color: '#64748B', fontSize: '13px' }}>
              คลิกเลือกโต๊ะในผังเพื่อจัดการการจอง หรือสลับสถานะ
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
