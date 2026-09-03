import { useContext, useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { InvitationComponent } from "../Components/InvitationComponent";
import { UserService } from "../../User/UserService";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { message } from "antd";

const AcceptInvitationPage = () => {
  const [invitation, setInvitation] = useState<AcceptInvitationVm>();

  const [isLoading, setIsLoading] = useState<boolean>(true);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const { invitationCode } = useParams();

  useEffect(() => {
    setHeaderButtons([]);
  }, [setHeaderButtons]);

  if (invitationCode === undefined) {
    throw new NotFoundError("invitationCode");
  }

  useEffect(() => {
    setHeaderTitle(["Invitation"]);
  }, [setHeaderTitle]);
  useEffect(() => {
    let isCurrent = true;
    setInvitation(undefined);
    setIsLoading(true);

    const getInvitation = async () => {
      try {
        const res = await UserService.GetInvitation(invitationCode);
        if (!isCurrent) return;
        setInvitation(res);
      } catch (err) {
        if (!isCurrent) return;
        const error = err as ApiErrorResponse;
        message.error(error.message);
      } finally {
        if (isCurrent) {
          setIsLoading(false);
        }
      }
    };

    void getInvitation();

    return () => {
      isCurrent = false;
    };
  }, [invitationCode]);

  const currentInvitation =
    invitation?.invitationCode === invitationCode ? invitation : undefined;
  const isCurrentInvitationLoading =
    isLoading || (invitation != null && currentInvitation == null);

  return (
    <>
      <InvitationComponent
        key={invitationCode}
        invitationCode={invitationCode}
        invitation={currentInvitation}
        isLoading={isCurrentInvitationLoading}
      />
    </>
  );
};

export { AcceptInvitationPage };
