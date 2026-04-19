import Papa from "papaparse";
import React from "react";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
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
}) => (
  <PlanarianModal
    fullScreen
    header="File Import Results"
    open={open}
    onClose={onClose}
    footer={null}
  >
    <CSVDisplay data={Papa.unparse(results)} />
  </PlanarianModal>
);
