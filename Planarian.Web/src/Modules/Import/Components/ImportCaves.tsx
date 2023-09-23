import { Typography, message } from "antd";
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { FileVm } from "../../Files/Models/FileVm";
import { CaveService } from "../../Caves/Service/CaveService";
import React from "react";
import {
  FailedCaveRecord,
  ImportApiErrorResponse,
} from "../../../Shared/Models/ApiErrorResponse";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import Papa from "papaparse";
import "./ImportComponent.scss";

export interface ImportCaveComponentProps {
  onUploaded: () => void;
}

const ImportCaveComponent = ({ onUploaded }: ImportCaveComponentProps) => {
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
      {!isUploaded && errorList.length <= 0 && (
        <UploadComponent
          draggerMessage="Click or drag your caves CSV file to this area to upload."
          draggerTitle="Import Cave File"
          hideCancelButton
          singleFile
          allowedFileTypes={[".csv"]}
          style={{ display: "flex" }}
          uploadFunction={async (params): Promise<FileVm> => {
            try {
              var result = await CaveService.ImportCaveCsv(
                params.file,
                params.uid,
                params.onProgress
              );
              setIsUploaded(true);
              onUploaded();
            } catch (e) {
              const error = e as ImportApiErrorResponse;
              if (errorList) {
                setErrorList(error.data);
              }
              message.error(error.message);
            }
            return {} as FileVm;
          }}
          updateFunction={(files: FileVm[]): Promise<void> => {
            throw new Error("Function not implemented intentionally.");
          }}
        />
      )}
      {errorList.length > 0 && (
        <>
          {/* <Typography.Paragraph>
            The following rows caused the import to fail. Please correct the
            errors and try again.
          </Typography.Paragraph> */}
          <CSVDisplay data={convertErrorListToCsv(errorList)} />
        </>
      )}
    </>
  );
};

export { ImportCaveComponent };
