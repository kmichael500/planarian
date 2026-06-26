import { serialize } from "object-to-formdata";
import { baseUrl } from "../..";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";

export const HttpHelpers = {
  ToFormData(data: any): FormData {
    var formData = serialize(data);
    return formData;
  },

  BuildAuthenticatedApiUrl(path: string): string {
    const normalizedBaseUrl = baseUrl ?? window.location.origin;
    const normalizedPath = path.startsWith("/") ? path : `/${path}`;
    const url = new URL(normalizedPath, normalizedBaseUrl);
    const accountId = AuthenticationService.GetAccountId();

    if (accountId) {
      url.searchParams.set("account_id", accountId);
    }

    return url.toString();
  },

  NavigateToApiUrl(path: string): void {
    window.location.assign(HttpHelpers.BuildAuthenticatedApiUrl(path));
  },
};
