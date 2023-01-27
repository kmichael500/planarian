export interface CreateOrEditTripObjectiveVm {
  id: string | null;
  projectId: string;
  name: string;
  description: string;
  tripReport: string | null;
  tripObjectiveTypeIds: string[];
  tripObjectiveMemberIds: string[];
}
