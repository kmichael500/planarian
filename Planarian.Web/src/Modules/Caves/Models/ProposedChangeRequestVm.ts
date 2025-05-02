import { AddCaveVm } from "./AddCaveVm";
import { ChangeRequestType } from "./ChangeRequestType";

export interface ProposedChangeRequestVm {
  id: string;
  cave: AddCaveVm;
  originalCave: AddCaveVm;
  changes: CaveHistoryRecord[];
}

export interface CaveHistoryRecord {
  caveId: string;
  entranceId: string | null;
  entranceName: string | null;
  changedByUserId: string;
  approvedByUserId: string | null;
  propertyName: string;
  changeType: ChangeType;
  changeValueType: ChangeValueType;
  valueString: string | null;
  valueInt: number | null;
  valueDouble: number | null;
  valueBool: boolean | null;
  valueDateTime: string | null;

  createdOn: string;
}

export enum ChangeValueType {
  String = "String",
  Int = "Int",
  Double = "Double",
  Bool = "Bool",
  DateTime = "DateTime",
  Cave = "Cave",
  Entrance = "Entrance",
}

export enum ChangeType {
  Add = "Add",
  Update = "Update",
  Delete = "Delete",
  Rename = "Rename",
}

export enum CaveLogPropertyName {
  CountyName = "CountyName",
  StateName = "StateName",
  Name = "Name",
  AlternateNames = "AlternateNames",
  LengthFeet = "LengthFeet",
  DepthFeet = "DepthFeet",
  MaxPitDepthFeet = "MaxPitDepthFeet",
  NumberOfPits = "NumberOfPits",
  Narrative = "Narrative",
  ReportedOn = "ReportedOn",
  GeologyTagName = "GeologyTagName",
  MapStatusTagName = "MapStatusTagName",
  GeologicAgeTagName = "GeologicAgeTagName",
  PhysiographicProvinceTagName = "PhysiographicProvinceTagName",
  BiologyTagName = "BiologyTagName",
  ArcheologyTagName = "ArcheologyTagName",
  CartographerNameTagName = "CartographerNameTagName",
  ReportedByNameTagName = "ReportedByNameTagName",
  OtherTagName = "OtherTagName",

  EntranceName = "EntranceName",
  EntranceLatitude = "EntranceLatitude",
  EntranceLongitude = "EntranceLongitude",
  EntranceElevationFeet = "EntranceElevationFeet",
  EntranceDescription = "EntranceDescription",
  EntranceIsPrimary = "EntranceIsPrimary",
  EntranceLocationQualityTagName = "EntranceLocationQualityTagName",
  EntrancePitDepthFeet = "EntrancePitDepthFeet",
  EntranceReportedOn = "EntranceReportedOn",
  EntranceStatusTagName = "EntranceStatusTagName",
  EntranceHydrologyTagName = "EntranceHydrologyTagName",
  EntranceFieldIndicationTagName = "EntranceFieldIndicationTagName",
  EntranceReportedByNameTagName = "EntranceReportedByNameTagName",

  Entrance = "Entrance",
  Cave = "Cave",
}

export interface CaveHistory {
  changedByUserId: string | null;
  approvedByUserId: string | null;
  submittedOn: string;
  reviewedOn: string | null;
  caveHistoryDetails: HistoryDetail[];
  entranceHistorySummary: EntranceHistorySummary[];
  records: CaveHistoryRecord[];
  type: ChangeRequestType;
}

export interface HistoryDetail {
  propertyName: CaveLogPropertyName;
  valueStrings: string[];
  valueString: string | null;
  valueInt: number | null;
  valueDouble: number | null;
  valueBool: boolean | null;
  valueDateTime: string | null;
  previousValueStrings: string[];
  previousValueString: string | null;
  previousValueInt: number | null;
  previousValueDouble: number | null;
  previousValueBool: boolean | null;
  previousValueDateTime: string | null;
}

export interface EntranceHistorySummary {
  entranceName: string;
  entranceId: string;
  details: HistoryDetail[];
  changeType: ChangeType;
}
