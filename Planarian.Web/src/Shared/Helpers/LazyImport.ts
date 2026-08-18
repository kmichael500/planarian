const chunkLoadMessage = /Loading chunk .* failed/i;

export const isChunkLoadError = (error: unknown): boolean =>
  error instanceof Error &&
  (error.name === "ChunkLoadError" || chunkLoadMessage.test(error.message));

const waitBeforeRetry = () =>
  new Promise<void>(resolve => window.setTimeout(resolve, 250));

export const retryChunkImport = async <T>(
  importer: () => Promise<T>,
  wait: () => Promise<void> = waitBeforeRetry
): Promise<T> => {
  try {
    return await importer();
  } catch (error) {
    if (!isChunkLoadError(error)) throw error;
    await wait();
    return importer();
  }
};
