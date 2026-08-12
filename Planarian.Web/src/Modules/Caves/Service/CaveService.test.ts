import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";
import { CaveService } from "./CaveService";

it("builds staged-file browser URLs through the authenticated API helper", () => {
  const builder = jest.spyOn(HttpHelpers, "BuildAuthenticatedApiUrl")
    .mockReturnValue("https://api.example.test/download?account_id=account-a");

  expect(CaveService.GetStagedChangeRequestFileUrl("request/id", "file id")).toBe(
    "https://api.example.test/download?account_id=account-a"
  );
  expect(builder).toHaveBeenCalledWith(
    "api/cave-change-requests/request%2Fid/files/file%20id"
  );
});
