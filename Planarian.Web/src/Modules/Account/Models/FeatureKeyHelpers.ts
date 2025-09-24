import { FeatureKey } from "./FeatureSettingVm";

const getFeatureKeyLabel = (key: FeatureKey): string => {
  switch (key) {
    case FeatureKey.EnabledFieldCaveId:
      return "Cave: ID";
    case FeatureKey.EnabledFieldCaveName:
      return "Cave: Name";
    case FeatureKey.EnabledFieldCaveAlternateNames:
      return "Cave: Alternate Names";
    case FeatureKey.EnabledFieldCaveState:
      return "Cave: State";
    case FeatureKey.EnabledFieldCaveCounty:
      return "Cave: County";
    case FeatureKey.EnabledFieldCaveLengthFeet:
      return "Cave: Length";
    case FeatureKey.EnabledFieldCaveDepthFeet:
      return "Cave: Depth";
    case FeatureKey.EnabledFieldCaveMaxPitDepthFeet:
      return "Cave: Max Pit Depth";
    case FeatureKey.EnabledFieldCaveNumberOfPits:
      return "Cave: Number of Pits";
    case FeatureKey.EnabledFieldCaveReportedOn:
      return "Cave: Reported On";
    case FeatureKey.EnabledFieldCaveReportedByNameTags:
      return "Cave: Reported By";
    case FeatureKey.EnabledFieldCaveGeologyTags:
      return "Cave: Geology Tags";
    case FeatureKey.EnabledFieldCaveGeologicAgeTags:
      return "Cave: Geologic Age Tags";
    case FeatureKey.EnabledFieldCavePhysiographicProvinceTags:
      return "Cave: Physiographic Province Tags";
    case FeatureKey.EnabledFieldCaveBiologyTags:
      return "Cave: Biology Tags";
    case FeatureKey.EnabledFieldCaveArcheologyTags:
      return "Cave: Archaeology Tags";
    case FeatureKey.EnabledFieldCaveMapStatusTags:
      return "Cave: Map Status Tags";
    case FeatureKey.EnabledFieldCaveCartographerNameTags:
      return "Cave: Cartographer Names";
    case FeatureKey.EnabledFieldCaveOtherTags:
      return "Cave: Other Tags";
    case FeatureKey.EnabledFieldCaveNarrative:
      return "Cave: Narrative";
    case FeatureKey.EnabledFieldCaveDistance:
      return "Cave: Distance";
    case FeatureKey.EnabledFieldEntranceCoordinates:
      return "Entrance: Coordinates";
    case FeatureKey.EnabledFieldEntranceElevation:
      return "Entrance: Elevation";
    case FeatureKey.EnabledFieldEntranceLocationQuality:
      return "Entrance: Location Quality";
    case FeatureKey.EnabledFieldEntranceName:
      return "Entrance: Name";
    case FeatureKey.EnabledFieldEntranceReportedOn:
      return "Entrance: Reported On";
    case FeatureKey.EnabledFieldEntranceReportedByNameTags:
      return "Entrance: Reported By";
    case FeatureKey.EnabledFieldEntrancePitDepth:
      return "Entrance: Pit Depth";
    case FeatureKey.EnabledFieldEntranceStatusTags:
      return "Entrance: Status Tags";
    case FeatureKey.EnabledFieldEntranceFieldIndicationTags:
      return "Entrance: Field Indication Tags";
    case FeatureKey.EnabledFieldEntranceHydrologyTags:
      return "Entrance: Hydrology Tags";
    case FeatureKey.EnabledFieldEntranceDescription:
      return "Entrance: Description";
    default:
      return key;
  }
};

export { getFeatureKeyLabel };
