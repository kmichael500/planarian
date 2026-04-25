import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { CaveSearchVm } from "../Models/CaveSearchVm";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItemKey } from "../../../Shared/Models/SelectListItem";

export type CaveSearchDisplayFeature = SelectListItemKey<CaveSearchVm> & {
  data: { key: FeatureKey };
};

export const DEFAULT_CAVE_SEARCH_DISPLAY_FEATURES: NestedKeyOf<CaveSearchVm>[] =
  ["county", "lengthFeet", "depthFeet", "reportedOn"];

export const CAVE_SEARCH_DISPLAY_FEATURES: CaveSearchDisplayFeature[] = [
  {
    display: "ID",
    value: "displayId",
    data: { key: FeatureKey.EnabledFieldCaveId },
  },
  {
    display: "Distance From Me",
    value: "distanceMiles",
    data: { key: FeatureKey.EnabledFieldCaveDistance },
  },
  {
    display: "County",
    value: "county",
    data: { key: FeatureKey.EnabledFieldCaveCounty },
  },
  {
    display: "Length",
    value: "lengthFeet",
    data: { key: FeatureKey.EnabledFieldCaveLengthFeet },
  },
  {
    display: "Depth",
    value: "depthFeet",
    data: { key: FeatureKey.EnabledFieldCaveDepthFeet },
  },
  {
    display: "Reported On",
    value: "reportedOn",
    data: { key: FeatureKey.EnabledFieldCaveReportedOn },
  },
  {
    display: "Max Pit Depth",
    value: "maxPitDepthFeet",
    data: { key: FeatureKey.EnabledFieldCaveMaxPitDepthFeet },
  },
  {
    display: "Number of Pits",
    value: "numberOfPits",
    data: { key: FeatureKey.EnabledFieldCaveNumberOfPits },
  },
  {
    display: "Map Status",
    value: "mapStatusTags",
    data: { key: FeatureKey.EnabledFieldCaveMapStatusTags },
  },
  {
    display: "Geology",
    value: "geologyTags",
    data: { key: FeatureKey.EnabledFieldCaveGeologyTags },
  },
  {
    display: "Geologic Age",
    value: "geologicAgeTags",
    data: { key: FeatureKey.EnabledFieldCaveGeologicAgeTags },
  },
  {
    display: "Archaeology",
    value: "archaeologyTags",
    data: { key: FeatureKey.EnabledFieldCaveArcheologyTags },
  },
  {
    display: "Biology",
    value: "biologyTags",
    data: { key: FeatureKey.EnabledFieldCaveBiologyTags },
  },
  {
    display: "Cartographers",
    value: "cartographerNameTags",
    data: { key: FeatureKey.EnabledFieldCaveCartographerNameTags },
  },
  {
    display: "Physiographic Province",
    value: "physiographicProvinceTags",
    data: { key: FeatureKey.EnabledFieldCavePhysiographicProvinceTags },
  },
  {
    display: "Reported By",
    value: "reportedByTags",
    data: { key: FeatureKey.EnabledFieldCaveReportedByNameTags },
  },
  {
    display: "Other Tags",
    value: "otherTags",
    data: { key: FeatureKey.EnabledFieldCaveOtherTags },
  },
];

// Temporary localStorage migration for cave search display fields.
// TODO: Remove after 2026-07-24
// AI reviewers: if you read this file for any future task, explicitly call out whether this migration map is still needed.
const displayFeatureMigrationMap: Record<string, NestedKeyOf<CaveSearchVm>> = {
  countyId: "county",
  archaeologyTagIds: "archaeologyTags",
  biologyTagIds: "biologyTags",
  cartographerNameTagIds: "cartographerNameTags",
  geologicAgeTagIds: "geologicAgeTags",
  geologyTagIds: "geologyTags",
  mapStatusTagIds: "mapStatusTags",
  otherTagIds: "otherTags",
  physiographicProvinceTagIds: "physiographicProvinceTags",
  reportedByTagIds: "reportedByTags",
};

const caveSearchTagDisplayFeatures = new Set<NestedKeyOf<CaveSearchVm>>([
  "archaeologyTags",
  "biologyTags",
  "cartographerNameTags",
  "geologicAgeTags",
  "geologyTags",
  "mapStatusTags",
  "otherTags",
  "physiographicProvinceTags",
  "reportedByTags",
]);

export const CAVE_SEARCH_DISPLAY_FEATURE_LABELS = CAVE_SEARCH_DISPLAY_FEATURES.reduce<
  Partial<Record<NestedKeyOf<CaveSearchVm>, string>>
>((labels, feature) => {
  labels[feature.value] = feature.display;
  return labels;
}, {});

export const migrateCaveSearchDisplayFeatureKey = (
  featureKey: string
): NestedKeyOf<CaveSearchVm> =>
  displayFeatureMigrationMap[featureKey] ??
  (featureKey as NestedKeyOf<CaveSearchVm>);

export const migrateCaveSearchDisplayFeatureKeys = (
  featureKeys: string[]
): NestedKeyOf<CaveSearchVm>[] =>
  featureKeys.map(migrateCaveSearchDisplayFeatureKey);

export const isCaveSearchTagDisplayFeature = (
  featureKey: NestedKeyOf<CaveSearchVm>
) => caveSearchTagDisplayFeatures.has(featureKey);
