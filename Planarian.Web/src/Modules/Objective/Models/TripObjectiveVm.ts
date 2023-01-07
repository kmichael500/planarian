export interface TripObjectiveVm {
  id: string;
  tripId: string;
  tripObjectiveTypeIds: string[];
  tripObjectiveMemberIds: string[];
  name: string;
  description: string;
  tripReport: string | null;
}
