import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { Alert, Badge, Space, Typography } from "antd";

const getNotificationMessage = (notification: unknown) => {
  if (typeof notification === "string") {
    return notification;
  }

  if (notification && typeof notification === "object") {
    const statusMessage = (notification as { statusMessage?: unknown })
      .statusMessage;
    if (typeof statusMessage === "string") {
      return statusMessage;
    }

    const message = (notification as { message?: unknown }).message;
    if (typeof message === "string") {
      return message;
    }
  }

  return "Progress update";
};

function NotificationComponent({
  groupName,
  isLoading,
  onConnected,
  onNotification,
  hideNotifications,
}: {
  groupName: string;
  isLoading: boolean;
  onConnected?: () => void | Promise<void>;
  onNotification?: (notification: unknown) => void;
  hideNotifications?: boolean;
}) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(
    null
  );
  const [notifications, setNotifications] = useState<string[]>([]);
  const hasJoinedGroupRef = useRef<string | null>(null);
  const onConnectedRef = useRef<typeof onConnected>(onConnected);

  useEffect(() => {
    onConnectedRef.current = onConnected;
  }, [onConnected]);

  useEffect(() => {
    setNotifications([]);
  }, [groupName]);

  useEffect(() => {
    // Build the connection
    const newConnection = new HubConnectionBuilder()
      .withUrl(AppOptions.signalrBaseUrl, {
        accessTokenFactory: () => {
          if (AuthenticationService.IsAuthenticated()) {
            var token = AuthenticationService.GetToken();
            if (token) {
              return token;
            }
          }
          throw new Error("No access token found");
        },
      })
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (!connection) {
      return;
    }

    let isDisposed = false;
    const receiveNotificationHandler = (notification: unknown) => {
      console.log("Received:", notification);
      onNotification?.(notification);
      const message = getNotificationMessage(notification);
      setNotifications((prev) => {
        const newArray = [...prev, message];
        if (newArray.length > 3) newArray.shift();
        return newArray;
      });
    };

    connection.on("ReceiveNotification", receiveNotificationHandler);
    connection
      .start()
      .then(async () => {
        if (isDisposed) {
          return;
        }

        console.log("Connection started!");
        if (groupName) {
          await connection.invoke("JoinGroup", groupName);
          hasJoinedGroupRef.current = groupName;
        }

        await onConnectedRef.current?.();
      })
      .catch((e) => console.error("Connection failed: ", e));

    return () => {
      isDisposed = true;
      if (groupName && hasJoinedGroupRef.current === groupName) {
        connection
          .invoke("LeaveGroup", groupName)
          .catch((e) => console.error("Error leaving group: ", e));
        hasJoinedGroupRef.current = null;
      }

      connection.off("ReceiveNotification", receiveNotificationHandler);
      connection
        .stop()
        .then(() => console.log("Connection stopped!"))
        .catch((e) => console.error("Error stopping the connection: ", e));
    };
  }, [connection, groupName]);

  const latestNotification =
    notifications.length > 0 ? notifications[notifications.length - 1] : null;
  const previousNotifications = notifications.slice(0, -1).reverse();

  if (hideNotifications) {
    return null;
  }

  if (!latestNotification && !isLoading) {
    return null;
  }

  return (
    <Space style={{ width: "100%" }} direction="vertical" size="middle">
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 12,
          width: "100%",
        }}
      >
        <Typography.Text strong>Progress Updates</Typography.Text>
        <Badge
          status={isLoading ? "processing" : "success"}
          text={isLoading ? "In progress" : "Complete"}
        />
      </div>

      {latestNotification && (
        <Alert
          message={latestNotification}
          type="info"
          style={{
            width: "100%",
            borderRadius: 14,
            borderWidth: 1,
            backgroundColor: "#eef6ff",
            boxShadow: "0 6px 18px rgba(15, 23, 42, 0.06)",
          }}
          showIcon
        />
      )}

      {previousNotifications.length > 0 && (
        <Space style={{ width: "100%" }} direction="vertical" size="small">
          {previousNotifications.map((notification, index) => (
            <Alert
              key={`${notification}-${index}`}
              message={notification}
              type="info"
              style={{
                width: "100%",
                borderRadius: 12,
                backgroundColor: "#f8fbff",
                opacity: 0.92,
              }}
              showIcon
            />
          ))}
        </Space>
      )}
    </Space>
  );
}

export { NotificationComponent };
