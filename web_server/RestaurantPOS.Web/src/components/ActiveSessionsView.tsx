import { useState, useEffect } from 'react';
import { getActiveSessions, kickSession, getStoredTenantCode, type ActiveSessionItem } from '../services/api';
import { logError } from '../services/logger';

export function ActiveSessionsView() {
  const [sessions, setSessions] = useState<ActiveSessionItem[]>([]);
  const [filterStore, setFilterStore] = useState<string>('ALL');
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [autoRefresh, setAutoRefresh] = useState<boolean>(true);
  const [lastRefreshed, setLastRefreshed] = useState<string>('');
  const [actionMessage, setActionMessage] = useState<string>('');

  const currentStore = getStoredTenantCode() || 'DEFAULT';

  const loadSessions = async (isBackground: boolean = false) => {
    if (!isBackground) setIsLoading(true);
    try {
      const data = await getActiveSessions(filterStore === 'ALL' ? undefined : filterStore);
      setSessions(data || []);
      const now = new Date();
      setLastRefreshed(now.toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
    } catch (err: any) {
      logError('Failed to load active sessions: ' + err.message, err);
    } finally {
      if (!isBackground) setIsLoading(false);
    }
  };

  useEffect(() => {
    loadSessions(false);
  }, [filterStore]);

  useEffect(() => {
    if (!autoRefresh) return;
    const timer = setInterval(() => {
      loadSessions(true);
    }, 5000);
    return () => clearInterval(timer);
  }, [autoRefresh, filterStore]);

  const handleKick = async (connId: string, clientName: string) => {
    if (!window.confirm(`คุณแน่ใจหรือไม่ว่าต้องการตัดการเชื่อมต่อเซสชัน: ${clientName}?`)) {
      return;
    }

    try {
      await kickSession(connId, 'Admin terminated connection');
      setActionMessage(`[สำเร็จ] ตัดการเชื่อมต่อเซสชัน ${connId.slice(0, 8)} เรียบร้อย`);
      setTimeout(() => setActionMessage(''), 4000);
      loadSessions(true);
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการตัดการเชื่อมต่อ: ' + err.message);
    }
  };

  const posCount = sessions.filter(s => s.clientType.toLowerCase().includes('pos')).length;
  const webCount = sessions.length - posCount;

  const getClientBadgeStyle = (type: string) => {
    const t = type.toLowerCase();
    if (t.includes('pos')) {
      return { bg: '#E3F2FD', color: '#1565C0', border: '#90CAF9', text: '[Windows POS]' };
    }
    if (t.includes('kds') || t.includes('ครัว')) {
      return { bg: '#FFF3E0', color: '#E65100', border: '#FFB74D', text: '[จอครัว KDS]' };
    }
    if (t.includes('cashier') || t.includes('แคชเชียร์')) {
      return { bg: '#E8F5E9', color: '#2E7D32', border: '#A5D6A7', text: '[Web แคชเชียร์]' };
    }
    if (t.includes('qr') || t.includes('table') || t.includes('ลูกค้า')) {
      return { bg: '#F3E5F5', color: '#7B1FA2', border: '#CE93D8', text: '[ลูกค้า QR โต๊ะ]' };
    }
    return { bg: '#ECEFF1', color: '#455A64', border: '#B0BEC5', text: `[${type}]` };
  };

  return (
    <div style={{ backgroundColor: '#FFFFFF', borderRadius: '8px', border: '1px solid #CFD8DC', padding: '20px' }}>
      {/* Header Bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px', flexWrap: 'wrap', gap: '10px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 'bold', color: '#263238', margin: 0 }}>
              ผู้ใช้งานและอุปกรณ์ที่กำลังออนไลน์สด (Live Active Sessions)
            </h3>
            <span style={{
              backgroundColor: '#DCFCE7',
              color: '#15803D',
              border: '1px solid #86EFAC',
              fontSize: '11px',
              padding: '2px 8px',
              borderRadius: '12px',
              fontWeight: 'bold'
            }}>
              [สด Real-Time]
            </span>
          </div>
          <div style={{ fontSize: '12px', color: '#78909C', marginTop: '4px' }}>
            ติดตามการเชื่อมต่อเครื่องคอมพิวเตอร์ Windows POS, จอครัว, แคชเชียร์ และลูกค้าที่กำลังใช้งานระบบทุกสาขา
          </div>
        </div>

        {/* Filter & Refresh Controls */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
          <select
            value={filterStore}
            onChange={(e) => setFilterStore(e.target.value)}
            style={{
              padding: '6px 12px',
              borderRadius: '4px',
              border: '1.5px solid #90CAF9',
              backgroundColor: '#F0F7FF',
              color: '#0D47A1',
              fontSize: '12.5px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            <option value="ALL">ดูทุกร้านค้า (All Stores)</option>
            <option value={currentStore}>ดูเฉพาะร้านปัจจุบัน ({currentStore})</option>
          </select>

          <label style={{ fontSize: '12px', color: '#37474F', display: 'flex', alignItems: 'center', gap: '4px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(e) => setAutoRefresh(e.target.checked)}
            />
            รีเฟรชอัตโนมัติ (5 วิ)
          </label>

          <button
            type="button"
            onClick={() => loadSessions(false)}
            disabled={isLoading}
            style={{
              backgroundColor: '#1976D2',
              color: '#FFFFFF',
              border: 'none',
              padding: '6px 14px',
              borderRadius: '4px',
              fontSize: '12.5px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            {isLoading ? '[ กำลังโหลด... ]' : '[ รีเฟรชเดี๋ยวนี้ ]'}
          </button>
        </div>
      </div>

      {actionMessage && (
        <div style={{
          backgroundColor: '#E8F5E9',
          color: '#2E7D32',
          border: '1px solid #A5D6A7',
          padding: '8px 14px',
          borderRadius: '4px',
          fontSize: '12.5px',
          fontWeight: 'bold',
          marginBottom: '14px'
        }}>
          {actionMessage}
        </div>
      )}

      {/* Stats Counter Bar */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '12px', marginBottom: '20px' }}>
        <div style={{ backgroundColor: '#F8FAFC', padding: '14px', borderRadius: '6px', border: '1px solid #E2E8F0' }}>
          <div style={{ fontSize: '12px', color: '#64748B', fontWeight: 600 }}>เซสชันที่เชื่อมต่อทั้งหมด</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#0F172A', marginTop: '2px' }}>
            {sessions.length} <span style={{ fontSize: '12px', fontWeight: 'normal' }}>เครื่อง/จุด</span>
          </div>
        </div>

        <div style={{ backgroundColor: '#F0F9FF', padding: '14px', borderRadius: '6px', border: '1px solid #BAE6FD' }}>
          <div style={{ fontSize: '12px', color: '#0369A1', fontWeight: 600 }}>เครื่อง Windows POS หน้าร้าน</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#0284C7', marginTop: '2px' }}>
            {posCount} <span style={{ fontSize: '12px', fontWeight: 'normal' }}>เครื่อง</span>
          </div>
        </div>

        <div style={{ backgroundColor: '#FDF4FF', padding: '14px', borderRadius: '6px', border: '1px solid #F0ABFC' }}>
          <div style={{ fontSize: '12px', color: '#A21CAF', fontWeight: 600 }}>Web Clients (ครัว/แคชเชียร์/QR)</div>
          <div style={{ fontSize: '24px', fontWeight: 'bold', color: '#C026D3', marginTop: '2px' }}>
            {webCount} <span style={{ fontSize: '12px', fontWeight: 'normal' }}>จุด</span>
          </div>
        </div>

        <div style={{ backgroundColor: '#F0FDF4', padding: '14px', borderRadius: '6px', border: '1px solid #BBF7D0' }}>
          <div style={{ fontSize: '12px', color: '#15803D', fontWeight: 600 }}>อัปเดตข้อมูลล่าสุดเมื่อ</div>
          <div style={{ fontSize: '18px', fontWeight: 'bold', color: '#16A34A', marginTop: '6px' }}>
            {lastRefreshed || 'กำลังตรวจสอบ...'}
          </div>
        </div>
      </div>

      {/* Table of Active Sessions */}
      {sessions.length === 0 ? (
        <div style={{
          textAlign: 'center',
          padding: '48px 16px',
          backgroundColor: '#F8FAFC',
          borderRadius: '8px',
          border: '1px dashed #CBD5E1'
        }}>
          <div style={{ fontSize: '14px', fontWeight: 'bold', color: '#64748B' }}>
            [ไม่พบเซสชันที่กำลังเชื่อมต่ออยู่ในขณะนี้]
          </div>
          <div style={{ fontSize: '12px', color: '#94A3B8', marginTop: '6px' }}>
            เมื่อมีเครื่อง Windows POS หรือเบราว์เซอร์เปิดใช้งาน ข้อมูลจะปรากฏขึ้นที่นี่โดยอัตโนมัติ
          </div>
        </div>
      ) : (
        <div style={{ overflowX: 'auto', border: '1px solid #E2E8F0', borderRadius: '6px' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
            <thead>
              <tr style={{ backgroundColor: '#F1F5F9', borderBottom: '1.5px solid #CBD5E1', textAlign: 'left' }}>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>รหัสร้าน (RPOS Code)</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>ประเภทอุปกรณ์ / หน้าจอ</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>ผู้ใช้งาน / สิทธิ์</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>ชื่อเครื่อง / Browser</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>IP Address</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>เวลาเชื่อมต่อ</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155' }}>สถานะสัญญาณ</th>
                <th style={{ padding: '10px 14px', fontWeight: 'bold', color: '#334155', textAlign: 'center' }}>จัดการ</th>
              </tr>
            </thead>
            <tbody>
              {sessions.map((item, idx) => {
                const badge = getClientBadgeStyle(item.clientType);
                const connectedDate = new Date(item.connectedAt);
                const timeStr = connectedDate.toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit', second: '2-digit' });

                return (
                  <tr
                    key={item.connectionId || idx}
                    style={{
                      borderBottom: '1px solid #E2E8F0',
                      backgroundColor: idx % 2 === 0 ? '#FFFFFF' : '#F8FAFC'
                    }}
                  >
                    <td style={{ padding: '10px 14px', fontWeight: 'bold', color: '#0D47A1', fontFamily: 'Consolas, monospace' }}>
                      {item.storeCode}
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <span style={{
                        backgroundColor: badge.bg,
                        color: badge.color,
                        border: `1px solid ${badge.border}`,
                        padding: '2px 8px',
                        borderRadius: '4px',
                        fontSize: '11.5px',
                        fontWeight: 'bold',
                        whiteSpace: 'nowrap'
                      }}>
                        {badge.text}
                      </span>
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <div style={{ fontWeight: 'bold', color: '#1E293B' }}>{item.username}</div>
                      <div style={{ fontSize: '11px', color: '#64748B' }}>{item.role || 'Staff'}</div>
                    </td>
                    <td style={{ padding: '10px 14px', color: '#475569', maxWidth: '160px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={item.deviceName}>
                      {item.deviceName}
                    </td>
                    <td style={{ padding: '10px 14px', color: '#475569', fontFamily: 'Consolas, monospace', fontSize: '12px' }}>
                      {item.ipAddress}
                    </td>
                    <td style={{ padding: '10px 14px', color: '#475569', fontSize: '12px' }}>
                      {timeStr}
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <span style={{
                        backgroundColor: '#DCFCE7',
                        color: '#166534',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        fontSize: '11px',
                        fontWeight: 'bold'
                      }}>
                        [ออนไลน์สด]
                      </span>
                    </td>
                    <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                      <button
                        type="button"
                        onClick={() => handleKick(item.connectionId, `${item.clientType} (${item.username})`)}
                        style={{
                          backgroundColor: '#FEE2E2',
                          color: '#B91C1C',
                          border: '1px solid #FCA5A5',
                          padding: '3px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 'bold',
                          cursor: 'pointer'
                        }}
                        title="ตัดการเชื่อมต่อเซสชันนี้"
                      >
                        [ เตะออก ]
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
