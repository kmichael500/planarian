import { TagType } from "../../Tag/Models/TagType";
import { FeatureKey, FeatureSettingVm } from "./FeatureSettingVm";

export const tagTypeFeatureMap: Partial<
  Record<TagType, readonly FeatureKey[] | null>
> = {
  [TagType.EntranceHydrology]: [FeatureKey.EnabledFieldEntranceHydrologyTags],
  [TagType.People]: [
    FeatureKey.EnabledFieldCaveReportedByNameTags,
    FeatureKey.EnabledFieldEntranceReportedByNameTags,
    FeatureKey.EnabledFieldCaveCartographerNameTags,
  ],
  [TagType.EntranceStatus]: [FeatureKey.EnabledFieldEntranceStatusTags],
  [TagType.LocationQuality]: [FeatureKey.EnabledFieldEntranceLocationQuality],
  [TagType.FieldIndication]: [FeatureKey.EnabledFieldEntranceFieldIndicationTags],
  [TagType.File]: null,
  [TagType.Geology]: [FeatureKey.EnabledFieldCaveGeologyTags],
  [TagType.GeologicAge]: [FeatureKey.EnabledFieldCaveGeologicAgeTags],
  [TagType.PhysiographicProvince]: [FeatureKey.EnabledFieldCavePhysiographicProvinceTags],
  [TagType.Biology]: [FeatureKey.EnabledFieldCaveBiologyTags],
  [TagType.Archeology]: [FeatureKey.EnabledFieldCaveArcheologyTags],
  [TagType.MapStatus]: [FeatureKey.EnabledFieldCaveMapStatusTags],
  [TagType.CaveOther]: [
    FeatureKey.EnabledFieldCaveOtherTags,
    FeatureKey.EnabledFieldEntranceOtherTags,
  ],
};

export const isTagTypeEnabled = (
  tagType: TagType,
  featureSettings: FeatureSettingVm[]
): boolean => {
  const featureKeys = tagTypeFeatureMap[tagType];
  if (featureKeys === null) return true;
  if (!featureKeys || featureKeys.length === 0) return false;
  return featureKeys.some((key) =>
    featureSettings.some((setting) => setting.key === key && setting.isEnabled)
  );
};
