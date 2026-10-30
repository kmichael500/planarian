import dayjs from "dayjs";
import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";
import { HttpClient } from "../../../Shared/Http/HttpClient";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "./CaveService";
import { FileAccessAction } from "../../Files/Services/FileService";

it("builds request and authoring staged-file view URLs through the authenticated API helper", () => {
  const builder = jest.spyOn(HttpHelpers, "BuildAuthenticatedApiUrl")
    .mockReturnValue("https://api.example.test/file?account_id=account-a");

  expect(CaveService.GetStagedChangeRequestFileUrl("request/id", "file id", FileAccessAction.View)).toBe(
    "https://api.example.test/file?account_id=account-a"
  );
  expect(builder).toHaveBeenLastCalledWith(
    "api/cave-change-requests/request%2Fid/files/file%20id/view"
  );
  expect(CaveService.GetAuthoringStagedFileUrl("file id", FileAccessAction.View)).toBe(
    "https://api.example.test/file?account_id=account-a"
  );
  expect(builder).toHaveBeenLastCalledWith("api/files/staged/file%20id/view");
});

it("requests bounded Mine and Review pages", async () => {
  const response = { data: { pageNumber: 2, pageSize: 5, totalCount: 0, totalPages: 0, results: [] } };
  const get = jest.spyOn(HttpClient, "get").mockResolvedValue(response);

  await CaveService.GetMyChangeRequests(2, 5);
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/mine?pageNumber=2&pageSize=5&sortBy=UpdatedOn&sortDescending=true");
  await CaveService.GetMyChangeRequests(1, 10, "Approved");
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/mine?pageNumber=1&pageSize=10&status=Approved&sortBy=UpdatedOn&sortDescending=true");
  await CaveService.GetReviewQueue(3, 7);
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/review?pageNumber=3&pageSize=7&sortBy=SubmittedOn&sortDescending=false");
  await CaveService.GetReviewQueue(1, 10, "Approved", "CaveName", true);
  expect(get).toHaveBeenLastCalledWith("api/cave-change-requests/review?pageNumber=1&pageSize=10&status=Approved&sortBy=CaveName&sortDescending=true");
});

it("loads the dedicated Cave edit authoring context", async () => {
  const get = jest.spyOn(HttpClient, "get").mockResolvedValue({ data: { cave: { id: "cave-a" }, linePlots: [] } });

  await CaveService.GetEditAuthoringContext("cave-a");

  expect(get).toHaveBeenLastCalledWith("api/caves/cave-a/edit-context");
});

it("includes existing Cave context in county-number helper requests", async () => {
  const get = jest.spyOn(HttpClient, "get").mockResolvedValue({ data: 43 });

  await CaveService.GetNextCountyNumber("county-a", true, "cave-a");
  expect(get).toHaveBeenLastCalledWith(
    "api/caves/counties/county-a/next-number?useFirstAvailableCountyNumber=true&caveId=cave-a"
  );

  await CaveService.GetNextCountyNumber("county-a");
  expect(get).toHaveBeenLastCalledWith(
    "api/caves/counties/county-a/next-number?useFirstAvailableCountyNumber=false"
  );
});

it("stages an authoring file through the generic file endpoint", async () => {
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({
    data: { id: "file-a", name: "map", extension: ".pdf", fileTypeTagId: "type-a", fileTypeKey: "Map" },
  });
  const upload = new File(["bytes"], "map.pdf", { type: "application/pdf" });

  await CaveService.StageAuthoringFile(upload, "uuid/a", () => undefined);

  expect(post).toHaveBeenCalledWith(
    "api/files/staged?uuid=uuid%2Fa",
    expect.any(FormData),
    expect.objectContaining({ headers: { "Content-Type": "multipart/form-data" } })
  );
});

it("posts request-changes and resubmit decisions against the expected proposal version", async () => {
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: { result: "ChangesRequested" } });

  await CaveService.RequestChangeRequestChanges("request-a", "version-a", "Please verify this.");
  expect(post).toHaveBeenLastCalledWith("api/cave-change-requests/request-a/request-changes",
    { notes: "Please verify this.", expectedProposalVersionId: "version-a" });

  await CaveService.ResubmitChangeRequest("request-a", "version-b");
  expect(post).toHaveBeenLastCalledWith("api/cave-change-requests/request-a/resubmit",
    { expectedProposalVersionId: "version-b" });
});

