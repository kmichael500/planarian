import { createPdfDocumentOptions, PDF_STREAM_SESSION_HEADER } from "./PdfViewerRequest";

it("uses credentialed requests and carries one explicit PDF stream session", () => {
  const sessionId = "01234567-89ab-cdef-0123-456789abcdef";

  expect(createPdfDocumentOptions(sessionId)).toEqual({
    withCredentials: true,
    httpHeaders: {
      [PDF_STREAM_SESSION_HEADER]: sessionId,
    },
  });
});
