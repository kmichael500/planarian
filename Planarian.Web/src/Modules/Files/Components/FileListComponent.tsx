import { Collapse } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileVm } from "../Models/FileVm";
import { FileListItemComponent } from "./FileListItemComponent";
import { customSort, groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { CloudUploadOutlined } from "@ant-design/icons";
import { ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { FileViewer } from "./FileViewerComponent";
import { getFileType } from "../Services/FileHelpers";
const { Panel } = Collapse;

export interface FileListComponentProps {
  files?: FileVm[];
  customOrder?: string[];
  isUploading: boolean;
  setIsUploading?: (value: boolean) => void;
  hasEditPermission?: boolean;
}

export const FileListComponent = ({
  files,
  isUploading,
  setIsUploading,
  customOrder,
  hasEditPermission = true,
}: FileListComponentProps) => {
  const safeFiles = files ?? [];

  const filesByType = useMemo(
    () => groupBy(safeFiles, (file) => file.fileTypeKey),
    [safeFiles]
  );

  const sortedFileTypes = useMemo(() => {
    const typeKeys = Object.keys(filesByType);
    return customOrder ? customSort(customOrder, typeKeys) : typeKeys;
  }, [customOrder, filesByType]);

  const orderedFiles = useMemo(
    () =>
      sortedFileTypes.flatMap((fileType) => filesByType[fileType] ?? []),
    [sortedFileTypes, filesByType]
  );

  const [activeFileId, setActiveFileId] = useState<string | null>(null);

  const activeIndex = useMemo(() => {
    if (!activeFileId) {
      return -1;
    }

    return orderedFiles.findIndex((file) => file.id === activeFileId);
  }, [orderedFiles, activeFileId]);

  const activeFile = activeIndex >= 0 ? orderedFiles[activeIndex] : null;

  const openViewer = useCallback(
    (file: FileVm) => {
      if (file?.id) {
        setActiveFileId(file.id);
      }
    },
    []
  );

  const closeViewer = useCallback(() => {
    setActiveFileId(null);
  }, []);

  const goToPrevious = useCallback(() => {
    setActiveFileId((currentId) => {
      if (!currentId) {
        return currentId;
      }

      const currentIdx = orderedFiles.findIndex((file) => file.id === currentId);
      if (currentIdx <= 0) {
        return currentId;
      }

      const previousFile = orderedFiles[currentIdx - 1];
      return previousFile?.id ?? currentId;
    });
  }, [orderedFiles]);

  const goToNext = useCallback(() => {
    setActiveFileId((currentId) => {
      if (!currentId) {
        return currentId;
      }

      const currentIdx = orderedFiles.findIndex((file) => file.id === currentId);
      if (currentIdx === -1 || currentIdx >= orderedFiles.length - 1) {
        return currentId;
      }

      const nextFile = orderedFiles[currentIdx + 1];
      return nextFile?.id ?? currentId;
    });
  }, [orderedFiles]);

  useEffect(() => {
    if (activeFileId && activeIndex === -1) {
      setActiveFileId(null);
    }
  }, [activeFileId, activeIndex]);

  const viewerIsOpen = Boolean(activeFile);
  const viewerFileType = activeFile
    ? getFileType(activeFile.fileName)
    : undefined;

  return (
    <>
      {sortedFileTypes.length > 0 ? (
        <Collapse bordered defaultActiveKey={sortedFileTypes}>
          {sortedFileTypes.map((fileType) => (
            <Panel header={fileType} key={fileType}>
              <CardGridComponent
                useList
                noDataDescription={`Looks like this cave was scooped ... do you want to change that?`}
                noDataCreateButton={
                  <PlanarianButton
                    alwaysShowChildren
                    icon={<CloudUploadOutlined />}
                    onClick={() => {
                      if (setIsUploading) {
                        setIsUploading(true);
                      }
                    }}
                  >
                    Upload
                  </PlanarianButton>
                }
                renderItem={(file) => {
                  return (
                    <FileListItemComponent
                      file={file}
                      onView={openViewer}
                    />
                  );
                }}
                itemKey={(item) => {
                  return item.id;
                }}
                items={filesByType[fileType]}
              ></CardGridComponent>
            </Panel>
          ))}
        </Collapse>
      ) : (
        <CardGridComponent
          noDataDescription={`Looks like this cave was scooped ... do you want to change that?`}
          noDataCreateButton={
            <PlanarianButton
              icon={<CloudUploadOutlined />}
              alwaysShowChildren
              permissionKey={PermissionKey.Manager}
              disabled={!hasEditPermission}
              onClick={() => {
                if (setIsUploading) {
                  setIsUploading(true);
                }
              }}
            >
              Upload
            </PlanarianButton>
          }
          renderItem={function (item: object): ReactNode {
            return null;
          }}
          itemKey={function (item: object): string {
            return "";
          }}
        ></CardGridComponent>
      )}
      <FileViewer
        open={viewerIsOpen}
        onCancel={closeViewer}
        embedUrl={activeFile?.embedUrl}
        downloadUrl={activeFile?.downloadUrl}
        displayName={activeFile?.displayName}
        fileType={viewerFileType}
        onPrevious={goToPrevious}
        onNext={goToNext}
        canGoPrevious={activeIndex > 0}
        canGoNext={
          activeIndex !== -1 && activeIndex < orderedFiles.length - 1
        }
      />
    </>
  );
};
