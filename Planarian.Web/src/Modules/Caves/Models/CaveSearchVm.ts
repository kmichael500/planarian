import {
  capitalizeFirstLetter,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";

export interface CaveSearchVm {
  id: string;
  name: string;
  narrativeSnippet?: string;
  reportedOn: string | null;
  isArchived: boolean;
  depthFeet: number | null;
  lengthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
  county: SelectListItem<string>;
  countyDisplayId: string;
  countyNumber: number;
  displayId: string;
  primaryEntranceLatitude: number | null;
  primaryEntranceLongitude: number | null;
  primaryEntranceElevationFeet: number | null;
  distanceMiles: number | null;
  archaeologyTags: SelectListItem<string>[];
  biologyTags: SelectListItem<string>[];
  cartographerNameTags: SelectListItem<string>[];
  geologicAgeTags: SelectListItem<string>[];
  geologyTags: SelectListItem<string>[];
  mapStatusTags: SelectListItem<string>[];
  otherTags: SelectListItem<string>[];
  physiographicProvinceTags: SelectListItem<string>[];
  reportedByTags: SelectListItem<string>[];
  isFavorite: boolean;
}

export class CaveSearchSortByConstants {
  static readonly DisplayId = capitalizeFirstLetter(
    nameof<CaveSearchVm>("displayId")
  );
  static readonly Name = capitalizeFirstLetter(nameof<CaveSearchVm>("name"));
  static readonly ReportedOn = capitalizeFirstLetter(
    nameof<CaveSearchVm>("reportedOn")
  );
  static readonly DepthFeet = capitalizeFirstLetter(
    nameof<CaveSearchVm>("depthFeet")
  );
  static readonly LengthFeet = capitalizeFirstLetter(
    nameof<CaveSearchVm>("lengthFeet")
  );
  static readonly MaxPitDepthFeet = capitalizeFirstLetter(
    nameof<CaveSearchVm>("maxPitDepthFeet")
  );
  static readonly NumberOfPits = capitalizeFirstLetter(
    nameof<CaveSearchVm>("numberOfPits")
  );
  static readonly DistanceMiles = capitalizeFirstLetter(
    nameof<CaveSearchVm>("distanceMiles")
  );
}

export const CaveSortMetadataKeys = Object.freeze({
  distanceLocation: "distanceSortLocation",
});
