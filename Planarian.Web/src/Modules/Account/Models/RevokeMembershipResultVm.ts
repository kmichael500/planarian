import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface RevokeMembershipResultVm {
  notificationEmailDeliveryStatus?: MessageDeliveryStatus | null;
}
