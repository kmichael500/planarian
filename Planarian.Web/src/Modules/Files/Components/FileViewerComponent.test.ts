import { readFileSync } from "fs";
import { resolve } from "path";

describe("optional file viewers", () => {
  const source = readFileSync(
    resolve(__dirname, "FileViewerComponent.tsx"),
    "utf8"
  );

  it("loads PDF and vector viewers through dynamic imports", () => {
    expect(source).toContain('import("./PdfViewer")');
    expect(source).toContain('import("./VectorDatasetViewer")');
    expect(source).not.toContain('from "./PdfViewer"');
    expect(source).not.toContain('from "./VectorDatasetViewer"');
  });
});
