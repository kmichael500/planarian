import { Collapse } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileVm } from "../Models/FileVm";
import { FileListItemComponent } from "./FileListItemComponent";
import { groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { CloudUploadOutlined } from "@ant-design/icons";
import { ReactNode } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { TagComponent } from "../../Tag/Components/TagComponent";
const { Panel } = Collapse;

export interface FileListComponentProps {
  files?: FileVm[];
  customOrder?: string[];
  hasEditPermission?: boolean;
}

export const FileListComponent = ({
  files,
  hasEditPermission = true,
}: FileListComponentProps) => {
  if (!files) {
    files = [];
  }
  const filesByType = groupBy(files, (file) => file.fileTypeTagId);

  let sortedFileTypesIds = Object.keys(filesByType);

  return (
    <>
      {sortedFileTypesIds.length > 0 ? (
        <Collapse bordered defaultActiveKey={sortedFileTypesIds}>
          {sortedFileTypesIds.map((fileTypeId) => (
            <Panel
              header={<TagComponent tagId={fileTypeId} />}
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
                renderItem={(file) => {
                  return <FileListItemComponent file={file} />;
                }}
                itemKey={(item) => {
                  return item.id;
                }}
                items={filesByType[fileTypeId]}
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
