export enum MessageDeliveryStatus {
  Submitting = "Submitting",
  Submitted = "Submitted",
  SendFailed = "SendFailed",
  Accepted = "Accepted",
  TemporaryFailed = "TemporaryFailed",
  Delivered = "Delivered",
  PermanentFailed = "PermanentFailed",
}

export const MessageDeliveryStatusDisplay: Record<
  MessageDeliveryStatus,
  string
> = {
  [MessageDeliveryStatus.Submitting]: "Submitting",
  [MessageDeliveryStatus.Submitted]: "Submitted",
  [MessageDeliveryStatus.SendFailed]: "Send failed",
  [MessageDeliveryStatus.Accepted]: "Accepted",
  [MessageDeliveryStatus.TemporaryFailed]: "Temporary failure",
  [MessageDeliveryStatus.Delivered]: "Delivered",
  [MessageDeliveryStatus.PermanentFailed]: "Permanent failure",
};
