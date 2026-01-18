export function isImageFileType(fileType: string | null | undefined): boolean {
  if (!fileType) {
    return false;
  }
  return ["jpeg", "jpg", "gif", "png"].includes(fileType.toLowerCase());
}

export function isPdfFileType(fileType: string | null | undefined): boolean {
  if (!fileType) {
    return false;
  }
  return fileType.toLowerCase() === "pdf";
}

export const isTextFileType = (fileType: string | null | undefined) => {
  const textFileTypes = ["th", "txt", "json", "xml", "html", "js", "ts", "css"]; // Add more file types as needed
  return fileType ? textFileTypes.includes(fileType.toLowerCase()) : false;
};
export const isCsvFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "csv" : false;
};

export const isGpxFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "gpx" : false;
};

export const isGeoJsonFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "geojson" : false;
};

export const isKmlFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "kml" : false;
};

export const isZipFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "zip" : false;
};

export const isVectorDatasetFileType = (
  fileType: string | null | undefined
) => {
  if (!fileType) {
    return false;
  }
  const normalized = fileType.toLowerCase();
  return ["geojson", "kml", "zip", "json"].includes(normalized);
};

export const isPltFileType = (fileType: string | null | undefined) => {
  return fileType ? fileType.toLowerCase() === "plt" : false;
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
    isCsvFileType(fileType) ||
    isGpxFileType(fileType) ||
    isVectorDatasetFileType(fileType) ||
    isPltFileType(fileType)
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
