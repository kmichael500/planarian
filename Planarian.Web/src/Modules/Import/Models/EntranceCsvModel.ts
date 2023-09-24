export interface EntranceCsvModel {
  countyCaveNumber: string;
  entranceName: string | null;
  entranceDescription: string | null;
  isPrimaryEntrance: boolean | null;
  entrancePitDepth: number | null;
  entranceStatus: string | null;
  entranceHydrology: string | null;
  entranceHydrologyFrequency: string | null;
  fieldIndication: string | null;
  countyCode: string | null;
  decimalLatitude: number | null;
  decimalLongitude: number | null;
  entranceElevationFt: number | null;
  geologyFormation: string | null;
  reportedOnDate: string | null;
  reportedByName: string | null;
  locationQuality: string | null;
  caveId: string | null;
}
