import { serialize } from "object-to-formdata";
import { getApiBaseUrl } from "../Http/HttpClient";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";

export const HttpHelpers = {
  ToFormData(data: any): FormData {
    var formData = serialize(data);
    return formData;
  },

  GetLocalRedirectUrl(redirectUrl: string | null): string {
    if (!redirectUrl?.trim()) return "/";

    try {
      const resolvedUrl = new URL(redirectUrl, window.location.origin);
      if (
        !redirectUrl.startsWith("/") ||
        resolvedUrl.origin !== window.location.origin
      ) {
        return "/";
      }

      return `${resolvedUrl.pathname}${resolvedUrl.search}${resolvedUrl.hash}`;
    } catch {
      return "/";
    }
  },

  GetSafeExternalHttpUrl(url: string | null | undefined): string | null {
    if (!url?.trim()) return null;

    try {
      const parsedUrl = new URL(url);
      return parsedUrl.protocol === "http:" || parsedUrl.protocol === "https:"
        ? parsedUrl.toString()
        : null;
    } catch {
      return null;
    }
  },

  BuildAuthenticatedApiUrl(path: string): string {
    const normalizedBaseUrl = getApiBaseUrl() ?? window.location.origin;
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
