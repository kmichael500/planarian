export interface CaveSearchParamsVm {
  //#region Cave
  id: string;
  name: string;
  narrative: string;
  stateId: string;
  countyId: string;
  lengthFeet: number;
  depthFeet: number;
  elevationFeet: number;
  numberOfPits: number;
  maxPitDepthFeet: number;
  mapStatusTagIds: string;
  cartographerNamePeopleTagIds: string;
  geologyTagIds: string;
  geologicAgeTagIds: string;
  physiographicProvinceTagIds: string;
  biologyTagIds: string;
  archaeologyTagIds: string;
  caveReportedByNameTagIds: string;
  caveReportedOnDate: string;
  caveOtherTagIds: string;

  //#endregion
  //#region Entrance
  entranceStatusTagIds: string;
  entranceDescription: string;
  entranceFieldIndicationTagIds: string;
  entrancePitDepthFeet: number;
  locationQualityTagIds: string;
  entranceHydrologyTagIds: string;
  entranceReportedByPeopleTagIds: string;
  entranceReportedOnDate: string;
  //#endregion
  //#region Files
  fileTypeTagIds: string;
  fileDisplayName: string;
}
