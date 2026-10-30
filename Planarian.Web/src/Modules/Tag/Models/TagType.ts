export enum TagType {
  Default = "Default",
  Trip = "Trip",
  Photo = "Photo",
  Geology = "Geology",
  GeologicAge = "GeologicAge",
  PhysiographicProvince = "PhysiographicProvince",
  LocationQuality = "LocationQuality",
  EntranceStatus = "EntranceStatus",
  FieldIndication = "FieldIndication",
  EntranceHydrology = "EntranceHydrology",
  File = "File",
  People = "People",
  Biology = "Biology",
  Archeology = "Archeology",
  MapStatus = "MapStatus",
  CaveOther = "CaveOther",
}

export enum TagSelectSource {
  ProjectMember = "ProjectMember",
}

export type TagSelectType = TagType | TagSelectSource;

export const TagTypeDisplayNames: Partial<Record<TagSelectType, string>> = {
  [TagType.Archeology]: "Archaeology",
  [TagSelectSource.ProjectMember]: "User",
};
