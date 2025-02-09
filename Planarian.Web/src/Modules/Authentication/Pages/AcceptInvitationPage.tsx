import { useContext, useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
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
  }, [invitation]);

  if (invitationCode === undefined) {
    throw new NotFoundError("invitationCode");
  }

  useEffect(() => {
    setHeaderTitle(["Invitation"]);
  }, []);
  useEffect(() => {
    const getInvitation = async () => {
      try {
        const res = await UserService.GetInvitation(invitationCode);
        setInvitation(res);
      } catch (err) {
        const error = err as ApiErrorResponse;
        message.error(error.message);
      } finally {
        setIsLoading(false);
      }
    };
    getInvitation();
  }, []);

  return (
    <>
      <InvitationComponent
        invitationCode={invitationCode}
        invitation={invitation}
        isLoading={isLoading}
        updateInvitation={async () => {
          setIsLoading(true);
          const updatedCave = await UserService.GetInvitation(invitationCode);
          setInvitation(updatedCave);
          setIsLoading(false);
        }}
      />
    </>
  );
};

export { AcceptInvitationPage };
