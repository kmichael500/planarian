const DB_NAME = "planarian-file-upload-queue";
const DB_VERSION = 1;
const FILE_STORE = "files";
const QUEUE_KEY_INDEX = "queueKey";

interface StoredQueueFile {
  id: string;
  queueKey: string;
  itemId: string;
  file: File;
}

interface QueueFileToStore {
  itemId: string;
  file: File;
}

const buildStorageId = (queueKey: string, itemId: string) =>
  `${queueKey}::${itemId}`;

const buildQueueKeyRange = (queueKey: string) =>
  IDBKeyRange.bound(`${queueKey}::`, `${queueKey}::\uffff`);

let databasePromise: Promise<IDBDatabase> | null = null;

const openDatabase = (): Promise<IDBDatabase> => {
  if (databasePromise) {
    return databasePromise;
  }

  databasePromise = new Promise((resolve, reject) => {
    const request = window.indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const database = request.result;
      const store = database.objectStoreNames.contains(FILE_STORE)
        ? request.transaction!.objectStore(FILE_STORE)
        : database.createObjectStore(FILE_STORE, { keyPath: "id" });

      if (!store.indexNames.contains(QUEUE_KEY_INDEX)) {
        store.createIndex(QUEUE_KEY_INDEX, "queueKey", { unique: false });
      }
    };

    request.onsuccess = () => {
      const database = request.result;
      database.onversionchange = () => {
        database.close();
        databasePromise = null;
      };
      resolve(database);
    };
    request.onerror = () => {
      databasePromise = null;
      reject(request.error ?? new Error("Failed to open upload queue storage."));
    };
    request.onblocked = () => {
      databasePromise = null;
    };
  });

  return databasePromise;
};

const runTransaction = async <T>(
  mode: IDBTransactionMode,
  action: (
    store: IDBObjectStore,
    resolve: (value: T) => void,
    reject: (reason?: unknown) => void
  ) => void
): Promise<T> => {
  const database = await openDatabase();

  return new Promise<T>((resolve, reject) => {
    const transaction = database.transaction(FILE_STORE, mode);
    const store = transaction.objectStore(FILE_STORE);

    transaction.onerror = () => {
      reject(
        transaction.error ?? new Error("Upload queue storage transaction failed.")
      );
    };

    action(store, resolve, reject);
  });
};

export const QueuedFileStorage = {
  async putFiles(
    queueKey: string,
    files: QueueFileToStore[],
    onProgress?: (completedCount: number) => void
  ): Promise<void> {
    if (files.length === 0) {
      return;
    }

    const database = await openDatabase();

    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(FILE_STORE, "readwrite");
      const store = transaction.objectStore(FILE_STORE);
      let completedCount = 0;
      let hasFailed = false;

      transaction.oncomplete = () => resolve();
      transaction.onerror = () => {
        reject(
          transaction.error ?? new Error("Upload queue storage transaction failed.")
        );
      };
      transaction.onabort = () => {
        reject(
          transaction.error ?? new Error("Failed to persist upload queue files.")
        );
      };

      files.forEach(({ itemId, file }) => {
        const request = store.put({
          id: buildStorageId(queueKey, itemId),
          queueKey,
          itemId,
          file,
        } as StoredQueueFile);

        request.onsuccess = () => {
          completedCount += 1;
          onProgress?.(completedCount);
        };
        request.onerror = () => {
          if (hasFailed) {
            return;
          }

          hasFailed = true;
          reject(
            request.error ?? new Error("Failed to persist upload queue files.")
          );
          transaction.abort();
        };
      });
    });
  },

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
        reject(request.error ?? new Error("Failed to persist upload queue file."));
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
        reject(request.error ?? new Error("Failed to read upload queue file."));
    });
  },

  async deleteFile(queueKey: string, itemId: string): Promise<void> {
    await runTransaction<void>("readwrite", (store, resolve, reject) => {
      const request = store.delete(buildStorageId(queueKey, itemId));
      request.onsuccess = () => resolve();
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to delete upload queue file."));
    });
  },

  async clearQueue(
    queueKey: string,
    onProgress?: (completedCount: number) => void
  ): Promise<void> {
    await runTransaction<void>("readwrite", (store, resolve, reject) => {
      const request = store.delete(buildQueueKeyRange(queueKey));

      request.onsuccess = () => {
        onProgress?.(0);
        resolve();
      };
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to clear upload queue files."));
    });
  },
};
