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
  ChangeType,
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

  // group current + original by type
  const currentByType = groupBy(files, (f) => f.fileTypeKey);
  const originalByType = diffMode
    ? groupBy(originalFiles, (f) => f.fileTypeKey)
    : {};

  // collect change-records by fileId
  const fileChangesById: Record<string, CaveHistoryRecord[]> = diffMode
    ? changes.reduce((acc, rec) => {
        if (rec.fileId) {
          acc[rec.fileId] = acc[rec.fileId] || [];
          acc[rec.fileId].push(rec);
        }
        return acc;
      }, {} as Record<string, CaveHistoryRecord[]>)
    : {};

  // determine panel order
  const allTypes = Array.from(
    new Set([...Object.keys(currentByType), ...Object.keys(originalByType)])
  );
  const sortedTypes = [
    ...customOrder.filter((t) => allTypes.includes(t)),
    ...allTypes
      .filter((t) => !customOrder.includes(t))
      .sort((a, b) => a.localeCompare(b)),
  ];

  // build panels, tagging new/removed/changed + formatting originalValue
  const panelData = sortedTypes.map((type) => {
    const current = currentByType[type] || [];
    const original = originalByType[type] || [];

    const newItems = current.map((f) => {
      const orig = original.find((o) => o.id === f.id);
      const recs = fileChangesById[f.id] || [];

      const nameRec = recs.find(
        (c) => c.propertyName === CaveLogPropertyName.FileName
      );
      const tagRec = recs.find(
        (c) => c.propertyName === CaveLogPropertyName.FileTag
      );

      let isRenamed = false;
      let isTagChanged = false;
      let itemOriginalDisplayName: string | undefined | null;
      let itemOriginalTagValue: string | undefined | null;

      if (orig) {
        if (nameRec) {
          isRenamed = true;
          itemOriginalDisplayName = orig.displayName;
        }
        if (tagRec) {
          isTagChanged = true;
          itemOriginalTagValue = orig.fileTypeKey;
        }
      }

      const isChanged = isRenamed || isTagChanged;

      return {
        ...f,
        isNew: Boolean(diffMode && !orig),
        isRemoved: false,
        isChanged,
        isRenamed,
        isTagChanged,
        originalDisplayName: itemOriginalDisplayName,
        originalTagValue: itemOriginalTagValue,
      };
    });

    const removedItems = diffMode
      ? original
          .filter((o) => !current.some((f) => f.id === o.id))
          .map((f) => ({
            ...f,
            isNew: false,
            isRemoved: true,
            isChanged: false,
            isRenamed: false,
            isTagChanged: false,
            originalDisplayName: undefined,
            originalTagValue: undefined,
          }))
      : [];

    const items = [...newItems, ...removedItems];

    return {
      type,
      items,
      hasDiff: items.some((i) => i.isNew || i.isRemoved || i.isChanged),
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
              renderItem={(
                it: FileVm & {
                  isNew?: boolean;
                  isRemoved?: boolean;
                  isChanged?: boolean;
                  isRenamed?: boolean;
                  isTagChanged?: boolean;
                  originalDisplayName?: string | null;
                  originalTagValue?: string | null;
                }
              ) => (
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
