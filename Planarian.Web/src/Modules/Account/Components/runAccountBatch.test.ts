import { runAccountBatch } from "./runAccountBatch";

const deferred = <T>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((yes) => {
    resolve = yes;
  });
  return { promise, resolve };
};

describe("bounded account batch", () => {
  it("keeps at most two mutations in flight and reports each result", async () => {
    const first = deferred<number>();
    const second = deferred<number>();
    const calls: number[] = [];
    const results: Array<[number, number | undefined]> = [];
    const operation = jest.fn((number: number) => {
      calls.push(number);
      return number === 1
        ? first.promise
        : number === 2
        ? second.promise
        : Promise.resolve(number);
    });
    const batch = runAccountBatch(
      [1, 2, 3, 4],
      operation,
      () => true,
      (number, result) => results.push([number, result.value]),
      2
    );
    expect(calls).toEqual([1, 2]);
    first.resolve(1);
    await Promise.resolve();
    await Promise.resolve();
    expect(calls).toContain(3);
    second.resolve(2);
    await batch;
    expect(results).toHaveLength(4);
  });

  it("stops dispatching work after an account switch but records in-flight outcomes", async () => {
    let stillCurrent = true;
    const first = deferred<string>();
    const second = deferred<string>();
    const operation = jest.fn((number: number) =>
      number === 1 ? first.promise : second.promise
    );
    const results: number[] = [];
    const batch = runAccountBatch(
      [1, 2, 3, 4],
      operation,
      () => stillCurrent,
      (number) => {
        results.push(number);
      },
      2
    );
    expect(operation).toHaveBeenCalledTimes(2);
    stillCurrent = false;
    first.resolve("first");
    second.resolve("second");
    await batch;
    expect(operation).toHaveBeenCalledTimes(2);
    expect(results.sort()).toEqual([1, 2]);
  });

  it("sequences account-access mutations by default so they cannot conflict with each other", async () => {
    const first = deferred<number>();
    const calls: number[] = [];
    const batch = runAccountBatch(
      [1, 2],
      (number) => {
        calls.push(number);
        return number === 1 ? first.promise : Promise.resolve(number);
      },
      () => true,
      () => undefined
    );

    expect(calls).toEqual([1]);
    first.resolve(1);
    await batch;
    expect(calls).toEqual([1, 2]);
  });

  it("records a failed mutation without blocking the remaining people", async () => {
    const results: Array<{ number: number; failed: boolean }> = [];
    await runAccountBatch(
      [1, 2, 3],
      async (number) => {
        if (number === 2) throw Error("Failed");
        return number;
      },
      () => true,
      (number, result) =>
        results.push({ number, failed: Boolean(result.error) }),
      1
    );
    expect(results).toEqual([
      { number: 1, failed: false },
      { number: 2, failed: true },
      { number: 3, failed: false },
    ]);
  });
});
