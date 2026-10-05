import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface AccountAccessChangeResultVm {
  notificationEmailDeliveryStatus?: MessageDeliveryStatus | null;
}
