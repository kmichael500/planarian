export interface CreateOrEditTripObjectiveVm {
  id: string | null;
  projectId: string;
  tripId: string;
  tripObjectiveTypeIds: string[];
  name: string;
  description: string;
  tripReport: string | null;
  tripObjectiveMemberIds: string[];
}
