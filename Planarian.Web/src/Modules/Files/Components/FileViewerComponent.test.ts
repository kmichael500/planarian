import { readFileSync } from "fs";
import { resolve } from "path";

describe("FileViewer bundle boundaries", () => {
  const source = readFileSync(
    resolve(__dirname, "FileViewerComponent.tsx"),
    "utf8"
  );

  it("keeps PDF and vector viewers behind dynamic imports", () => {
    expect(source).toContain('import("./PdfViewer")');
    expect(source).toContain('import("./VectorDatasetViewer")');
    expect(source).not.toContain('from "./PdfViewer"');
    expect(source).not.toContain('from "./VectorDatasetViewer"');
  });
});
