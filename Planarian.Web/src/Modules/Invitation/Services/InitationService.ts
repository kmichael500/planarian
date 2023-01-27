import { HttpClient } from "../../..";
import { MemberGridType } from "../../../Shared/Components/MemberGridComponent";
import { InviteMember as InviteMember } from "../../../Shared/Models/InviteMember";

const baseUrl = "api/invitations";
const InvitationService = {
  async InviteMember(
    values: InviteMember,
    type: MemberGridType
  ): Promise<void> {
    const response = await HttpClient.post<void>(`${baseUrl}/`, values);
    return response.data;
  },
};

export { InvitationService };
