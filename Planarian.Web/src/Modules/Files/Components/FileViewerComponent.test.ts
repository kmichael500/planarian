import { readFileSync } from "fs";
import { resolve } from "path";

describe("FileViewer bundle boundaries", () => {
  const source = readFileSync(
    resolve(__dirname, "FileViewerComponent.tsx"),
    "utf8"
  );

  const pdfSource = readFileSync(resolve(__dirname, "PdfViewer.tsx"), "utf8");

  it("keeps PDF and vector viewers behind dynamic imports", () => {
    expect(source).toContain('import("./PdfViewer")');
    expect(source).toContain('import("./VectorDatasetViewer")');
    expect(source).not.toContain('from "./PdfViewer"');
    expect(source).not.toContain('from "./VectorDatasetViewer"');
  });

  it("streams PDFs through the full PDF.js viewer instead of a custom interaction layer", () => {
    expect(source).toContain("fileUrl={fileEmbedUrl}");
    expect(source).not.toContain("getFileBlob");
    expect(source).not.toContain("pdfFile");
    expect(pdfSource).toContain('pdfAssetUrl("viewer/pdfjs-viewer-element.js")');
    expect(pdfSource).toContain("viewerApp.open(documentOptions)");
    expect(pdfSource).not.toContain('addEventListener("wheel"');
    expect(pdfSource).not.toContain('addEventListener("touchmove"');
    expect(pdfSource).not.toContain("updateScale(");
  });
});
