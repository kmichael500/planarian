export enum MessageDeliveryEventType {
  Unknown = "Unknown",
  Accepted = "Accepted",
  Delivered = "Delivered",
  TemporaryFailed = "TemporaryFailed",
  PermanentFailed = "PermanentFailed",
  Opened = "Opened",
  Clicked = "Clicked",
  Unsubscribed = "Unsubscribed",
  Complained = "Complained",
}

export const MessageDeliveryEventTypeDisplay: Record<
  MessageDeliveryEventType,
  string
> = {
  [MessageDeliveryEventType.Unknown]: "Unknown",
  [MessageDeliveryEventType.Accepted]: "Accepted",
  [MessageDeliveryEventType.Delivered]: "Delivered",
  [MessageDeliveryEventType.TemporaryFailed]: "Temporary failure",
  [MessageDeliveryEventType.PermanentFailed]: "Permanent failure",
  [MessageDeliveryEventType.Opened]: "Opened",
  [MessageDeliveryEventType.Clicked]: "Clicked",
  [MessageDeliveryEventType.Unsubscribed]: "Unsubscribed",
  [MessageDeliveryEventType.Complained]: "Complained",
};
