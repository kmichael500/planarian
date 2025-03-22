import { Collapse } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileVm } from "../Models/FileVm";
import { FileListItemComponent } from "./FileListItemComponent";
import { customSort, groupBy } from "../../../Shared/Helpers/ArrayHelpers";
import { CloudUploadOutlined } from "@ant-design/icons";
import { ReactNode } from "react";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
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
  if (!files) {
    files = [];
  }
  const filesByType = groupBy(files, (file) => file.fileTypeKey);

  let sortedFileTypes = customOrder
    ? customSort(customOrder, Object.keys(filesByType))
    : Object.keys(filesByType);

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
                  return <FileListItemComponent file={file} />;
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
    </>
  );
};
