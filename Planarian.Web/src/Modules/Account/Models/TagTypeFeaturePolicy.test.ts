import { TagType } from "../../Tag/Models/TagType";
import { FeatureKey, FeatureSettingVm } from "./FeatureSettingVm";
import { isTagTypeEnabled } from "./TagTypeFeaturePolicy";

const setting = (key: FeatureKey, isEnabled: boolean): FeatureSettingVm => ({
  id: key,
  key,
  isEnabled,
  isDefault: false,
});

test("keeps shared Other tag management available when only Entrance Other is enabled", () => {
  expect(
    isTagTypeEnabled(TagType.CaveOther, [
      setting(FeatureKey.EnabledFieldCaveOtherTags, false),
      setting(FeatureKey.EnabledFieldEntranceOtherTags, true),
    ])
  ).toBe(true);
});

test("keeps People management available when any People-backed field is enabled", () => {
  expect(
    isTagTypeEnabled(TagType.People, [
      setting(FeatureKey.EnabledFieldCaveReportedByNameTags, false),
      setting(FeatureKey.EnabledFieldEntranceReportedByNameTags, true),
      setting(FeatureKey.EnabledFieldCaveCartographerNameTags, false),
    ])
  ).toBe(true);
});

test("hides shared Other tag management when both backing fields are disabled", () => {
  expect(
    isTagTypeEnabled(TagType.CaveOther, [
      setting(FeatureKey.EnabledFieldCaveOtherTags, false),
      setting(FeatureKey.EnabledFieldEntranceOtherTags, false),
    ])
  ).toBe(false);
});

test("hides People management when every People-backed field is disabled", () => {
  expect(
    isTagTypeEnabled(TagType.People, [
      setting(FeatureKey.EnabledFieldCaveReportedByNameTags, false),
      setting(FeatureKey.EnabledFieldEntranceReportedByNameTags, false),
      setting(FeatureKey.EnabledFieldCaveCartographerNameTags, false),
      setting(FeatureKey.EnabledFieldCaveBiologyTags, true),
    ])
  ).toBe(false);
});

test("keeps file tag management available independently of feature settings", () => {
  expect(isTagTypeEnabled(TagType.File, [])).toBe(true);
});
