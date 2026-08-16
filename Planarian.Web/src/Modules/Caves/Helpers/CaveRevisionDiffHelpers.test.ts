import { isCaveRevisionDiffEmpty } from "./CaveRevisionDiffHelpers";
import { CaveRevisionDiffVm } from "../Models/CaveRevisionVm";

const empty: CaveRevisionDiffVm = {
  scalars: [], addedTags: [], removedTags: [], addedEntrances: [], removedEntrances: [],
  changedEntrances: [], addedFiles: [], removedFiles: [], changedFiles: [], entranceChanges: [], fileChanges: [],
  addedLinePlots: [], removedLinePlots: [], changedLinePlots: [], linePlotChanges: [], referenceMetadataChanges: [],
};

it("recognizes an empty authoritative Cave diff", () => {
  expect(isCaveRevisionDiffEmpty(empty)).toBe(true);
  expect(isCaveRevisionDiffEmpty({ ...empty, addedFiles: ["staged-file"] })).toBe(false);
  expect(isCaveRevisionDiffEmpty({ ...empty, changedLinePlots: ["line-1"] })).toBe(false);
});
