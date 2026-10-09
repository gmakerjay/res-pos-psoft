import { useState, useEffect } from 'react';
import {
  getServerDiagnostics,
  devForceSync,
  devClearCache,
  getAllTenantsSummary,
  type ServerDiagnostics,
  type TenantSummaryItem
} from '../services/api';
import { logError } from '../services/logger';

export function DevActionPanelView() {
  const [diagnostics, setDiagnostics] = useState<ServerDiagnostics | null>(null);
  const [tenants, setTenants] = useState<TenantSummaryItem[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isActing, setIsActing] = useState<boolean>(false);
  const [actionMessage, setActionMessage] = useState<string>('');
  const [targetStore, setTargetStore] = useState<string>('ALL');
  const [lastRefreshed, setLastRefreshed] = useState<string>('');
  const [autoRefresh, setAutoRefresh] = useState<boolean>(false);

  const loadData = async (isBackground: boolean = false) => {
    if (!isBackground) setIsLoading(true);
    try {
      const [diagData, tenantsData] = await Promise.all([
        getServerDiagnostics().catch(err => {
          logError('Failed to fetch diagnostics: ' + err.message, err);
          return null;
        }),
        getAllTenantsSummary().catch(err => {
          logError('Failed to fetch tenants: ' + err.message, err);
          return [];
        })
      ]);

      if (diagData) setDiagnostics(diagData);
      if (tenantsData) setTenants(tenantsData);

      const now = new Date();
      setLastRefreshed(now.toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
    } catch (err: any) {
      logError('DevActionPanelView load error: ' + err.message, err);
    } finally {
      if (!isBackground) setIsLoading(false);
    }
  };

  useEffect(() => {
    loadData(false);
  }, []);

  useEffect(() => {
    if (!autoRefresh) return;
    const timer = setInterval(() => {
      loadData(true);
    }, 10000);
    return () => clearInterval(timer);
  }, [autoRefresh]);

  const handleForceSync = async (storeCode?: string) => {
    const target = storeCode || targetStore;
    const confirmText = target === 'ALL'
      ? 'คุณต้องการส่งคำสั่ง Force Sync ไปยังทุกจุดขาย (WPF POS, Web Hub, KDS) ทั่วระบบหรือไม่?'
      : `คุณต้องการส่งคำสั่ง Force Sync ไปยังร้านรหัส ${target} หรือไม่?`;

    if (!window.confirm(confirmText)) return;

    setIsActing(true);
    try {
      await devForceSync(target);
      setActionMessage(`[สำเร็จ] ส่งคำสั่ง Force Sync ไปยัง ${target} เรียบร้อย (${new Date().toLocaleTimeString('th-TH')})`);
      setTimeout(() => setActionMessage(''), 5000);
      loadData(true);
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการ Force Sync: ' + err.message);
    } finally {
      setIsActing(false);
    }
  };

  const handleClearCache = async () => {
    if (!window.confirm('คุณต้องการล้างแคชหน่วยความจำของเซิร์ฟเวอร์กลางหรือไม่?')) return;

    setIsActing(true);
    try {
      await devClearCache();
      setActionMessage(`[สำเร็จ] ล้างแคชระบบเรียบร้อย (${new Date().toLocaleTimeString('th-TH')})`);
      setTimeout(() => setActionMessage(''), 5000);
      loadData(true);
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการล้างแคช: ' + err.message);
    } finally {
      setIsActing(false);
    }
  };

  const handlePing = async () => {
    setIsActing(true);
    try {
      await loadData(false);
      setActionMessage(`[สำเร็จ] Ping สำเร็จ - ข้อมูลสถานะและระบบเป็นปัจจุบัน (${new Date().toLocaleTimeString('th-TH')})`);
      setTimeout(() => setActionMessage(''), 5000);
    } catch (err: any) {
      alert('เกิดข้อผิดพลาดในการ Ping: ' + err.message);
    } finally {
      setIsActing(false);
    }
  };

  const formatDbSize = (kb: number) => {
    if (kb >= 1024) {
      return `${(kb / 1024).toFixed(2)} MB`;
    }
    return `${kb} KB`;
  };

  return (
    <div style={{ padding: '16px', background: '#f0f2f5', minHeight: '100%', fontFamily: 'Tahoma, Segoe UI, sans-serif' }}>
      {/* Top Header Card */}
      <div style={{
        background: '#fff',
        border: '1px solid #c0c0c0',
        borderRadius: '4px',
        padding: '16px 20px',
        marginBottom: '16px',
        boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '12px'
      }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{
              background: '#003366',
              color: '#fff',
              fontSize: '11px',
              fontWeight: 'bold',
              padding: '2px 8px',
              borderRadius: '2px'
            }}>
              [DEV CONSOLE]
            </span>
            <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 'bold', color: '#1a1a1a' }}>
              Action Panel สำหรับผู้ดูแลระบบและวิศวกรรม (DEV)
            </h2>
          </div>
          <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: '#666' }}>
            ตรวจสอบสถานะเซิร์ฟเวอร์กลาง จัดการซิงค์ข้อมูล Real-time ข้ามไคลเอนต์ POS/Web และวิเคราะห์ฐานข้อมูลแต่ละสาขา
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '12px', color: '#444', display: 'flex', alignItems: 'center', gap: '4px', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={e => setAutoRefresh(e.target.checked)}
            />
            ออโต้รีเฟรช (10s)
          </label>
          <button
            onClick={() => loadData(false)}
            disabled={isLoading || isActing}
            style={{
              background: '#e1e1e1',
              border: '1px solid #707070',
              padding: '4px 12px',
              fontSize: '12px',
              cursor: (isLoading || isActing) ? 'not-allowed' : 'pointer'
            }}
          >
            [ รีเฟรชข้อมูล ]
          </button>
          {lastRefreshed && (
            <span style={{ fontSize: '11px', color: '#777' }}>
              อัปเดตล่าสุด: {lastRefreshed}
            </span>
          )}
        </div>
      </div>

      {/* Action Notification Banner */}
      {actionMessage && (
        <div style={{
          background: '#d4edda',
          color: '#155724',
          border: '1px solid #c3e6cb',
          borderRadius: '4px',
          padding: '10px 16px',
          marginBottom: '16px',
          fontSize: '13px',
          fontWeight: 'bold'
        }}>
          {actionMessage}
        </div>
      )}

      {/* Diagnostics Cards Grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
        gap: '12px',
        marginBottom: '16px'
      }}>
        {/* Card 1: Platform Architecture */}
        <div style={{
          background: '#fff',
          border: '1px solid #c0c0c0',
          borderRadius: '4px',
          padding: '14px',
          boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
        }}>
          <div style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>โหมดการทำงานของระบบ (SERVER MODE)</div>
          <div style={{ marginTop: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{
              background: diagnostics?.serverMode === 'Standalone' ? '#6f42c1' : '#0056b3',
              color: '#fff',
              fontSize: '12px',
              fontWeight: 'bold',
              padding: '3px 8px',
              borderRadius: '2px'
            }}>
              [{diagnostics?.serverMode === 'Standalone' ? 'Standalone (Turnkey ขายเป็นโปรเจกต์)' : 'Platform (Cloud SaaS กลาง)'}]
            </span>
          </div>
          <div style={{ marginTop: '6px', fontSize: '12px', color: '#444' }}>
            การรับสมัครร้านค้า: {' '}
            <strong style={{ color: diagnostics?.allowRegistration ? '#28a745' : '#dc3545' }}>
              [{diagnostics?.allowRegistration ? 'เปิดรับสมัคร' : 'ปิดรับสมัคร - ล็อคร้านค้า'}]
            </strong>
          </div>
          {diagnostics?.serverMode === 'Standalone' && (
            <div style={{ marginTop: '4px', fontSize: '11px', color: '#666' }}>
              รหัส RPOS เฉพาะร้าน: <strong>{diagnostics?.standaloneRPOSCode || '-'}</strong>
            </div>
          )}
        </div>

        {/* Card 2: Server Health */}
        <div style={{
          background: '#fff',
          border: '1px solid #c0c0c0',
          borderRadius: '4px',
          padding: '14px',
          boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
        }}>
          <div style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>สถานะเซิร์ฟเวอร์ & UPTIME</div>
          <div style={{ fontSize: '20px', fontWeight: 'bold', color: '#1a1a1a', marginTop: '6px' }}>
            {diagnostics?.uptime || 'กำลังตรวจสอบ...'}
          </div>
          <div style={{ marginTop: '4px', fontSize: '12px', color: '#555' }}>
            การใช้แรม (Memory): <strong>{diagnostics ? `${diagnostics.memoryUsageMb.toFixed(1)} MB` : '-'}</strong>
          </div>
          <div style={{ marginTop: '2px', fontSize: '11px', color: '#777' }}>
            OS: {diagnostics?.osVersion || '-'}
          </div>
        </div>

        {/* Card 3: Active Connections */}
        <div style={{
          background: '#fff',
          border: '1px solid #c0c0c0',
          borderRadius: '4px',
          padding: '14px',
          boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
        }}>
          <div style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>การเชื่อมต่อสด (REAL-TIME SESSIONS)</div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '6px' }}>
            <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#28a745' }}>
              {diagnostics?.activeSessionsCount ?? 0}
            </span>
            <span style={{ fontSize: '12px', color: '#666' }}>เซสชันทั้งหมดที่ออนไลน์</span>
          </div>
          <div style={{ marginTop: '4px', fontSize: '12px', color: '#444' }}>
            จุดขาย WPF POS หน้าร้าน: <strong style={{ color: '#0056b3' }}>{diagnostics?.activePosTerminalsCount ?? 0} เครื่อง</strong>
          </div>
          <div style={{ marginTop: '2px', fontSize: '11px', color: '#777' }}>
            ร้านค้าทั้งหมดในระบบ: {diagnostics?.totalTenants ?? tenants.length} สาขา
          </div>
        </div>

        {/* Card 4: Runtime Version */}
        <div style={{
          background: '#fff',
          border: '1px solid #c0c0c0',
          borderRadius: '4px',
          padding: '14px',
          boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
        }}>
          <div style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>สภาพแวดล้อมรันไทม์ (ENVIRONMENT)</div>
          <div style={{ marginTop: '8px', fontSize: '12px', color: '#333' }}>
            Runtime: <strong>{diagnostics?.dotNetVersion || '.NET 10.0'}</strong>
          </div>
          <div style={{ marginTop: '4px', fontSize: '11px', color: '#555' }}>
            เวลาเซิร์ฟเวอร์ (Local): <strong>{diagnostics?.serverTimeLocal ? new Date(diagnostics.serverTimeLocal).toLocaleTimeString('th-TH') : '-'}</strong>
          </div>
          <div style={{ marginTop: '2px', fontSize: '10px', color: '#888' }}>
            SignalR Hub: <code>/posHub</code> (Transport: WebSockets)
          </div>
        </div>
      </div>

      {/* Action Control Panel */}
      <div style={{
        background: '#fff',
        border: '1px solid #c0c0c0',
        borderRadius: '4px',
        padding: '16px 20px',
        marginBottom: '16px',
        boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
          <span style={{
            background: '#ffc107',
            color: '#000',
            fontSize: '11px',
            fontWeight: 'bold',
            padding: '2px 6px',
            borderRadius: '2px'
          }}>
            [คำสั่งด่วน]
          </span>
          <h3 style={{ margin: 0, fontSize: '14px', fontWeight: 'bold', color: '#222' }}>
            DEV Action Controls (ส่งสัญญาณและบำรุงรักษาระบบ)
          </h3>
        </div>

        <div style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '12px',
          alignItems: 'center',
          background: '#f8f9fa',
          padding: '12px',
          borderRadius: '4px',
          border: '1px solid #e2e8f0'
        }}>
          {/* Target Store Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ fontSize: '12px', fontWeight: 'bold', color: '#444' }}>เป้าหมาย:</span>
            <select
              value={targetStore}
              onChange={e => setTargetStore(e.target.value)}
              style={{
                fontSize: '12px',
                padding: '4px 8px',
                border: '1px solid #aaa',
                borderRadius: '3px',
                background: '#fff'
              }}
            >
              <option value="ALL">-- ทุกร้านค้าในระบบ (Broadcast ALL) --</option>
              {tenants.map(t => (
                <option key={t.storeCode} value={t.storeCode}>
                  [{t.storeCode}] {t.storeName}
                </option>
              ))}
            </select>
          </div>

          {/* Action 1: Force Sync */}
          <button
            onClick={() => handleForceSync()}
            disabled={isActing}
            style={{
              background: '#0056b3',
              color: '#fff',
              border: '1px solid #004085',
              padding: '6px 14px',
              fontSize: '12px',
              fontWeight: 'bold',
              borderRadius: '3px',
              cursor: isActing ? 'not-allowed' : 'pointer'
            }}
          >
            [ ส่งสัญญาณบังคับซิงค์สด (Force Sync) ]
          </button>

          {/* Action 2: Ping */}
          <button
            onClick={handlePing}
            disabled={isActing}
            style={{
              background: '#28a745',
              color: '#fff',
              border: '1px solid #1e7e34',
              padding: '6px 14px',
              fontSize: '12px',
              fontWeight: 'bold',
              borderRadius: '3px',
              cursor: isActing ? 'not-allowed' : 'pointer'
            }}
          >
            [ ทดสอบการเชื่อมต่อ (Ping Diagnostics) ]
          </button>

          {/* Action 3: Clear Cache */}
          <button
            onClick={handleClearCache}
            disabled={isActing}
            style={{
              background: '#dc3545',
              color: '#fff',
              border: '1px solid #bd2130',
              padding: '6px 14px',
              fontSize: '12px',
              fontWeight: 'bold',
              borderRadius: '3px',
              cursor: isActing ? 'not-allowed' : 'pointer'
            }}
          >
            [ ล้างแคชระบบ (Clear Cache) ]
          </button>
        </div>
        <div style={{ marginTop: '8px', fontSize: '11px', color: '#666' }}>
          * หมายเหตุ: การกด Force Sync จะส่งคำสั่ง SignalR ไปยังทุกหน้าจอที่เชื่อมต่ออยู่ เพื่อดึงข้อมูลสินค้า, โต๊ะ, ออเดอร์ และรายงานใหม่ทันทีโดยไม่ต้องรีโหลดหน้าเว็บ
        </div>
      </div>

      {/* Tenants Database Summary Table */}
      <div style={{
        background: '#fff',
        border: '1px solid #c0c0c0',
        borderRadius: '4px',
        padding: '16px',
        boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '14px', fontWeight: 'bold', color: '#222' }}>
              รายงานสาขาและฐานข้อมูลร้านค้า (Tenants Database Inspector)
            </h3>
            <span style={{ fontSize: '11px', color: '#666' }}>
              ข้อมูลปริมาณรายการ ขนาดฐานข้อมูล SQLite รายสาขา และสถานะจุดขายออนไลน์
            </span>
          </div>
          <span style={{
            background: '#e9ecef',
            padding: '2px 8px',
            fontSize: '12px',
            borderRadius: '2px',
            fontWeight: 'bold',
            color: '#495057'
          }}>
            รวมทั้งหมด: {tenants.length} สาขา
          </span>
        </div>

        {isLoading ? (
          <div style={{ padding: '30px', textAlign: 'center', color: '#777', fontSize: '13px' }}>
            กำลังโหลดข้อมูลฐานข้อมูลร้านค้า...
          </div>
        ) : tenants.length === 0 ? (
          <div style={{ padding: '30px', textAlign: 'center', color: '#777', fontSize: '13px' }}>
            ไม่พบข้อมูลร้านค้าในระบบ
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{
              width: '100%',
              borderCollapse: 'collapse',
              fontSize: '12px',
              fontFamily: 'Tahoma, Segoe UI, sans-serif'
            }}>
              <thead>
                <tr style={{ background: '#003366', color: '#fff', textAlign: 'left' }}>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244' }}>รหัส RPOS</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244' }}>ชื่อร้านค้า</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244' }}>เจ้าของ / โทร</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244' }}>แพ็กเกจ</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'center' }}>โต๊ะ</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'center' }}>เมนู</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'center' }}>ออเดอร์</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'right' }}>ขนาด DB</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'center' }}>สถานะสด</th>
                  <th style={{ padding: '8px 10px', border: '1px solid #002244', textAlign: 'center' }}>คำสั่ง</th>
                </tr>
              </thead>
              <tbody>
                {tenants.map((item, index) => (
                  <tr
                    key={item.storeCode}
                    style={{
                      background: index % 2 === 0 ? '#fff' : '#f9f9f9',
                      borderBottom: '1px solid #e0e0e0'
                    }}
                  >
                    <td style={{ padding: '8px 10px', fontWeight: 'bold', color: '#0056b3', border: '1px solid #e0e0e0' }}>
                      {item.storeCode}
                    </td>
                    <td style={{ padding: '8px 10px', border: '1px solid #e0e0e0' }}>
                      <div style={{ fontWeight: 'bold', color: '#222' }}>{item.storeName}</div>
                      <div style={{ fontSize: '10px', color: '#777' }}>สร้างเมื่อ: {item.createdAt ? new Date(item.createdAt).toLocaleDateString('th-TH') : '-'}</div>
                    </td>
                    <td style={{ padding: '8px 10px', border: '1px solid #e0e0e0' }}>
                      <div>{item.ownerName || '-'}</div>
                      <div style={{ fontSize: '11px', color: '#666' }}>{item.ownerPhone || '-'}</div>
                    </td>
                    <td style={{ padding: '8px 10px', border: '1px solid #e0e0e0' }}>
                      <span style={{
                        background: '#e2e8f0',
                        color: '#334155',
                        fontSize: '11px',
                        padding: '2px 6px',
                        borderRadius: '2px',
                        fontWeight: 'bold'
                      }}>
                        {item.subscriptionPlan || 'FREE'}
                      </span>
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'center', border: '1px solid #e0e0e0' }}>
                      {item.tables}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'center', border: '1px solid #e0e0e0' }}>
                      {item.products}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'center', fontWeight: 'bold', border: '1px solid #e0e0e0' }}>
                      {item.orders}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 'bold', color: '#444', border: '1px solid #e0e0e0' }}>
                      {formatDbSize(item.dbSizeKb)}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'center', border: '1px solid #e0e0e0' }}>
                      {item.isOnline ? (
                        <span style={{
                          background: '#28a745',
                          color: '#fff',
                          fontSize: '10px',
                          fontWeight: 'bold',
                          padding: '2px 6px',
                          borderRadius: '2px'
                        }}>
                          [ออนไลน์: {item.activeTerminals} จุด]
                        </span>
                      ) : (
                        <span style={{
                          background: '#6c757d',
                          color: '#fff',
                          fontSize: '10px',
                          padding: '2px 6px',
                          borderRadius: '2px'
                        }}>
                          [ออฟไลน์]
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'center', border: '1px solid #e0e0e0' }}>
                      <button
                        onClick={() => handleForceSync(item.storeCode)}
                        disabled={isActing}
                        style={{
                          background: '#0056b3',
                          color: '#fff',
                          border: 'none',
                          padding: '3px 8px',
                          fontSize: '11px',
                          borderRadius: '2px',
                          cursor: isActing ? 'not-allowed' : 'pointer'
                        }}
                      >
                        [ Force Sync ]
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
