import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';

interface UserScopedRecord {
  key: string;
  userId: string;
  expiresAt: number;
  value: unknown;
}

@Injectable({ providedIn: 'root' })
export class IndexedDbService {
  private readonly platformId = inject(PLATFORM_ID);
  private databasePromise: Promise<IDBDatabase> | undefined;

  async putUserRecord(record: UserScopedRecord): Promise<void> {
    const database = await this.open();
    await this.complete(
      database.transaction('user-data', 'readwrite').objectStore('user-data').put(record),
    );
  }

  async getUserRecord<T>(key: string, userId: string): Promise<T | null> {
    const database = await this.open();
    const request = database
      .transaction('user-data', 'readonly')
      .objectStore('user-data')
      .get(key) as IDBRequest<UserScopedRecord | undefined>;
    const record = await this.request(request);
    if (record?.userId !== userId || record.expiresAt <= Date.now()) {
      if (record?.userId === userId) await this.deleteUserRecord(key);
      return null;
    }
    return record.value as T;
  }

  async deleteUserRecord(key: string): Promise<void> {
    const database = await this.open();
    await this.complete(
      database.transaction('user-data', 'readwrite').objectStore('user-data').delete(key),
    );
  }

  async purgeUser(userId: string): Promise<void> {
    const database = await this.open();
    const transaction = database.transaction('user-data', 'readwrite');
    const index = transaction.objectStore('user-data').index('userId');
    const keys = await this.request<IDBValidKey[]>(index.getAllKeys(userId));
    for (const key of keys) transaction.objectStore('user-data').delete(key);
    await this.transactionComplete(transaction);
  }

  private open(): Promise<IDBDatabase> {
    if (!isPlatformBrowser(this.platformId) || !('indexedDB' in globalThis)) {
      return Promise.reject(new Error('IndexedDB is unavailable in this rendering context.'));
    }
    this.databasePromise ??= new Promise((resolve, reject) => {
      const request = indexedDB.open('dorosak', 1);
      request.onupgradeneeded = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains('user-data')) {
          const store = database.createObjectStore('user-data', { keyPath: 'key' });
          store.createIndex('userId', 'userId', { unique: false });
          store.createIndex('expiresAt', 'expiresAt', { unique: false });
        }
      };
      request.onsuccess = () => {
        resolve(request.result);
      };
      request.onerror = () => {
        reject(request.error ?? new Error('IndexedDB could not be opened.'));
      };
    });
    return this.databasePromise;
  }

  private complete(request: IDBRequest): Promise<void> {
    return new Promise((resolve, reject) => {
      request.onsuccess = () => {
        resolve();
      };
      request.onerror = () => {
        reject(request.error ?? new Error('IndexedDB write failed.'));
      };
    });
  }

  private request<T>(request: IDBRequest<T>): Promise<T> {
    return new Promise((resolve, reject) => {
      request.onsuccess = () => {
        resolve(request.result);
      };
      request.onerror = () => {
        reject(request.error ?? new Error('IndexedDB request failed.'));
      };
    });
  }

  private transactionComplete(transaction: IDBTransaction): Promise<void> {
    return new Promise((resolve, reject) => {
      transaction.oncomplete = () => {
        resolve();
      };
      transaction.onerror = () => {
        reject(transaction.error ?? new Error('IndexedDB transaction failed.'));
      };
      transaction.onabort = () => {
        reject(transaction.error ?? new Error('IndexedDB transaction was aborted.'));
      };
    });
  }
}
