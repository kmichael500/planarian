export interface FeatureSettingVm {
  id: string;
  key: FeatureKey;
  isEnabled: boolean;
  isDefault: boolean;
}

export enum FeatureKey {
  EnabledFieldCaveId = "EnabledFieldCaveId",
  EnabledFieldCaveName = "EnabledFieldCaveName",
  EnabledFieldCaveAlternateNames = "EnabledFieldCaveAlternateNames",
  EnabledFieldCaveState = "EnabledFieldCaveState",
  EnabledFieldCaveCounty = "EnabledFieldCaveCounty",
  EnabledFieldCaveLengthFeet = "EnabledFieldCaveLengthFeet",
  EnabledFieldCaveDepthFeet = "EnabledFieldCaveDepthFeet",
  EnabledFieldCaveMaxPitDepthFeet = "EnabledFieldCaveMaxPitDepthFeet",
  EnabledFieldCaveNumberOfPits = "EnabledFieldCaveNumberOfPits",
  EnabledFieldCaveReportedOn = "EnabledFieldCaveReportedOn",
  EnabledFieldCaveReportedByNameTags = "EnabledFieldCaveReportedByNameTags",
  EnabledFieldCaveGeologyTags = "EnabledFieldCaveGeologyTags",
  EnabledFieldCaveGeologicAgeTags = "EnabledFieldCaveGeologicAgeTags",
  EnabledFieldCavePhysiographicProvinceTags = "EnabledFieldCavePhysiographicProvinceTags",
  EnabledFieldCaveBiologyTags = "EnabledFieldCaveBiologyTags",
  EnabledFieldCaveArcheologyTags = "EnabledFieldCaveArcheologyTags",
  EnabledFieldCaveMapStatusTags = "EnabledFieldCaveMapStatusTags",
  EnabledFieldCaveCartographerNameTags = "EnabledFieldCaveCartographerNameTags",
  EnabledFieldCaveOtherTags = "EnabledFieldCaveOtherTags",
  EnabledFieldCaveNarrative = "EnabledFieldCaveNarrative",

  EnabledFieldEntranceCoordinates = "EnabledFieldEntranceCoordinates",
  EnabledFieldEntranceElevation = "EnabledFieldEntranceElevation",
  EnabledFieldEntranceLocationQuality = "EnabledFieldEntranceLocationQuality",
  EnabledFieldEntranceName = "EnabledFieldEntranceName",
  EnabledFieldEntranceReportedOn = "EnabledFieldEntranceReportedOn",
  EnabledFieldEntranceReportedByNameTags = "EnabledFieldEntranceReportedByNameTags",
  EnabledFieldEntrancePitDepth = "EnabledFieldEntrancePitDepth",
  EnabledFieldEntranceStatusTags = "EnabledFieldEntranceStatusTags",
  EnabledFieldEntranceFieldIndicationTags = "EnabledFieldEntranceFieldIndicationTags",
  EnabledFieldEntranceHydrologyTags = "EnabledFieldEntranceHydrologyTags",
  EnabledFieldEntranceDescription = "EnabledFieldEntranceDescription",
}
