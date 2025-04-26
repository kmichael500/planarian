import { AddCaveVm } from "./AddCaveVm";

export interface ProposedChangeRequestVm {
  id: string;
  cave: AddCaveVm;
  originalCave: AddCaveVm;
  changes: CaveChangeLogVm[];
}

export interface CaveChangeLogVm {
  caveId: string;
  entranceId?: string;
  changedByUserId: string;
  approvedByUserId?: string;
  propertyName: CaveLogPropertyName;
  changeType: ChangeType;
  changeValueType: ChangeValueType;
  valueString?: string;
  valueInt?: number;
  valueDouble?: number;
  valueBool?: boolean;
  valueDateTime?: Date;
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
  EntranceDescription = "EntranceDescription",
  EntranceIsPrimary = "EntranceIsPrimary",
  EntranceLocationQualityTagName = "EntranceLocationQualityTagName",
  EntrancePitDepthFeet = "EntrancePitDepthFeet",
  EntranceReportedOn = "EntranceReportedOn",
  EntranceStatusTagName = "EntranceStatusTagName",
  EntranceHydrologyTagName = "EntranceHydrologyTagName",
  EntranceFieldIndicationTagName = "EntranceFieldIndicationTagName",
  EntranceReportedByNameTagName = "EntranceReportedByNameTagName",
  EntranceOtherTagName = "EntranceOtherTagName",

  Entrance = "Entrance",
  Cave = "Cave",
}
