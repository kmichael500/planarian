const DB_NAME = "planarian-import-queue";
const DB_VERSION = 1;
const FILE_STORE = "files";

interface StoredQueueFile {
  id: string;
  queueKey: string;
  itemId: string;
  file: File;
}

const buildStorageId = (queueKey: string, itemId: string) =>
  `${queueKey}::${itemId}`;

const openDatabase = (): Promise<IDBDatabase> =>
  new Promise((resolve, reject) => {
    const request = window.indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const database = request.result;
      if (!database.objectStoreNames.contains(FILE_STORE)) {
        database.createObjectStore(FILE_STORE, { keyPath: "id" });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () =>
      reject(request.error ?? new Error("Failed to open import queue storage."));
  });

const runTransaction = async <T>(
  mode: IDBTransactionMode,
  action: (store: IDBObjectStore, resolve: (value: T) => void, reject: (reason?: unknown) => void) => void
): Promise<T> => {
  const database = await openDatabase();

  return new Promise<T>((resolve, reject) => {
    const transaction = database.transaction(FILE_STORE, mode);
    const store = transaction.objectStore(FILE_STORE);

    transaction.oncomplete = () => database.close();
    transaction.onerror = () => {
      database.close();
      reject(
        transaction.error ?? new Error("Import queue storage transaction failed.")
      );
    };

    action(store, resolve, reject);
  });
};

export const ImportQueueStorage = {
  async putFile(queueKey: string, itemId: string, file: File): Promise<void> {
    await runTransaction<void>("readwrite", (store, resolve, reject) => {
      const request = store.put({
        id: buildStorageId(queueKey, itemId),
        queueKey,
        itemId,
        file,
      } as StoredQueueFile);

      request.onsuccess = () => resolve();
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to persist import queue file."));
    });
  },

  async getFile(queueKey: string, itemId: string): Promise<File | null> {
    return runTransaction<File | null>("readonly", (store, resolve, reject) => {
      const request = store.get(buildStorageId(queueKey, itemId));

      request.onsuccess = () => {
        const result = request.result as StoredQueueFile | undefined;
        resolve(result?.file ?? null);
      };
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to read import queue file."));
    });
  },

  async deleteFile(queueKey: string, itemId: string): Promise<void> {
    await runTransaction<void>("readwrite", (store, resolve, reject) => {
      const request = store.delete(buildStorageId(queueKey, itemId));
      request.onsuccess = () => resolve();
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to delete import queue file."));
    });
  },

  async clearQueue(queueKey: string): Promise<void> {
    await runTransaction<void>("readwrite", (store, resolve, reject) => {
      const request = store.openCursor();

      request.onsuccess = () => {
        const cursor = request.result;
        if (!cursor) {
          resolve();
          return;
        }

        const record = cursor.value as StoredQueueFile;
        if (record.queueKey === queueKey) {
          cursor.delete();
        }
        cursor.continue();
      };

      request.onerror = () =>
        reject(request.error ?? new Error("Failed to clear import queue files."));
    });
  },
};
