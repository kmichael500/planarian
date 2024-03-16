export interface EntranceVm {
  id: string;
  isPrimary: boolean;
  reportedByUserId: string | null;
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
  fieldIndicationTagIds: string[];
  entranceHydrologyTagIds: string[];
}
