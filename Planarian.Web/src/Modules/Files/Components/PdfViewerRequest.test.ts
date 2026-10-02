import {
  createPdfDocumentRequestOptions,
  createPdfStreamSessionId,
  PDF_STREAM_SESSION_HEADER,
} from "./PdfViewerRequest";

describe("PDF document request options", () => {
  it("loads the authenticated file URL directly and scopes range requests to one viewer session header", () => {
    const sessionId = "3a3f8e88-aac9-4aba-9982-44ec03d340ab";
    const options = createPdfDocumentRequestOptions(
      "https://planarian.test/api/files/abcdefghij/view?account_id=account",
      sessionId
    );

    const requestUrl = new URL(options.url);
    expect(requestUrl.pathname).toBe("/api/files/abcdefghij/view");
    expect(requestUrl.searchParams.get("account_id")).toBe("account");
    expect(options.withCredentials).toBe(true);
    expect(options.httpHeaders).toEqual({
      [PDF_STREAM_SESSION_HEADER]: sessionId,
    });
  });

  it("creates a backend-compatible stream session identifier", () => {
    expect(createPdfStreamSessionId()).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i
    );
  });
});
