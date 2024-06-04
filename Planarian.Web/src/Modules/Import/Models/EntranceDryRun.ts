export interface EntranceDryRun {
  associatedCave: string;
  locationQuality: string;
  isPrimaryEntrance: boolean;
  entranceName: string | null;
  entranceDescription: string | null;
  decimalLatitude: number;
  decimalLongitude: number;
  entranceElevationFt: number;
  reportedOnDate: string | null;
  entrancePitDepth: number | null;
  entranceStatuses: string[];
  fieldIndication: string[];
  entranceHydrology: string[];
  reportedByNames: string[];
}
