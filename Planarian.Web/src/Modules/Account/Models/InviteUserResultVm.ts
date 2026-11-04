import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface InviteUserResultVm {
  userId: string;
  alreadyInvited?: boolean;
  invitationEmailDeliveryStatus?: MessageDeliveryStatus;
}
