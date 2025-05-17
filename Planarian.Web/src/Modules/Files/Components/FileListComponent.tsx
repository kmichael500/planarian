import { Collapse, Tag } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileVm } from "../Models/FileVm";
import { FileListItemComponent } from "./FileListItemComponent";
import { groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { CloudUploadOutlined } from "@ant-design/icons";
import { ReactNode } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { CaveHistoryRecord } from "../../Caves/Models/ProposedChangeRequestVm";
const { Panel } = Collapse;

export interface FileListComponentProps {
  files?: FileVm[];
  customOrder?: string[];
  hasEditPermission?: boolean;

  changes?: CaveHistoryRecord[];
  originalFiles?: FileVm[];
}

export const FileListComponent = ({
  files,
  hasEditPermission = true,
  changes,
  originalFiles,
}: FileListComponentProps) => {
  const currentFilesList = files || [];
  const currentFilesByType = groupBy(
    currentFilesList,
    (file) => file.fileTypeTagId
  );

  const originalFilesList = changes && originalFiles ? originalFiles : [];
  const originalFilesByTypeGlobal =
    changes && originalFiles
      ? groupBy(originalFilesList, (f) => f.fileTypeTagId)
      : null;

  // Global ID sets based on your definition
  const currentFileIdsGlobal = new Set(currentFilesList.map((f) => f.id));
  const originalFileIdsGlobal = new Set(originalFilesList.map((f) => f.id));

  const allConsideredFileTypeIds = new Set<string>([
    ...Object.keys(currentFilesByType),
    ...(originalFilesByTypeGlobal
      ? Object.keys(originalFilesByTypeGlobal)
      : []),
  ]);
  const sortedRelevantFileTypeIds = Array.from(allConsideredFileTypeIds);

  const panelData = sortedRelevantFileTypeIds.map((fileTypeId) => {
    const itemsToDisplay: (FileVm & {
      isNew?: boolean;
      isRemoved?: boolean;
    })[] = [];
    const displayedIdsInPanel = new Set<string>(); // Avoid duplicates in a panel

    // 1. Add current files of this type
    const currentFilesForThisType = currentFilesByType[fileTypeId] || [];
    currentFilesForThisType.forEach((cf) => {
      if (displayedIdsInPanel.has(cf.id)) return;

      const isGloballyNew =
        changes && originalFiles ? !originalFileIdsGlobal.has(cf.id) : false;
      itemsToDisplay.push({ ...cf, isNew: isGloballyNew, isRemoved: false });
      displayedIdsInPanel.add(cf.id);
    });

    // 2. Add original files of this type that are now globally deleted
    if (changes && originalFiles && originalFilesByTypeGlobal) {
      const originalFilesForThisType =
        originalFilesByTypeGlobal[fileTypeId] || [];
      originalFilesForThisType.forEach((of) => {
        if (displayedIdsInPanel.has(of.id)) return;

        const isGloballyDeleted = !currentFileIdsGlobal.has(of.id);
        if (isGloballyDeleted) {
          itemsToDisplay.push({ ...of, isNew: false, isRemoved: true });
          displayedIdsInPanel.add(of.id);
        }
      });
    }

    const hasNewItemsInPanel = itemsToDisplay.some((item) => item.isNew);
    const hasRemovedItemsInPanel = itemsToDisplay.some(
      (item) => item.isRemoved
    );

    return {
      fileTypeId,
      hasNew: hasNewItemsInPanel, // Panel has new items if it lists globally new files
      hasRemoved: hasRemovedItemsInPanel, // Panel has removed if it lists globally deleted files
      items: itemsToDisplay,
    };
  });

  let defaultActiveKeys: string[];
  if (changes && originalFiles) {
    defaultActiveKeys = panelData
      .filter((info) => info.hasNew || info.hasRemoved) // Expand if panel lists new or removed items
      .map((info) => info.fileTypeId);
  } else {
    defaultActiveKeys = sortedRelevantFileTypeIds.filter(
      (id) => (currentFilesByType[id] || []).length > 0
    );
  }

  return (
    <>
      {panelData.length > 0 ? (
        <Collapse bordered defaultActiveKey={defaultActiveKeys}>
          {panelData.map(({ fileTypeId, hasNew, hasRemoved, items }) => {
            // hasRemoved here means this panel lists globally deleted files of this type
            if (items.length === 0 && !(changes && originalFiles)) {
              return null;
            }
            return (
              <Panel
                header={
                  <span
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "8px",
                    }}
                  >
                    <TagComponent tagId={fileTypeId} />
                  </span>
                }
                key={fileTypeId}
              >
                <CardGridComponent
                  useList
                  noDataDescription={`Looks like this cave was scooped ... do you want to change that?`}
                  noDataCreateButton={
                    <PlanarianButton
                      alwaysShowChildren
                      icon={<CloudUploadOutlined />}
                    >
                      Upload
                    </PlanarianButton>
                  }
                  renderItem={(
                    fileItem: FileVm & { isNew?: boolean; isRemoved?: boolean }
                  ) => {
                    return (
                      <FileListItemComponent
                        file={fileItem}
                        isNew={fileItem.isNew}
                        isRemoved={fileItem.isRemoved}
                      />
                    );
                  }}
                  itemKey={(item) =>
                    item.id + (item.isRemoved ? "_removed" : "")
                  }
                  items={items}
                ></CardGridComponent>
              </Panel>
            );
          })}
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
    </>
  );
};
