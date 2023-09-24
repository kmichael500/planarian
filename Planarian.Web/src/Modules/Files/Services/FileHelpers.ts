export function isImageFileType(fileType: string | null | undefined): boolean {
  if (!fileType) {
    return false;
  }
  return ["jpeg", "jpg", "gif", "png"].includes(fileType);
}

export function isPdfFileType(fileType: string | null | undefined): boolean {
  if (!fileType) {
    return false;
  }
  return fileType === "pdf";
}

export const isTextFileType = (fileType: string | null | undefined) => {
  const textFileTypes = ["th", "txt", "json", "xml", "html", "js", "ts", "css"]; // Add more file types as needed
  return fileType ? textFileTypes.includes(fileType.toLowerCase()) : false;
};
export const isCsvFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "csv" : false;
};

export function isSupportedFileType(
  fileType: string | null | undefined
): boolean {
  if (!fileType) {
    return false;
  }
  return (
    isPdfFileType(fileType) ||
    isImageFileType(fileType) ||
    isTextFileType(fileType) ||
    isCsvFileType(fileType)
  );
}

export function getFileType(
  fileName: string | undefined | null
): string | null {
  if (!fileName) {
    return null;
  }

  const fileType = fileName.split(".").pop() || null;
  return fileType;
}
