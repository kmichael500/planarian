export interface CreateOrEditTripVm {
  id: string | null;
  projectId: string;
  name: string;
  description: string;
  tripReport: string | null;
  tripTagIds: string[];
  tripMemberIds: string[];
}
