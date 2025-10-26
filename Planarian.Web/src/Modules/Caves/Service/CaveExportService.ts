import { message } from "antd";
import { saveAs } from "file-saver";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { CaveService } from "./CaveService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";

export type ExportType = "gpx" | "csv";

const performCaveExport = async (
  type: ExportType,
  queryBuilder: QueryBuilder<CaveSearchParamsVm>,
  exportFeatureKeys?: FeatureKey[]
) => {
  const exportLabel = type.toUpperCase();
  const hide = message.loading(`Exporting ${exportLabel}...`, 0);

  try {
    const response =
      type === "gpx"
        ? await CaveService.ExportCavesGpx(queryBuilder, exportFeatureKeys)
        : await CaveService.ExportCavesCsv(queryBuilder, exportFeatureKeys);

    const accountName = AuthenticationService.GetAccountName() || "Export";
    const localDateTime = new Date().toISOString();
    const fileName = `${accountName} ${localDateTime}.${type}`;

    saveAs(response, fileName);
  } catch (err) {
    const error = err as ApiErrorResponse;
    message.error(error.message);
  } finally {
    hide();
  }
};

export { performCaveExport };
