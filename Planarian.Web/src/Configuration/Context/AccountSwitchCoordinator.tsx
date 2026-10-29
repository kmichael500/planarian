import React, { useContext, useLayoutEffect, useRef, useState } from "react";
import { message, Spin } from "antd";
import { useNavigate } from "react-router-dom";
import { AppContext } from "./AppContext";
import {
  ACCOUNT_SWITCH_EVENT,
  AccountSwitchRequest,
} from "../../Modules/Authentication/Services/AuthenticationService";

interface AccountSwitchCoordinatorProps {
  children: React.ReactNode;
}

export const AccountSwitchCoordinator: React.FC<AccountSwitchCoordinatorProps> = ({
  children,
}) => {
  const { currentAccountId, refreshSession } = useContext(AppContext);
  const navigate = useNavigate();
  const [isSwitching, setIsSwitching] = useState(false);
  const latestSequence = useRef(0);
  const switchQueue = useRef<Promise<void>>(Promise.resolve());

  useLayoutEffect(() => {
    const handleAccountSwitch = (event: Event) => {
      const request = (event as CustomEvent<AccountSwitchRequest>).detail;
      if (!request?.accountId) {
        return;
      }

      const sequence = ++latestSequence.current;
      setIsSwitching(true);

      switchQueue.current = switchQueue.current
        .catch(() => undefined)
        .then(async () => {
          if (sequence !== latestSequence.current) {
            return;
          }

          await refreshSession(request.accountId);

          if (sequence === latestSequence.current) {
            navigate(request.redirectPath, { replace: true });
          }
        })
        .catch(() => {
          if (sequence === latestSequence.current) {
            message.error("The account could not be switched. Your previous account is still active.");
          }
        })
        .finally(() => {
          if (sequence === latestSequence.current) {
            setIsSwitching(false);
          }
        });
    };

    window.addEventListener(ACCOUNT_SWITCH_EVENT, handleAccountSwitch);
    return () => {
      window.removeEventListener(ACCOUNT_SWITCH_EVENT, handleAccountSwitch);
    };
  }, [navigate, refreshSession]);

  if (isSwitching) {
    return (
      <div
        role="status"
        aria-label="Switching accounts"
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          height: "100%",
        }}
      >
        <Spin size="large" />
      </div>
    );
  }

  return (
    <React.Fragment key={currentAccountId ?? "anonymous"}>
      {children}
    </React.Fragment>
  );
};
