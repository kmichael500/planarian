import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";
import { HttpClient } from "../../../Shared/Http/HttpClient";
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

it("requests bounded Mine and Review pages", async () => {
  const response = { data: { pageNumber: 2, pageSize: 5, totalCount: 0, totalPages: 0, results: [] } };
  const get = jest.spyOn(HttpClient, "get").mockResolvedValue(response);

  await CaveService.GetMyChangeRequests(2, 5);
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/mine?pageNumber=2&pageSize=5");
  await CaveService.GetReviewQueue(3, 7);
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/review?pageNumber=3&pageSize=7");
});

it("loads the dedicated Cave edit authoring context", async () => {
  const get = jest.spyOn(HttpClient, "get").mockResolvedValue({ data: { cave: { id: "cave-a" }, linePlots: [] } });

  await CaveService.GetEditAuthoringContext("cave-a");

  expect(get).toHaveBeenLastCalledWith("api/caves/cave-a/edit-context");
});

it("stages an authoring file through the generic file endpoint", async () => {
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({
    data: { id: "file-a", fileName: "map.pdf", displayName: "map", fileTypeTagId: "type-a", fileTypeKey: "Map" },
  });
  const upload = new File(["bytes"], "map.pdf", { type: "application/pdf" });

  await CaveService.StageAuthoringFile(upload, "uuid/a", () => undefined);

  expect(post).toHaveBeenCalledWith(
    "api/files/staged?uuid=uuid%2Fa",
    expect.any(FormData),
    expect.objectContaining({ headers: { "Content-Type": "multipart/form-data" } })
  );
});
