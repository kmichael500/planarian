import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export enum AccountAccessAction {
  Disable = "Disable",
  Enable = "Enable",
  Revoke = "Revoke",
}

export interface AccountAccessChangeResultVm {
  notificationEmailDeliveryStatus?: MessageDeliveryStatus | null;
}
