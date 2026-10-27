import { useContext, useEffect, useState } from "react";
import { Button, Card, message, Typography } from "antd";
import { AccountService } from "../Services/AccountService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { ConfirmationModalComponent } from "../../../Shared/Components/Validation/ConfirmationModalComponent";
import { SignalRProgressComponent } from "../../../Shared/Components/SignalRProgress/SignalRProgressComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";

const ResetAccountComponent = () => {
  const { userGroupPrefix } = useContext(AppContext);
  const [cleanupIncomplete, setCleanupIncomplete] = useState(false);
  const [retrying, setRetrying] = useState(false);

  useEffect(() => {
    let active = true;
    AccountService.HasPendingResetCleanup()
      .then((pending) => { if (active) setCleanupIncomplete(pending); })
      .catch(() => undefined);
    return () => { active = false; };
  }, []);

  return (
    <Card title="Reset Account">
      <Typography.Paragraph>
        This action will permanently delete all your caves and related account
        data, including Files and stored account archives. This action is irreversible.
        Download any archive you wish to keep before resetting. Account members
        and account-level preferences are retained.
      </Typography.Paragraph>
      <ConfirmationModalComponent
        autoClose
        title={`Reset Account`}
        modalMessage={
          <>
            Are you positive you want to delete <b>ALL</b> cave data?! This is
            an irreversible action! Stored account archives will also be deleted.
          </>
        }
        onConfirm={async () => {
          try {
            const result = await AccountService.ResetAccount();
            if (!result.relationalResetCompleted) {
              throw new Error("The server did not confirm the account reset.");
            }
            setCleanupIncomplete(!result.storageCleanupCompleted);
            if (result.storageCleanupCompleted) {
              message.success("Account reset finished; storage accepted the cleanup requests.");
            } else {
              message.warning(
                "Account data was reset, but stored-file cleanup is incomplete. Retry cleanup before adding new data."
              );
            }
          } catch (e) {
            const error = e as ApiErrorResponse;
            message.error(error.message);
          }
        }}
        okText="Yes"
        cancelText="No"
        confirmationWord={"DELETE EVERYTHING"}
        onOkClickRender={
          <>
            <Card>
              <SignalRProgressComponent
                groupName={
                  userGroupPrefix ? `${userGroupPrefix}-DeleteAllCaves` : null
                }
                isLoading={true}
              ></SignalRProgressComponent>
            </Card>
          </>
        }
      >
        Delete
      </ConfirmationModalComponent>
      {cleanupIncomplete && (
        <Button
          loading={retrying}
          onClick={async () => {
            setRetrying(true);
            try {
              const result = await AccountService.RetryResetCleanup();
              setCleanupIncomplete(!result.storageCleanupCompleted);
              if (result.storageCleanupCompleted) {
                message.success("Remaining storage cleanup requests were accepted.");
              } else {
                message.warning("Stored-file cleanup remains incomplete. Retry again or contact support.");
              }
            } catch (e) {
              message.error((e as ApiErrorResponse).message);
            } finally {
              setRetrying(false);
            }
          }}
        >
          Retry stored-file cleanup
        </Button>
      )}
    </Card>
  );
};

export { ResetAccountComponent };
