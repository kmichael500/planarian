import { CaveRevisionDiffVm } from "../Models/CaveRevisionVm";

export const isCaveRevisionDiffEmpty = (diff: CaveRevisionDiffVm): boolean =>
  diff.scalars.length === 0 &&
  diff.addedTags.length === 0 &&
  diff.removedTags.length === 0 &&
  diff.addedEntrances.length === 0 &&
  diff.removedEntrances.length === 0 &&
  diff.changedEntrances.length === 0 &&
  diff.addedFiles.length === 0 &&
  diff.removedFiles.length === 0 &&
  diff.changedFiles.length === 0 &&
  diff.referenceMetadataChanges.length === 0;
