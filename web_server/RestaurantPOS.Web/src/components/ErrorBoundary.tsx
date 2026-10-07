import { Component } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import { logError } from '../services/logger';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
    errorInfo: null
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error, errorInfo: null };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    this.setState({ errorInfo });
    logError(`React Error Boundary caught: ${error.message}`, error, 'React-ErrorBoundary');
  }

  private handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
    window.location.reload();
  };

  public render() {
    if (this.state.hasError) {
      return (
        <div style={{
          padding: '30px',
          margin: '30px auto',
          maxWidth: '600px',
          background: '#FFF8F8',
          border: '1px solid #FFCDD2',
          borderRadius: '8px',
          fontFamily: 'Segoe UI, Tahoma, sans-serif'
        }}>
          <h2 style={{ color: '#D32F2F', marginTop: 0 }}>เกิดข้อผิดพลาดในการแสดงผล</h2>
          <p style={{ color: '#555' }}>ระบบตรวจพบข้อผิดพลาดที่ไม่คาดคิด กรุณารีเฟรชหน้าเว็บหรือลองใหม่อีกครั้ง</p>
          <pre style={{
            background: '#F5F5F5',
            padding: '12px',
            borderRadius: '4px',
            fontSize: '12px',
            color: '#333',
            overflowX: 'auto'
          }}>
            {this.state.error?.message}
          </pre>
          <button
            onClick={this.handleReset}
            style={{
              background: '#245EDC',
              color: '#FFF',
              border: 'none',
              padding: '10px 20px',
              borderRadius: '4px',
              fontWeight: 'bold',
              cursor: 'pointer'
            }}
          >
            รีเฟรชหน้าเว็บ (Reload Page)
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
