export enum NotificationHubMethod {
  JoinGroup = "JoinGroup",
  LeaveGroup = "LeaveGroup",
}

export const NotificationHubEvent = {
  ReceiveNotification: "ReceiveNotification",
} as const;