const completeAuthoringPayload = (): AddCaveVm => ({
  id: "cave-a",
  expectedRevisionId: "revision-a",
  name: "Complete Cave",
  alternateNames: ["Alias A"],
  countyId: "county-a",
  stateId: "state-a",
  countyDisplayId: "A01",
  countyNumber: 42,
  isCountyNumberManuallySet: true,
  useFirstAvailableCountyNumber: false,
  lengthFeet: null,
  depthFeet: 0,
  maxPitDepthFeet: 12.5,
  numberOfPits: null,
  narrative: null,
  reportedOn: dayjs("2026-01-02T03:04:05Z"),
  entrances: [{
    id: "entrance-a",
    isPrimary: true,
    locationQualityTagId: "quality-a",
    name: null,
    description: null,
    latitude: 35.1,
    longitude: -86.2,
    elevationFeet: 0,
    reportedOn: null,
    pitFeet: null,
    entranceStatusTagIds: [],
    fieldIndicationTagIds: [],
    entranceHydrologyTagIds: [],
    entranceOtherTagIds: [],
    reportedByNameTagIds: [],
  }],
  geologyTagIds: [],
  files: [{
    id: "file-a",
    name: "Survey",
    extension: ".pdf",
    fileTypeTagId: "file-type-a",
    fileTypeKey: "Survey",
  }],
  linePlots: [{
    id: "line-a",
    name: "Traverse",
    geoJson: "{\"type\":\"FeatureCollection\",\"features\":[]}",
  }],
  reportedByNameTagIds: [],
  biologyTagIds: [],
  archeologyTagIds: [],
  cartographerNameTagIds: [],
  mapStatusTagIds: [],
  geologicAgeTagIds: [],
  physiographicProvinceTagIds: [],
  otherTagIds: [],
});

it("forwards the complete authoring payload when creating a Cave", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: "cave-created" });

  await CaveService.AddCave(cave);

  expect(post).toHaveBeenCalledWith("api/caves", cave);
});

it("forwards the complete authoring payload when previewing and submitting a new-Cave proposal", async () => {
  const cave = completeAuthoringPayload();
  cave.id = undefined;
  cave.expectedRevisionId = undefined;
  const post = jest.spyOn(HttpClient, "post")
    .mockResolvedValueOnce({ data: {} })
    .mockResolvedValueOnce({ data: "request-a" });

  await CaveService.PreviewNewCave(cave);
  expect(post).toHaveBeenLastCalledWith("api/cave-change-requests/caves/new/preview", cave);

  await CaveService.SubmitNewCave(cave);
  expect(post).toHaveBeenLastCalledWith("api/cave-change-requests/caves/new", cave);
});

it("forwards the complete authoring payload when directly editing a Cave", async () => {
  const cave = completeAuthoringPayload();
  const put = jest.spyOn(HttpClient, "put").mockResolvedValue({ data: undefined });

  await CaveService.UpdateCave(cave);

  expect(put).toHaveBeenCalledWith("api/caves/", cave);
});

it("wraps the complete authoring payload when previewing a new change request", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: {} });

  await CaveService.PreviewChanges("cave-a", cave, "revision-a");

  expect(post).toHaveBeenCalledWith("api/cave-change-requests/caves/cave-a/preview",
    { cave, expectedBaseRevisionId: "revision-a" });
});

it("wraps the complete authoring payload when submitting a new change request", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: "request-a" });

  await CaveService.SubmitChanges("cave-a", cave, "revision-a");

  expect(post).toHaveBeenCalledWith("api/cave-change-requests/caves/cave-a",
    { cave, expectedBaseRevisionId: "revision-a" });
});

it("wraps the complete authoring payload when previewing a revised proposal", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: {} });

  await CaveService.PreviewRevisedChanges("request-a", cave, true, "revision-b", "proposal-a");

  expect(post).toHaveBeenCalledWith("api/cave-change-requests/request-a/versions/preview", {
    cave,
    againstCurrent: true,
    expectedBaseRevisionId: "revision-b",
    expectedProposalVersionId: "proposal-a",
  });
});

it("wraps the complete authoring payload when saving a revised proposal", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: "proposal-b" });

  await CaveService.ReviseChanges("request-a", cave, false, "revision-a", "proposal-a");

  expect(post).toHaveBeenCalledWith("api/cave-change-requests/request-a/versions", {
    cave,
    againstCurrent: false,
    expectedBaseRevisionId: "revision-a",
    expectedProposalVersionId: "proposal-a",
  });
});

it("preserves a null published base when revising a new-Cave proposal", async () => {
  const cave = completeAuthoringPayload();
  const post = jest.spyOn(HttpClient, "post").mockResolvedValue({ data: "proposal-b" });

  await CaveService.ReviseChanges("request-a", cave, false, undefined, "proposal-a");

  expect(post).toHaveBeenCalledWith("api/cave-change-requests/request-a/versions", {
    cave,
    againstCurrent: false,
    expectedBaseRevisionId: undefined,
    expectedProposalVersionId: "proposal-a",
  });
});

it("serializes null, zero, empty collections, dates, and aggregate identities without changing intent", () => {
  const wire = JSON.parse(JSON.stringify(completeAuthoringPayload()));

  expect(wire).toMatchObject({
    expectedRevisionId: "revision-a",
    lengthFeet: null,
    depthFeet: 0,
    numberOfPits: null,
    narrative: null,
    reportedOn: "2026-01-02T03:04:05.000Z",
    biologyTagIds: [],
    files: [{ id: "file-a", name: "Survey", extension: ".pdf", fileTypeTagId: "file-type-a" }],
    linePlots: [{ id: "line-a", name: "Traverse" }],
    entrances: [{
      id: "entrance-a",
      name: null,
      description: null,
      reportedOn: null,
      pitFeet: null,
      entranceStatusTagIds: [],
      entranceOtherTagIds: [],
    }],
  });
  expect(wire).toHaveProperty("files");
  expect(wire).toHaveProperty("linePlots");
  expect(wire).toHaveProperty("biologyTagIds");
});
