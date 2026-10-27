import { HttpClient } from "../../../Shared/Http/HttpClient";

const baseUrl = "api/leads";
const LeadService = {
  //#region Leads

  async DeleteLead(leadId: string): Promise<void> {
    await HttpClient.delete<void>(`${baseUrl}/${leadId}`);
  },

  //#endregion
};
export { LeadService };
