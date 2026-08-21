export enum TagType {
  Default = "User",
  Trip = "Trip",
  Photo = "Photo",
  ProjectMember = "User",
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

export const TagTypeDisplayNames: Partial<Record<TagType, string>> = {
  [TagType.Archeology]: "Archaeology",
};
