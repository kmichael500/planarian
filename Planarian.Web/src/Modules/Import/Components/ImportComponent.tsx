import { Card, Space, Typography } from "antd";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CloudDownloadOutlined } from "@ant-design/icons";
import {
  createCsvWithHeaders,
  downloadFile,
} from "../../../Shared/Helpers/FileHelpers";
import {
  UploadComponent,
  UploadParams,
} from "../../Files/Components/UploadComponent";
import { FileVm } from "../../Files/Models/FileVm";
import { CaveService } from "../../Caves/Service/CaveService";

const caveImportHeaders = [
  "CaveName",
  "CaveLengthFt",
  "CaveDepthFt",
  "MaxPitDepthFt",
  "NumberOfPits",
  "Narrative",
  "CountyCode",
  "CountyName",
  "CountyCaveNumber",
  "State",
  "Geology",
  "ReportedOnDate",
  "ReportedByName",
  "IsArchived",
];

const entranceImportHeaders = [
  "CountyCaveNumber",
  "EntranceName",
  "EntranceDescription",
  "IsPrimaryEntrance",
  "EntrancePitDepth",
  "EntranceStatus",
  "EntranceHydrology",
  "EntranceHydrologyFrequency",
  "FieldIndication",
  "CountyCode",
  "DecimalLatitude",
  "DecimalLongitude",
  "EntranceElevationFt",
  "GeologyFormation",
  "ReportedOnDate",
  "ReportedByName",
  "LocationQuality",
];

const ImportComponent = () => {
  return (
    <Card style={{ height: "100%" }} title="Templates">
      <Typography.Paragraph>
        In order to import your data, it must be in the following format.
      </Typography.Paragraph>
      <Space>
        <PlanarianButton
          icon={<CloudDownloadOutlined />}
          onClick={() => {
            downloadFile(
              "cave_import.csv",
              createCsvWithHeaders(caveImportHeaders)
            );
          }}
        >
          Sample Cave Import Template
        </PlanarianButton>
        <PlanarianButton
          icon={<CloudDownloadOutlined />}
          onClick={() => {
            downloadFile(
              "entrance_import.csv",
              createCsvWithHeaders(entranceImportHeaders)
            );
          }}
        >
          Sample Entrance Import Template
        </PlanarianButton>
      </Space>
      <UploadComponent
        uploadFunction={async (params): Promise<FileVm> => {
          return await CaveService.ImportCaveCsv(
            params.file,
            params.uid,
            params.onProgress
          );
        }}
        updateFunction={(files: FileVm[]): Promise<void> => {
          throw new Error("Function not implemented.");
        }}
      />
    </Card>
  );
};

export { ImportComponent };
