import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface DisableAccessResultVm {
  notificationEmailDeliveryStatus?: MessageDeliveryStatus | null;
}
