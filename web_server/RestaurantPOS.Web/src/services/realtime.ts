import * as signalR from '@microsoft/signalr';
import { getServerUrl, logError, logInfo } from './logger';
import { getStoredTenantCode, type Order, type TableItem } from './api';

export type ConnectionState = 'connected' | 'reconnecting' | 'disconnected';

export interface StoreStatusEventData {
  storeCode: string;
  isOnline: boolean;
  activePosCount: number;
  message?: string;
  timestamp?: string;
}

export class RealtimeService {
  private hub: signalR.HubConnection | null = null;
  private stateListeners: ((state: ConnectionState) => void)[] = [];
  private orderCreatedListeners: ((order: Order) => void)[] = [];
  private orderStatusListeners: ((order: Order) => void)[] = [];
  private tableStatusListeners: ((table: TableItem) => void)[] = [];
  private billClosedListeners: ((order: Order) => void)[] = [];
  private menuUpdatedListeners: ((data: any) => void)[] = [];
  private categoryUpdatedListeners: ((data: any) => void)[] = [];
  private ingredientUpdatedListeners: ((data: any) => void)[] = [];
  private storeStatusListeners: ((data: StoreStatusEventData) => void)[] = [];

  public start() {
    if (this.hub) return;

    const tenant = getStoredTenantCode();
    const hubUrl = `${getServerUrl()}/hubs/pos?tenant=${encodeURIComponent(tenant)}`;
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hub.onreconnecting((error) => {
      logInfo(`SignalR reconnecting: ${error?.message}`, 'Web-SignalR');
      this.notifyState('reconnecting');
    });

    this.hub.onreconnected((connectionId) => {
      logInfo(`SignalR reconnected: ${connectionId}`, 'Web-SignalR');
      this.notifyState('connected');
    });

    this.hub.onclose((error) => {
      logInfo(`SignalR closed: ${error?.message}`, 'Web-SignalR');
      this.notifyState('disconnected');
    });

    this.hub.on('OrderCreated', (order: Order) => {
      this.orderCreatedListeners.forEach(cb => cb(order));
    });

    this.hub.on('OrderStatusChanged', (order: Order) => {
      this.orderStatusListeners.forEach(cb => cb(order));
    });

    this.hub.on('TableStatusChanged', (table: TableItem) => {
      this.tableStatusListeners.forEach(cb => cb(table));
    });

    this.hub.on('BillClosed', (order: Order) => {
      this.billClosedListeners.forEach(cb => cb(order));
    });

    this.hub.on('MenuUpdated', (data: any) => {
      this.menuUpdatedListeners.forEach(cb => cb(data));
    });

    this.hub.on('CategoryUpdated', (data: any) => {
      this.categoryUpdatedListeners.forEach(cb => cb(data));
    });

    this.hub.on('IngredientUpdated', (data: any) => {
      this.ingredientUpdatedListeners.forEach(cb => cb(data));
    });

    this.hub.on('StoreStatusChanged', (data: StoreStatusEventData) => {
      logInfo(`Store status changed for ${data.storeCode}: Online=${data.isOnline}, Terminals=${data.activePosCount}`, 'Web-SignalR');
      this.storeStatusListeners.forEach(cb => cb(data));
    });

    this.hub.start()
      .then(() => {
        logInfo('SignalR connected to ' + hubUrl, 'Web-SignalR');
        this.notifyState('connected');
      })
      .catch((err) => {
        logError('SignalR failed to start: ' + err.message, err, 'Web-SignalR');
        this.notifyState('disconnected');
      });
  }

  public stop() {
    if (this.hub) {
      this.hub.stop();
      this.hub = null;
      this.notifyState('disconnected');
    }
  }

  public onStateChange(listener: (state: ConnectionState) => void) {
    this.stateListeners.push(listener);
    return () => {
      this.stateListeners = this.stateListeners.filter(l => l !== listener);
    };
  }

  public onOrderCreated(cb: (order: Order) => void) {
    this.orderCreatedListeners.push(cb);
    return () => {
      this.orderCreatedListeners = this.orderCreatedListeners.filter(l => l !== cb);
    };
  }

  public onOrderStatusChanged(cb: (order: Order) => void) {
    this.orderStatusListeners.push(cb);
    return () => {
      this.orderStatusListeners = this.orderStatusListeners.filter(l => l !== cb);
    };
  }

  public onTableStatusChanged(cb: (table: TableItem) => void) {
    this.tableStatusListeners.push(cb);
    return () => {
      this.tableStatusListeners = this.tableStatusListeners.filter(l => l !== cb);
    };
  }

  public onBillClosed(cb: (order: Order) => void) {
    this.billClosedListeners.push(cb);
    return () => {
      this.billClosedListeners = this.billClosedListeners.filter(l => l !== cb);
    };
  }

  public onMenuUpdated(cb: (data: any) => void) {
    this.menuUpdatedListeners.push(cb);
    return () => {
      this.menuUpdatedListeners = this.menuUpdatedListeners.filter(l => l !== cb);
    };
  }

  public onCategoryUpdated(cb: (data: any) => void) {
    this.categoryUpdatedListeners.push(cb);
    return () => {
      this.categoryUpdatedListeners = this.categoryUpdatedListeners.filter(l => l !== cb);
    };
  }

  public onIngredientUpdated(cb: (data: any) => void) {
    this.ingredientUpdatedListeners.push(cb);
    return () => {
      this.ingredientUpdatedListeners = this.ingredientUpdatedListeners.filter(l => l !== cb);
    };
  }

  public onStoreStatusChanged(cb: (data: StoreStatusEventData) => void) {
    this.storeStatusListeners.push(cb);
    return () => {
      this.storeStatusListeners = this.storeStatusListeners.filter(l => l !== cb);
    };
  }

  public async restart() {
    if (this.hub) {
      try {
        await this.hub.stop();
      } catch { }
      this.hub = null;
    }
    this.start();
  }

  private notifyState(state: ConnectionState) {
    this.stateListeners.forEach(cb => cb(state));
  }
}

export const realtimeService = new RealtimeService();
