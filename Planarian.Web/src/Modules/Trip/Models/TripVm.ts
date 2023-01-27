export interface TripVm {
  id: string;
  projectId: string;
  tripTagIds: string[];
  tripMemberIds: string[];
  name: string;
  description: string;
  tripReport: string | null;
}
