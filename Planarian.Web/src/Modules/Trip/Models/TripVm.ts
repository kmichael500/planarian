export interface TripVm {
  id: string;
  projectId: string;
  tripTagTypeIds: string[];
  tripMemberIds: string[];
  name: string;
  description: string;
  tripReport: string | null;
}
