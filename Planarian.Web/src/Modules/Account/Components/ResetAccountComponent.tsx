import React from "react";
import { Card, Button, message, Typography } from "antd";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { AccountService } from "../Services/AccountService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { ConfirmationModalComponent } from "../../../Shared/Components/Validation/ConfirmationModalComponent";
import { NotificationComponent } from "../../Import/Components/NotificationComponent";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";

const ResetAccountComponent = () => {
  var userGroupPrefix = AuthenticationService.GetUserGroupPrefix();

  return (
    <Card title="Reset Account">
      <Typography.Paragraph>
        This action will permanently delete all your caves and related account
        data. This action is irreversible.
      </Typography.Paragraph>
      <ConfirmationModalComponent
        title={`Reset Account`}
        modalMessage={
          <>
            Are you positive you want to delete <b>ALL</b> cave data?! This is
            an irreversible action!
          </>
        }
        onConfirm={async () => {
          try {
            await AccountService.ResetAccount();
            message.success("everything is gone (:");
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
              <NotificationComponent
                groupName={`${userGroupPrefix}-DeleteAllCaves`}
                isLoading={true}
              ></NotificationComponent>
            </Card>
          </>
        }
      >
        Delete
      </ConfirmationModalComponent>
    </Card>
  );
};

export { ResetAccountComponent };
