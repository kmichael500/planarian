const DB_NAME = "planarian-file-upload-queue";
const DB_VERSION = 2;
const FILE_STORE = "files";
const QUEUE_STATE_STORE = "queueStates";
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

export interface StoredQueueState<TState> {
  queueKey: string;
  state: TState;
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

      if (!database.objectStoreNames.contains(QUEUE_STATE_STORE)) {
        database.createObjectStore(QUEUE_STATE_STORE, { keyPath: "queueKey" });
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
      reject(new Error("Upload queue storage upgrade was blocked."));
    };
  });

  return databasePromise;
};

const runTransaction = async (
  mode: IDBTransactionMode,
  action: (store: IDBObjectStore, transaction: IDBTransaction) => void
): Promise<void> => {
  const database = await openDatabase();

  return new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(FILE_STORE, mode);
    const store = transaction.objectStore(FILE_STORE);

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => {
      reject(
        transaction.error ?? new Error("Upload queue storage transaction failed.")
      );
    };
    transaction.onabort = () => {
      reject(
        transaction.error ?? new Error("Upload queue storage transaction failed.")
      );
    };

    action(store, transaction);
  });
};

const abortTransaction = (transaction: IDBTransaction) => {
  try {
    transaction.abort();
  } catch {
    // The transaction may already be inactive or aborting.
  }
};

export const QueuedFileStorage = {
  async putQueueState<TState>(queueKey: string, state: TState): Promise<void> {
    const database = await openDatabase();

    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(QUEUE_STATE_STORE, "readwrite");
      const store = transaction.objectStore(QUEUE_STATE_STORE);
      const request = store.put({
        queueKey,
        state,
      } as StoredQueueState<TState>);

      transaction.oncomplete = () => resolve();
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to persist upload queue state."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to persist upload queue state."));
      request.onerror = () => abortTransaction(transaction);
    });
  },

  async getQueueState<TState>(queueKey: string): Promise<TState | null> {
    const database = await openDatabase();

    return new Promise<TState | null>((resolve, reject) => {
      const transaction = database.transaction(QUEUE_STATE_STORE, "readonly");
      const store = transaction.objectStore(QUEUE_STATE_STORE);
      const request = store.get(queueKey);

      request.onsuccess = () => {
        const result = request.result as StoredQueueState<TState> | undefined;
        resolve(result?.state ?? null);
      };
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to read upload queue state."));
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to read upload queue state."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to read upload queue state."));
    });
  },

  async deleteQueueState(queueKey: string): Promise<void> {
    const database = await openDatabase();

    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(QUEUE_STATE_STORE, "readwrite");
      const store = transaction.objectStore(QUEUE_STATE_STORE);
      const request = store.delete(queueKey);

      transaction.oncomplete = () => resolve();
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to delete upload queue state."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to delete upload queue state."));
      request.onerror = () => abortTransaction(transaction);
    });
  },

  async putFilesAndQueueState<TState>(
    queueKey: string,
    files: QueueFileToStore[],
    state: TState,
    onProgress?: (completedCount: number) => void
  ): Promise<void> {
    const database = await openDatabase();

    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(
        [FILE_STORE, QUEUE_STATE_STORE],
        "readwrite"
      );
      const fileStore = transaction.objectStore(FILE_STORE);
      const queueStateStore = transaction.objectStore(QUEUE_STATE_STORE);
      let completedCount = 0;

      transaction.oncomplete = () => resolve();
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to persist upload queue."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to persist upload queue."));

      files.forEach(({ itemId, file }) => {
        const request = fileStore.put({
          id: buildStorageId(queueKey, itemId),
          queueKey,
          itemId,
          file,
        } as StoredQueueFile);

        request.onsuccess = () => {
          completedCount += 1;
          onProgress?.(completedCount);
        };
        request.onerror = () => abortTransaction(transaction);
      });

      const queueStateRequest = queueStateStore.put({
        queueKey,
        state,
      } as StoredQueueState<TState>);
      queueStateRequest.onerror = () => abortTransaction(transaction);
    });
  },

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
          abortTransaction(transaction);
        };
      });
    });
  },

  async putFile(queueKey: string, itemId: string, file: File): Promise<void> {
    await runTransaction("readwrite", (store, transaction) => {
      const request = store.put({
        id: buildStorageId(queueKey, itemId),
        queueKey,
        itemId,
        file,
      } as StoredQueueFile);

      request.onerror = () => abortTransaction(transaction);
    });
  },

  async getFile(queueKey: string, itemId: string): Promise<File | null> {
    const database = await openDatabase();

    return new Promise<File | null>((resolve, reject) => {
      const transaction = database.transaction(FILE_STORE, "readonly");
      const store = transaction.objectStore(FILE_STORE);
      const request = store.get(buildStorageId(queueKey, itemId));

      request.onsuccess = () => {
        const result = request.result as StoredQueueFile | undefined;
        resolve(result?.file ?? null);
      };
      request.onerror = () =>
        reject(request.error ?? new Error("Failed to read upload queue file."));
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to read upload queue file."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to read upload queue file."));
    });
  },

  async deleteFile(queueKey: string, itemId: string): Promise<void> {
    await runTransaction("readwrite", (store, transaction) => {
      const request = store.delete(buildStorageId(queueKey, itemId));
      request.onerror = () => abortTransaction(transaction);
    });
  },

  async clearQueue(
    queueKey: string,
    onProgress?: (completedCount: number) => void
  ): Promise<void> {
    const database = await openDatabase();

    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(
        [FILE_STORE, QUEUE_STATE_STORE],
        "readwrite"
      );
      const fileStore = transaction.objectStore(FILE_STORE);
      const queueStateStore = transaction.objectStore(QUEUE_STATE_STORE);
      const deleteFilesRequest = fileStore.delete(buildQueueKeyRange(queueKey));
      const deleteQueueStateRequest = queueStateStore.delete(queueKey);

      transaction.oncomplete = () => {
        onProgress?.(0);
        resolve();
      };
      transaction.onerror = () =>
        reject(transaction.error ?? new Error("Failed to clear upload queue."));
      transaction.onabort = () =>
        reject(transaction.error ?? new Error("Failed to clear upload queue."));
      deleteFilesRequest.onerror = () => {
        abortTransaction(transaction);
      };
      deleteQueueStateRequest.onerror = () => {
        abortTransaction(transaction);
      };
    });
  },
};
