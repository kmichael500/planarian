import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface RegisterUserResultVm {
  confirmationEmailDeliveryStatus?: MessageDeliveryStatus;
}
