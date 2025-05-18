import { Collapse } from "antd";
import { CloudUploadOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileListItemComponent } from "./FileListItemComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { FileVm } from "../Models/FileVm";
import { CaveHistoryRecord } from "../../Caves/Models/ProposedChangeRequestVm";
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
  changes,
  customOrder = [],
  hasEditPermission = true,
}: FileListComponentProps) => {
  const diffMode = Boolean(changes && originalFiles.length);

  const currentByType = groupBy(files, (f) => f.fileTypeKey);
  const originalByType = diffMode
    ? groupBy(originalFiles, (f) => f.fileTypeKey)
    : {};

  const allTypes = Array.from(
    new Set([...Object.keys(currentByType), ...Object.keys(originalByType)])
  );

  const sortedTypes = [
    ...customOrder.filter((key) => allTypes.includes(key)),
    ...allTypes
      .filter((key) => !customOrder.includes(key))
      .sort((a, b) => a.localeCompare(b)),
  ];

  const panelData = sortedTypes.map((type) => {
    const current = currentByType[type] || [];
    const original = originalByType[type] || [];

    const newItems = current.map((f) => ({
      ...f,
      isNew: diffMode && !original.some((o) => o.id === f.id),
      isRemoved: false,
    }));

    const removedItems = diffMode
      ? original
          .filter((o) => !current.some((f) => f.id === o.id))
          .map((f) => ({ ...f, isNew: false, isRemoved: true }))
      : [];

    const items = [...newItems, ...removedItems];
    return {
      type,
      items,
      hasDiff: items.some((i) => i.isNew || i.isRemoved),
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
                <PlanarianButton
                  alwaysShowChildren
                  icon={<CloudUploadOutlined />}
                >
                  Upload
                </PlanarianButton>
              }
              items={items}
              itemKey={(it) => it.id + (it.isRemoved ? "_removed" : "")}
              renderItem={(
                it: FileVm & { isNew?: boolean; isRemoved?: boolean }
              ) => (
                <FileListItemComponent
                  file={it}
                  isNew={it.isNew}
                  isRemoved={it.isRemoved}
                />
              )}
            />
          </Panel>
        )
      )}
    </Collapse>
  );
};
