import { HttpClient } from "../../..";

const baseUrl = "api/leads";
const LeadService = {
  //#region Leads

  async DeleteLead(leadId: string): Promise<void> {
    const response = await HttpClient.delete<void>(`${baseUrl}/${leadId}`);
  },

  //#endregion
};
export { LeadService };
