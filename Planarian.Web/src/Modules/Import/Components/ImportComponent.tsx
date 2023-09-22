import { Card, Space, Tabs, Typography, message } from "antd";
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
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import React from "react";
import {
  ApiErrorResponse,
  FailedCaveRecord,
  ImportApiErrorResponse,
} from "../../../Shared/Models/ApiErrorResponse";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import Papa from "papaparse";
import "./ImportComponent.scss";

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
  const [isUploaded, setIsUploaded] = React.useState(false);
  const [errorList, setErrorList] = React.useState<FailedCaveRecord[]>([]);

  const convertErrorListToCsv = (errorList: FailedCaveRecord[]): string => {
    const csv = Papa.unparse(
      errorList.map((error) => ({
        rowNumber: error.rowNumber,
        reason: error.reason,
        ...error.caveCsvModel,
      }))
    );
    return csv;
  };

  return (
    <>
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
          <DeleteButtonComponent
            onConfirm={() => {
              CaveService.DeleteAllCaves();
            }}
            title={"Delete All Caves"}
          />
        </Space>
        <Tabs
          defaultActiveKey="1"
          items={[
            {
              label: "Caves",
              key: "1",
              children: (
                <>
                  {!isUploaded && errorList.length <= 0 && (
                    <UploadComponent
                      uploadFunction={async (params): Promise<FileVm> => {
                        try {
                          var result = await CaveService.ImportCaveCsv(
                            params.file,
                            params.uid,
                            params.onProgress
                          );
                          setIsUploaded(true);
                        } catch (e) {
                          const error = e as ImportApiErrorResponse;
                          setErrorList(error.data);
                          message.error(error.message);
                        }
                        return {} as FileVm;
                      }}
                      updateFunction={(files: FileVm[]): Promise<void> => {
                        throw new Error("Function not implemented.");
                      }}
                    />
                  )}
                  {errorList.length > 0 && (
                    <>
                      <Typography.Paragraph>
                        The following rows caused the import to fail. Please
                        correct the errors and try again.
                      </Typography.Paragraph>
                      <CSVDisplay data={convertErrorListToCsv(errorList)} />
                    </>
                  )}
                </>
              ),
            },
            {
              label: "Entrances",
              key: "2",
              children: (
                <>
                  {!isUploaded && errorList.length <= 0 && (
                    <UploadComponent
                      uploadFunction={async (params): Promise<FileVm> => {
                        try {
                          var result = await CaveService.ImportEntranceCsv(
                            params.file,
                            params.uid,
                            params.onProgress
                          );
                          setIsUploaded(true);
                        } catch (e) {
                          const error = e as ImportApiErrorResponse;
                          setErrorList(error.data);
                          message.error(error.message);
                        }
                        return {} as FileVm;
                      }}
                      updateFunction={(files: FileVm[]): Promise<void> => {
                        throw new Error("Function not implemented.");
                      }}
                    />
                  )}
                  {errorList.length > 0 && (
                    <>
                      <Typography.Paragraph>
                        The following rows caused the import to fail. Please
                        correct the errors and try again.
                      </Typography.Paragraph>
                      <CSVDisplay data={convertErrorListToCsv(errorList)} />
                    </>
                  )}
                </>
              ),
            },
          ]}
        />
      </Card>
    </>
  );
};

export { ImportComponent };
