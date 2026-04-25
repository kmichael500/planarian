import Papa from "papaparse";
import React from "react";
import { CloudDownloadOutlined } from "@ant-design/icons";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";
import { CSVDisplay } from "../../Files/Components/CsvDisplayComponent";
import { FileImportResult } from "../Models/FileUploadresult";

interface ImportFileResultsModalProps {
  open: boolean;
  results: FileImportResult[];
  onClose: () => void;
}

export const ImportFileResultsModal: React.FC<ImportFileResultsModalProps> = ({
  open,
  results,
  onClose,
}) => {
  const csvData = Papa.unparse(results);

  return (
    <PlanarianModal
      fullScreen
      header={[
        "File Import Results",
        <PlanarianButton
          key="download-results"
          alwaysShowChildren
          icon={<CloudDownloadOutlined />}
          onClick={() => downloadFile("file_import_results.csv", csvData)}
        >
          Download CSV
        </PlanarianButton>,
      ]}
      open={open}
      onClose={onClose}
      footer={null}
    >
      <CSVDisplay data={csvData} />
    </PlanarianModal>
  );
};
