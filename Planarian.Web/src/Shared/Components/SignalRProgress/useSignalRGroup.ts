import { useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from "@microsoft/signalr";
import { AppOptions } from "../../Services/AppService";

interface UseSignalRGroupOptions {
  groupName: string | null;
  onConnected?: () => void | Promise<void>;
  onNotification?: (notification: unknown) => void;
}

function useSignalRGroup({
  groupName,
  onConnected,
  onNotification,
}: UseSignalRGroupOptions) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const groupNameRef = useRef<string | null>(groupName);
  const hasJoinedGroupRef = useRef<string | null>(null);
  const onConnectedRef = useRef<typeof onConnected>(onConnected);
  const onNotificationRef = useRef<typeof onNotification>(onNotification);

  useEffect(() => {
    groupNameRef.current = groupName;
  }, [groupName]);

  useEffect(() => {
    onConnectedRef.current = onConnected;
  }, [onConnected]);

  useEffect(() => {
    onNotificationRef.current = onNotification;
  }, [onNotification]);

  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl(AppOptions.signalrBaseUrl, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .build();

    newConnection.onreconnected(async () => {
      const currentGroupName = groupNameRef.current;
      if (!currentGroupName) {
        await onConnectedRef.current?.();
        return;
      }

      await newConnection.invoke("JoinGroup", currentGroupName);
      hasJoinedGroupRef.current = currentGroupName;
      await onConnectedRef.current?.();
    });

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (!connection) {
      return;
    }

    let isDisposed = false;

    const receiveNotificationHandler = (notification: unknown) => {
      onNotificationRef.current?.(notification);
    };

    connection.on("ReceiveNotification", receiveNotificationHandler);
    connection
      .start()
      .then(async () => {
        if (isDisposed) {
          return;
        }

        const currentGroupName = groupNameRef.current;
        if (currentGroupName) {
          await connection.invoke("JoinGroup", currentGroupName);
          hasJoinedGroupRef.current = currentGroupName;
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
        hasJoinedGroupRef.current &&
        connection.state === HubConnectionState.Connected
      ) {
        connection
          .invoke("LeaveGroup", hasJoinedGroupRef.current)
          .catch((e) => console.error("Error leaving group: ", e));
        hasJoinedGroupRef.current = null;
      }

      connection.off("ReceiveNotification", receiveNotificationHandler);
      connection
        .stop()
        .catch((e) => console.error("Error stopping the connection: ", e));
    };
  }, [connection]);

  useEffect(() => {
    if (!connection || connection.state !== HubConnectionState.Connected) {
      return;
    }

    const syncGroup = async () => {
      const previousGroupName = hasJoinedGroupRef.current;
      if (previousGroupName === groupName) {
        return;
      }

      if (previousGroupName) {
        await connection.invoke("LeaveGroup", previousGroupName);
      }

      if (groupName) {
        await connection.invoke("JoinGroup", groupName);
      }

      hasJoinedGroupRef.current = groupName;
      await onConnectedRef.current?.();
    };

    syncGroup().catch((e) =>
      console.error("Error syncing SignalR group membership: ", e)
    );
  }, [connection, groupName]);
}

export { useSignalRGroup };
