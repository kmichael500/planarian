// src/modules/Files/Components/FileListComponent.tsx
import React from "react";
import { Collapse } from "antd";
import { CloudUploadOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileListItemComponent } from "./FileListItemComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { FileVm } from "../Models/FileVm";
import {
  CaveHistoryRecord,
  CaveLogPropertyName,
} from "../../Caves/Models/ProposedChangeRequestVm";

const { Panel } = Collapse;

export interface FileListComponentProps {
  files?: FileVm[];
  originalFiles?: FileVm[];
  changes?: CaveHistoryRecord[];
  customOrder?: string[];
  hasEditPermission?: boolean;
}

export const FileListComponent = ({
  files = [],
  originalFiles = [],
  changes = [],
  customOrder = [],
  hasEditPermission = true,
}: FileListComponentProps) => {
  const diffMode = Boolean(changes.length && originalFiles.length);

  // group current files by type
  const currentByType = groupBy(files, (f) => f.fileTypeKey);

  // figure out which originals have been removed
  const removedFiles = diffMode
    ? originalFiles.filter((o) => !files.some((f) => f.id === o.id))
    : [];
  const removedByType = groupBy(removedFiles, (f) => f.fileTypeKey);

  // collect change-records by fileId
  const fileChangesById: Record<string, CaveHistoryRecord[]> = diffMode
    ? changes.reduce((acc, rec) => {
        if (rec.fileId) {
          (acc[rec.fileId] ||= []).push(rec);
        }
        return acc;
      }, {} as Record<string, CaveHistoryRecord[]>)
    : {};

  // determine panel order
  const allTypes = Array.from(
    new Set([...Object.keys(currentByType), ...Object.keys(removedByType)])
  );
  const sortedTypes = [
    ...customOrder.filter((t) => allTypes.includes(t)),
    ...allTypes
      .filter((t) => !customOrder.includes(t))
      .sort((a, b) => a.localeCompare(b)),
  ];

  // build panels
  const panelData = sortedTypes.map((type) => {
    console.log(originalFiles, files);
    // current items (from files)
    const current = currentByType[type] || [];
    const newItems = current.map((f) => {
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

    // removed items (only use originalFiles here)
    const removedItems = removedFiles
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
  });

  const defaultKeys = panelData
    .filter((p) => (diffMode ? p.hasDiff : p.hasCurrent))
    .map((p) => p.type);

  return (
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
              noDataCreateButton={
                <PlanarianButton icon={<CloudUploadOutlined />}>
                  Upload
                </PlanarianButton>
              }
              items={items}
              itemKey={(it) => it.id + (it.isRemoved ? "_removed" : "")}
              renderItem={(it) => (
                <FileListItemComponent
                  file={it}
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
  );
};
