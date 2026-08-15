import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface UserManagerGridVm {
  userId: string;
  emailAddress: string;
  fullName: string;
  invitationAcceptedOn: string | null;
  invitationSentOn: string | null;
  lastActiveOn: string | null;
  hasActiveInvitation: boolean;
  invitationEmailAttemptCount: number;
  invitationEmailDeliveryStatus?: MessageDeliveryStatus | null;
  invitationEmailDeliveryStatusOn?: string | null;
  invitationEmailOpenCount: number;
  invitationEmailAutomatedOpenCount: number;
  invitationEmailClickCount: number;
  invitationEmailAutomatedClickCount: number;
}
