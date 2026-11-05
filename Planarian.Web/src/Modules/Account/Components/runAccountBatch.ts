export async function runAccountBatch<T, R>(
  items: T[],
  operation: (item: T) => Promise<R>,
  isCurrentAccount: () => boolean,
  onResult: (item: T, result: { value?: R; error?: unknown }) => void,
  concurrency = 1
): Promise<void> {
  let next = 0;

  async function worker(): Promise<void> {
    while (isCurrentAccount() && next < items.length) {
      const item = items[next++];
      let outcome: { value?: R; error?: unknown };
      try {
        outcome = { value: await operation(item) };
      } catch (error) {
        outcome = { error };
      }
      onResult(item, outcome);
    }
  }

  await Promise.all(
    Array.from({ length: Math.min(concurrency, items.length) }, () => worker())
  );
}
