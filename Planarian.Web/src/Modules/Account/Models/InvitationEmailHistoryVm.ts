import { MessageDeliveryEventType } from "../../../Shared/Models/MessageDeliveryEventType";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

export interface InvitationEmailEventVm {
  eventType: MessageDeliveryEventType;
  occurredOn: string;
  bot?: string | null;
  severity?: string | null;
  reason?: string | null;
  deliveryCode?: string | null;
  enhancedDeliveryCode?: string | null;
  attemptNumber?: number | null;
  isDelayedBounce?: boolean | null;
}

export interface InvitationEmailAttemptVm {
  messageLogId: string;
  createdOn: string;
  deliveryStatus?: MessageDeliveryStatus | null;
  deliveryStatusOn?: string | null;
  events: InvitationEmailEventVm[];
}
