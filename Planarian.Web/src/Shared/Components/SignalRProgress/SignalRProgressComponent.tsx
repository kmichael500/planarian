import { useEffect, useState } from "react";
import { Alert, Space, Spin } from "antd";
import { useSignalRGroup } from "./useSignalRGroup";

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
  groupName: string | null;
  isLoading: boolean;
  onConnected?: () => void | Promise<void>;
  onNotification?: (notification: unknown) => void;
  hideNotifications?: boolean;
}) {
  const [notifications, setNotifications] = useState<string[]>([]);

  useEffect(() => {
    setNotifications([]);
  }, [groupName]);

  useSignalRGroup({
    groupName,
    onConnected,
    onNotification: (notification) => {
      onNotification?.(notification);
      const message = getNotificationMessage(notification);
      setNotifications((prev) => {
        const newArray = [...prev, message];
        if (newArray.length > 3) {
          newArray.shift();
        }

        return newArray;
      });
    },
  });

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
