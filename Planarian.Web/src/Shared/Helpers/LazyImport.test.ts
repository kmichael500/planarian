import { isChunkLoadError, retryChunkImport } from "./LazyImport";

const chunkLoadError = () => {
  const error = new Error("Loading chunk PdfViewer failed.");
  error.name = "ChunkLoadError";
  return error;
};

it("recognizes Webpack chunk-load failures by name or message", () => {
  expect(isChunkLoadError(chunkLoadError())).toBe(true);
  expect(isChunkLoadError(new Error("Loading chunk 42 failed."))).toBe(true);
  expect(isChunkLoadError(new Error("module initialization failed"))).toBe(false);
});

it("retries a chunk-load failure once", async () => {
  const importer = jest.fn()
    .mockRejectedValueOnce(chunkLoadError())
    .mockResolvedValueOnce("loaded");

  await expect(retryChunkImport(importer, async () => undefined)).resolves.toBe("loaded");
  expect(importer).toHaveBeenCalledTimes(2);
});

it("propagates a persistent chunk failure after one retry", async () => {
  const first = chunkLoadError();
  const second = chunkLoadError();
  const importer = jest.fn()
    .mockRejectedValueOnce(first)
    .mockRejectedValueOnce(second);

  await expect(retryChunkImport(importer, async () => undefined)).rejects.toBe(second);
  expect(importer).toHaveBeenCalledTimes(2);
});

it("does not retry unrelated import failures", async () => {
  const error = new Error("module initialization failed");
  const importer = jest.fn().mockRejectedValue(error);

  await expect(retryChunkImport(importer)).rejects.toBe(error);
  expect(importer).toHaveBeenCalledTimes(1);
});
