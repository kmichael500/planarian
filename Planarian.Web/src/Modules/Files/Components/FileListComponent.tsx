// src/modules/Files/Components/FileListComponent.tsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Collapse, Typography } from "antd";
import { CloudUploadOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileListItemComponent } from "./FileListItemComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { customSort, groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { FileVm } from "../Models/FileVm";
import {
  CaveHistoryRecord,
  CaveLogPropertyName,
} from "../../Caves/Models/ProposedChangeRequestVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { FileViewer } from "./FileViewerComponent";
import { getFileType } from "../Services/FileHelpers";
const { Panel } = Collapse;

export interface FileListComponentProps {
  files?: FileVm[];
  originalFiles?: FileVm[];
  changes?: CaveHistoryRecord[];
  customOrder?: string[];
  isUploading?: boolean;
  setIsUploading?: (value: boolean) => void;
  hasEditPermission?: boolean;
}

type FileListItemVm = FileVm & {
  isNew?: boolean;
  isRemoved?: boolean;
  isRenamed?: boolean;
  isTagChanged?: boolean;
  originalDisplayName?: string | null;
  originalTagValue?: string | null;
};

export const FileListComponent = ({
  files = [],
  originalFiles = [],
  changes = [],
  customOrder = [],
  setIsUploading,
  hasEditPermission = true,
}: FileListComponentProps) => {
  const diffMode = Boolean(changes.length && originalFiles.length);

  const filesByType = useMemo(
    () => groupBy(files, (file) => file.fileTypeKey),
    [files]
  );

  const removedFiles = useMemo(() => {
    if (!diffMode) {
      return [];
    }

    return originalFiles.filter((o) => !files.some((f) => f.id === o.id));
  }, [diffMode, originalFiles, files]);

  const removedByType = useMemo(
    () => groupBy(removedFiles, (file) => file.fileTypeKey),
    [removedFiles]
  );

  const fileChangesById: Record<string, CaveHistoryRecord[]> = useMemo(() => {
    if (!diffMode) {
      return {};
    }

    return changes.reduce((acc, rec) => {
      if (rec.fileId) {
        (acc[rec.fileId] ||= []).push(rec);
      }
      return acc;
    }, {} as Record<string, CaveHistoryRecord[]>);
  }, [diffMode, changes]);

  const allTypes = useMemo(
    () =>
      Array.from(
        new Set([...Object.keys(filesByType), ...Object.keys(removedByType)])
      ),
    [filesByType, removedByType]
  );

  const sortedTypes = useMemo(
    () => [
      ...customOrder.filter((t) => allTypes.includes(t)),
      ...allTypes
        .filter((t) => !customOrder.includes(t))
        .sort((a, b) => a.localeCompare(b)),
    ],
    [customOrder, allTypes]
  );

  const panelData = useMemo(
    () =>
      sortedTypes.map((type) => {
        const current = filesByType[type] || [];
        const newItems: FileListItemVm[] = current.map((f) => {
          const recs = fileChangesById[f.id] || [];
          const nameRec = recs.find(
            (c) => c.propertyName === CaveLogPropertyName.FileName
          );
          const tagRec = recs.find(
            (c) => c.propertyName === CaveLogPropertyName.FileTag
          );

          return {
            ...f,
            isNew: diffMode && !originalFiles.some((o) => o.id === f.id),
            isRemoved: false,
            isRenamed: Boolean(nameRec),
            isTagChanged: Boolean(tagRec),
            originalDisplayName: nameRec
              ? originalFiles.find((o) => o.id === f.id)?.displayName
              : undefined,
            originalTagValue: tagRec
              ? originalFiles.find((o) => o.id === f.id)?.fileTypeKey
              : undefined,
          };
        });

        const removedItems: FileListItemVm[] = removedFiles
          .filter((f) => f.fileTypeKey === type)
          .map((f) => ({
            ...f,
            isNew: false,
            isRemoved: true,
            isRenamed: false,
            isTagChanged: false,
            originalDisplayName: undefined,
            originalTagValue: undefined,
          }));

        const items = [...newItems, ...removedItems];
        return {
          type,
          items,
          hasDiff: items.some(
            (i) => i.isNew || i.isRemoved || i.isRenamed || i.isTagChanged
          ),
          hasCurrent: current.length > 0,
        };
      }),
    [
      sortedTypes,
      filesByType,
      fileChangesById,
      diffMode,
      originalFiles,
      removedFiles,
    ]
  );

  const defaultKeys = useMemo(
    () =>
      panelData
        .filter((p) => (diffMode ? p.hasDiff : p.hasCurrent))
        .map((p) => p.type),
    [panelData, diffMode]
  );

  const orderedFiles = useMemo(
    () => sortedTypes.flatMap((fileType) => filesByType[fileType] ?? []),
    [sortedTypes, filesByType]
  );

  const [activeFileId, setActiveFileId] = useState<string | null>(null);

  const activeIndex = useMemo(() => {
    if (!activeFileId) {
      return -1;
    }

    return orderedFiles.findIndex((file) => file.id === activeFileId);
  }, [orderedFiles, activeFileId]);

  const activeFile = activeIndex >= 0 ? orderedFiles[activeIndex] : null;

  const openViewer = useCallback((file: FileListItemVm) => {
    if (file?.id && !file.isRemoved) {
      setActiveFileId(file.id);
    }
  }, []);

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

  const renderUploadButton = () => (
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
  );

  return (
    <>
      {panelData.length > 0 ? (
        <Collapse bordered defaultActiveKey={defaultKeys}>
          {panelData.map(({ type, items }) =>
            !items.length && !diffMode ? null : (
              <Panel
                key={type}
                header={
                  <span style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <TagComponent tagId={type} />
                  </span>
                }
              >
                <CardGridComponent
                  useList
                  noDataDescription="Looks like this cave was scooped … do you want to change that?"
                  noDataCreateButton={renderUploadButton()}
                  items={items}
                  itemKey={(it) => it.id + (it.isRemoved ? "_removed" : "")}
                  renderItem={(it) => (
                    <FileListItemComponent
                      file={it}
                      onView={openViewer}
                      isNew={it.isNew}
                      isRemoved={it.isRemoved}
                      isRenamed={it.isRenamed}
                      isTagChanged={it.isTagChanged}
                      originalDisplayName={it.originalDisplayName}
                      originalTagValue={it.originalTagValue}
                    />
                  )}
                />
              </Panel>
            )
          )}
        </Collapse>
      ) : (
        <CardGridComponent
          noDataDescription="Looks like this cave was scooped … do you want to change that?"
          noDataCreateButton={renderUploadButton()}
          renderItem={() => <Typography.Text />}
          itemKey={() => ""}
        />
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
        canGoNext={activeIndex !== -1 && activeIndex < orderedFiles.length - 1}
      />
    </>
  );
};
