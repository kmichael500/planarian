export const PDF_STREAM_SESSION_HEADER = "X-Planarian-File-Stream-Session";

export const createPdfDocumentOptions = (sessionId: string) => ({
  withCredentials: true,
  httpHeaders: {
    [PDF_STREAM_SESSION_HEADER]: sessionId,
  },
});
