// Centralized client logger and error reporting service

const API_BASE_KEY = 'restaurant_pos_server_url';
export function getServerUrl(): string {
  const stored = localStorage.getItem(API_BASE_KEY);
  if (typeof window !== 'undefined' && window.location && window.location.origin) {
    const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
    if (!isLocal && stored && (stored.includes('localhost') || stored.includes('127.0.0.1'))) {
      localStorage.removeItem(API_BASE_KEY);
      return window.location.origin;
    }
    if (!window.location.origin.includes(':5173') && !stored) {
      return window.location.origin;
    }
  }
  if (stored) return stored;
  if (typeof window !== 'undefined' && window.location && window.location.origin && !window.location.origin.includes(':5173')) {
    return window.location.origin;
  }
  return 'http://localhost:5000';
}

export function setServerUrl(url: string): void {
  localStorage.setItem(API_BASE_KEY, url.replace(/\/$/, ''));
}

export interface ClientLogPayload {
  level: number; // 0=Debug, 1=Info, 2=Warn, 3=Error, 4=Fatal
  source: string;
  message: string;
  stackTrace?: string;
  details?: string;
  deviceInfo?: string;
  userId?: string;
}

export function logError(message: string, error?: any, source: string = 'Web-Client') {
  console.error(`[${source}]`, message, error);

  const payload: ClientLogPayload = {
    level: 3,
    source,
    message,
    stackTrace: error?.stack || String(error),
    details: typeof error === 'object' ? JSON.stringify(error) : String(error),
    deviceInfo: `UserAgent: ${navigator.userAgent} | Screen: ${window.innerWidth}x${window.innerHeight}`,
    userId: 'WebUser'
  };

  sendToServer(payload);
}

export function logInfo(message: string, source: string = 'Web-Client') {
  console.log(`[${source}]`, message);
}

function sendToServer(payload: ClientLogPayload) {
  try {
    fetch(`${getServerUrl()}/api/logs/client`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    }).catch(() => {
      // Ignore network fail for logger
    });
  } catch {
    // Ignore logger failure
  }
}

// Global unhandled error handlers
export function initGlobalErrorHandlers() {
  window.onerror = (message, source, lineno, colno, error) => {
    logError(`Unhandled Window Error: ${message} at ${source}:${lineno}:${colno}`, error, 'Web-Global');
    return false;
  };

  window.onunhandledrejection = (event) => {
    logError(`Unhandled Promise Rejection: ${event.reason}`, event.reason, 'Web-Promise');
  };
}
