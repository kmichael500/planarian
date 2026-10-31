import { HttpClient } from "../../../Shared/Http/HttpClient";

const baseUrl = "api/leads";
const LeadService = {

  async DeleteLead(leadId: string): Promise<void> {
    await HttpClient.delete<void>(`${baseUrl}/${leadId}`);
  },

};
export { LeadService };
