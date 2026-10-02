import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface InviteUserResultVm {
  userId: string;
  invitationEmailDeliveryStatus?: MessageDeliveryStatus;
}
