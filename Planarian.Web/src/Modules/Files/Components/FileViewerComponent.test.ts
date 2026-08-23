import { readFileSync } from "fs";
import { resolve } from "path";

describe("FileViewer bundle boundaries", () => {
  const source = readFileSync(
    resolve(__dirname, "FileViewerComponent.tsx"),
    "utf8"
  );
  const pdfSource = readFileSync(resolve(__dirname, "PdfViewer.tsx"), "utf8");

  it("declares a development eager PDF import and a production lazy PDF import", () => {
    expect(source).toContain('process.env.NODE_ENV === "development"');
    expect(source).toContain('require("./PdfViewer").PdfViewer');
    expect(source).toContain('import("./PdfViewer")');
    expect(source).toContain('import("./VectorDatasetViewer")');
    expect(source).not.toContain('from "./PdfViewer"');
    expect(source).not.toContain('from "./VectorDatasetViewer"');
  });

  it("wires both lazy viewers through retry and local error-boundary helpers", () => {
    expect(source).toContain('retryChunkImport(() => import("./PdfViewer"))');
    expect(source).toContain('retryChunkImport(() => import("./VectorDatasetViewer"))');
    expect(source.match(/<LazyLoadErrorBoundary/g)).toHaveLength(2);
  });

  it("wires PdfViewer to the authenticated view URL instead of the old blob loader", () => {
    expect(source).toContain("fileUrl={fileEmbedUrl}");
    expect(source).not.toContain("getFileBlob");
    expect(source).not.toContain("pdfFile");
    expect(pdfSource).toContain("file={fileUrl}");
    expect(pdfSource).toContain("options={documentOptions}");
  });

  it("keeps react-pdf dependencies inside the lazy PdfViewer module using the documented imports", () => {
    expect(pdfSource).toContain('from "react-pdf"');
    expect(pdfSource).toContain('import "react-pdf/dist/Page/AnnotationLayer.css"');
    expect(pdfSource).toContain('import "react-pdf/dist/Page/TextLayer.css"');
    expect(pdfSource).toContain("pdfjs.GlobalWorkerOptions.workerSrc");
    expect(pdfSource).not.toContain('import("react-pdf")');
  });
});
