export const MAP_ENTRANCE_LAYER_SETTING_ID = "entrances";

export const formatEntranceDisplayLabel = (
  caveName: string,
  entranceName: string | null | undefined,
  isPrimary: boolean
) =>
  `${caveName}${
    entranceName && entranceName !== caveName ? ` (${entranceName})` : ""
  }${isPrimary ? " *" : ""}`;

export const ENTRANCE_LABEL_TEXT_FIELD = [
  "concat",
  ["get", "cavename"],
  [
    "case",
    ["all", ["has", "Name"], ["!=", ["get", "Name"], ""], ["!=", ["get", "Name"], ["get", "cavename"]]],
    ["concat", " (", ["get", "Name"], ")"],
    "",
  ],
  ["case", ["get", "IsPrimary"], " *", ""],
] as any;
