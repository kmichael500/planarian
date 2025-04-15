import {
  capitalizeFirstLetter,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";

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
  countyId: string;
  displayId: string;
  primaryEntranceLatitude: number | null;
  primaryEntranceLongitude: number | null;
  primaryEntranceElevationFeet: number | null;
  archaeologyTagIds: string[];
  biologyTagIds: string[];
  cartographerNameTagIds: string[];
  geologicAgeTagIds: string[];
  geologyTagIds: string[];
  mapStatusTagIds: string[];
  otherTagIds: string[];
  physiographicProvinceTagIds: string[];
  reportedByTagIds: string[];
  isFavorite: boolean;
}

export class CaveSearchSortByConstants {
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
}
