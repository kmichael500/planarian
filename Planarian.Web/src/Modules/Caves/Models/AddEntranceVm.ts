export interface AddEntranceVm {
  id?: string;
  isPrimary: boolean;
  locationQualityTagId: string;
  name: string | null;
  description: string | null;
  latitude: number;
  longitude: number;
  elevationFeet: number;
  reportedOn: string | null;
  reportedByName: string | null;
  pitFeet: number | null;
  entranceStatusTagIds: string[];
  entranceHydrologyFrequencyTagIds: string[];
  fieldIndicationTagIds: string[];
  entranceHydrologyTagIds: string[];
}
