import { useEffect, useState } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { Alert, List, Space, Spin, Typography } from "antd";

function NotificationComponent({
  groupName,
  isLoading,
}: {
  groupName: string;
  isLoading: boolean;
}) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(
    null
  );
  const [notifications, setNotifications] = useState<string[]>([]);

  useEffect(() => {
    // Build the connection
    const newConnection = new HubConnectionBuilder()
      .withUrl(AppOptions.signalrBaseUrl, {
        accessTokenFactory: () => {
          console.log("Getting token");
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
    if (connection) {
      connection
        .start()
        .then(() => {
          console.log("Connection started!");
          if (groupName) {
            connection
              .invoke("JoinGroup", groupName)
              .catch((err) => console.error(err));
          }
          // Registering the ReceiveNotification method to get notifications
          connection.on("ReceiveNotification", (message) => {
            console.log("Received:", message);
            setNotifications((prev) => {
              // Maintaining only the last 3 notifications
              const newArray = [...prev, message];
              if (newArray.length > 3) newArray.shift();
              return newArray;
            });
          });
        })
        .catch((e) => console.log("Connection failed: ", e));
    }

    // Clean up the connection on component unmount
    return () => {
      if (connection) {
        connection
          .stop()
          .then(() => console.log("Connection stopped!"))
          .catch((e) => console.log("Error stopping the connection: ", e));
      }
    };
  }, [connection, groupName]);

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

export { NotificationComponent };
