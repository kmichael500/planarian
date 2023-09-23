import { useEffect, useState } from "react";
import { HubConnectionBuilder } from "@microsoft/signalr";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";

function NotificationComponent({ groupName }: { groupName: string }) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(
    null
  );
  const [notification, setNotification] = useState("");

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
            setNotification(message);
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
      <h3>Notifications</h3>
      <div>{notification}</div>
    </div>
  );
}

export default NotificationComponent;
