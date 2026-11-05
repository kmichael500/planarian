import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export type AccountAccessAction = "disable" | "enable" | "revoke";

export interface AccountAccessChangeResultVm {
  notificationEmailDeliveryStatus?: MessageDeliveryStatus | null;
}
