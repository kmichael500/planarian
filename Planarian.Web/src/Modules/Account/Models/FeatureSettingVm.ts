export interface FeatureSettingVm {
  id: string;
  key: FeatureKey;
  isEnabled: boolean;
}

export enum FeatureKey {
  EnabledFieldCaveAlternateNames = "EnabledFieldCaveAlternateNames",
  EnabledFieldCaveLengthFeet = "EnabledFieldCaveLengthFeet",
  EnabledFieldCaveDepthFeet = "EnabledFieldCaveDepthFeet",
  EnabledFieldCaveMaxPitDepthFeet = "EnabledFieldCaveMaxPitDepthFeet",
  EnabledFieldCaveNumberOfPits = "EnabledFieldCaveNumberOfPits",
  EnabledFieldCaveNarrative = "EnabledFieldCaveNarrative",
  EnabledFieldCaveGeologyTags = "EnabledFieldCaveGeologyTags",
  EnabledFieldCaveMapStatusTags = "EnabledFieldCaveMapStatusTags",
  EnabledFieldCaveGeologicAgeTags = "EnabledFieldCaveGeologicAgeTags",
  EnabledFieldCavePhysiographicProvinceTags = "EnabledFieldCavePhysiographicProvinceTags",
  EnabledFieldCaveBiologyTags = "EnabledFieldCaveBiologyTags",
  EnabledFieldCaveArcheologyTags = "EnabledFieldCaveArcheologyTags",
  EnabledFieldCaveCartographerNameTags = "EnabledFieldCaveCartographerNameTags",
  EnabledFieldCaveReportedByNameTags = "EnabledFieldCaveReportedByNameTags",
  EnabledFieldCaveOtherTags = "EnabledFieldCaveOtherTags",
  EnabledFieldEntranceDescription = "EnabledFieldEntranceDescription",
  EnabledFieldEntranceStatusTags = "EnabledFieldEntranceStatusTags",
  EnabledFieldEntranceHydrologyTags = "EnabledFieldEntranceHydrologyTags",
  EnabledFieldEntranceFieldIndicationTags = "EnabledFieldEntranceFieldIndicationTags",
  EnabledFieldEntranceReportedByNameTags = "EnabledFieldEntranceReportedByNameTags",
  EnabledFieldEntranceOtherTags = "EnabledFieldEntranceOtherTags",
}
