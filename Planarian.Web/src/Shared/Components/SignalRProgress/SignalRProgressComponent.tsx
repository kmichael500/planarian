import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { AppOptions } from "../../Services/AppService";
import { AuthenticationService } from "../../../Modules/Authentication/Services/AuthenticationService";
import { Alert, Space, Spin } from "antd";

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

function SignalRProgressComponent({
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
  const groupNameRef = useRef<string>(groupName);
  const hasJoinedGroupRef = useRef<string | null>(null);
  const onConnectedRef = useRef<typeof onConnected>(onConnected);

  useEffect(() => {
    onConnectedRef.current = onConnected;
  }, [onConnected]);

  useEffect(() => {
    groupNameRef.current = groupName;
  }, [groupName]);

  useEffect(() => {
    setNotifications([]);
  }, [groupName]);

  useEffect(() => {
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

    newConnection.onreconnected(async () => {
      const currentGroupName = groupNameRef.current;
      if (!currentGroupName) {
        return;
      }

      await newConnection.invoke("JoinGroup", currentGroupName);
      hasJoinedGroupRef.current = currentGroupName;
    });

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      let isDisposed = false;

      const receiveNotificationHandler = (notification: unknown) => {
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
        .catch(async (e) => {
          console.error("Connection failed: ", e);
          await onConnectedRef.current?.();
        });

      return () => {
        isDisposed = true;

        if (
          groupName &&
          hasJoinedGroupRef.current === groupName &&
          connection.state === HubConnectionState.Connected
        ) {
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
    }
  }, [connection, groupName]);

  if (hideNotifications) {
    return null;
  }

  return (
    <div>
      <Spin spinning={isLoading} />
      <Space style={{ width: "100%" }} direction="vertical" size="large">
        {notifications.map((notification, index) => (
          <Alert
            key={index}
            message={notification}
            type="info"
            style={{
              width: "100%",
              ...(index === notifications.length - 1
                ? { backgroundColor: "#e6f7ff", fontWeight: "bold" }
                : {}),
            }}
            showIcon
          />
        ))}
      </Space>
    </div>
  );
}

export { SignalRProgressComponent };
