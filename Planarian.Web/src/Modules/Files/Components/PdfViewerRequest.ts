export const PDF_STREAM_SESSION_QUERY_PARAMETER = "file_stream_session";

const formatUuidV4 = (bytes: Uint8Array) => {
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
};

export const createPdfStreamSessionId = () => {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  const bytes = new Uint8Array(16);
  if (typeof crypto !== "undefined" && typeof crypto.getRandomValues === "function") {
    crypto.getRandomValues(bytes);
  } else {
    for (let index = 0; index < bytes.length; index += 1) {
      bytes[index] = Math.floor(Math.random() * 256);
    }
  }
  return formatUuidV4(bytes);
};

export interface PdfDocumentRequestOptions {
  url: string;
  withCredentials: true;
}

export const createPdfDocumentRequestOptions = (
  url: string,
  sessionId: string
): PdfDocumentRequestOptions => {
  const requestUrl = new URL(url);
  requestUrl.searchParams.set(PDF_STREAM_SESSION_QUERY_PARAMETER, sessionId);

  return {
    url: requestUrl.toString(),
    withCredentials: true,
  };
};
