import React, { useEffect, useState } from "react";
import { Timeline, Spin, Tag, Typography } from "antd";
import { CaveService } from "../Service/CaveService";
import {
  CaveHistory,
  HistoryDetail,
  CaveLogPropertyName,
  ChangeType,
} from "../Models/ProposedChangeRequestVm";
import {
  formatDateTime,
  formatCoordinate,
  formatDistance,
  DistanceFormat,
  formatBoolean,
  formatNumber,
  defaultIfEmpty,
  isNullOrWhiteSpace,
  toCommaString,
  formatDate,
} from "../../../Shared/Helpers/StringHelpers";
import { UserAvatarComponent } from "../../User/Componenets/UserAvatarComponent";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { ChangeRequestType } from "../Models/ChangeRequestType";

// ——— display‐name mapping ———
const getDisplayName = (propertyName: string): string => {
  switch (propertyName) {
    // Cave properties
    case CaveLogPropertyName.Name:
      return "Name";
    case CaveLogPropertyName.CountyName:
      return "County";
    case CaveLogPropertyName.StateName:
      return "State";
    case CaveLogPropertyName.AlternateNames:
      return "Alternative Names";
    case CaveLogPropertyName.LengthFeet:
      return "Length";
    case CaveLogPropertyName.DepthFeet:
      return "Depth";
    case CaveLogPropertyName.MaxPitDepthFeet:
      return "Max Pit Depth";
    case CaveLogPropertyName.NumberOfPits:
      return "Number of Pits";
    case CaveLogPropertyName.Narrative:
      return "Narrative";
    case CaveLogPropertyName.ReportedOn:
      return "Reported On";
    case CaveLogPropertyName.GeologyTagName:
      return "Geology";
    case CaveLogPropertyName.MapStatusTagName:
      return "Map Status";
    case CaveLogPropertyName.GeologicAgeTagName:
      return "Geologic Age";
    case CaveLogPropertyName.PhysiographicProvinceTagName:
      return "Physiographic Province";
    case CaveLogPropertyName.BiologyTagName:
      return "Biology";
    case CaveLogPropertyName.ArcheologyTagName:
      return "Archeology";
    case CaveLogPropertyName.CartographerNameTagName:
      return "Cartographers";
    case CaveLogPropertyName.ReportedByNameTagName:
      return "Reported By";
    case CaveLogPropertyName.OtherTagName:
      return "Other";

    // Entrance properties
    case CaveLogPropertyName.EntranceName:
      return "Entrance Name";
    case CaveLogPropertyName.EntranceLatitude:
      return "Latitude";
    case CaveLogPropertyName.EntranceLongitude:
      return "Longitude";
    case CaveLogPropertyName.EntranceElevationFeet:
      return "Elevation";
    case CaveLogPropertyName.EntranceDescription:
      return "Description";
    case CaveLogPropertyName.EntranceIsPrimary:
      return "Primary Entrance";
    case CaveLogPropertyName.EntranceLocationQualityTagName:
      return "Location Quality";
    case CaveLogPropertyName.EntrancePitDepthFeet:
      return "Pit Depth";
    case CaveLogPropertyName.EntranceReportedOn:
      return "Entrance Reported On";
    case CaveLogPropertyName.EntranceStatusTagName:
      return "Status";
    case CaveLogPropertyName.EntranceHydrologyTagName:
      return "Hydrology";
    case CaveLogPropertyName.EntranceFieldIndicationTagName:
      return "Field Indication";
    case CaveLogPropertyName.EntranceReportedByNameTagName:
      return "Entrance Reported By";

    default:
      return propertyName;
  }
};

// Only Add/Delete now; Update falls through to default color/display
const getChangeTypeColor = (t: ChangeType): string => {
  switch (t) {
    case ChangeType.Add:
      return "success";
    case ChangeType.Delete:
      return "error";
    default:
      return "default";
  }
};

const getChangeTypeDisplay = (t: ChangeType): string => {
  switch (t) {
    case ChangeType.Add:
      return "Added";
    case ChangeType.Delete:
      return "Deleted";
    default:
      return "";
  }
};

const ExpandableNarrative: React.FC<{
  text: string | undefined;
  style?: React.CSSProperties;
}> = ({ text, style }) => {
  const [expanded, setExpanded] = useState(false);

  if (!text) {
    return null;
  }

  const isLongText = text.length > 500;
  const displayedText =
    expanded || !isLongText ? text : text.slice(0, 500) + "...";

  return (
    <div>
      <ParagraphDisplayComponent
        text={displayedText}
        style={{ margin: 0, ...style }}
      />
      {isLongText && (
        <PlanarianButton
          style={{ padding: 0 }}
          onClick={() => setExpanded(!expanded)}
          alwaysShowChildren
          type="link"
          icon={undefined}
        >
          {expanded ? "Show Less" : "Show More"}
        </PlanarianButton>
      )}
    </div>
  );
};

// Explicitly handle every property
const renderDetail = (d: HistoryDetail, entry: CaveHistory) => {
  let newValRaw: string | null | React.ReactNode = null;
  let prevValRaw: string | null | React.ReactNode = null;

  let showProperty = true;

  switch (d.propertyName) {
    case CaveLogPropertyName.Name:
    case CaveLogPropertyName.CountyName:
    case CaveLogPropertyName.StateName:
    case CaveLogPropertyName.EntranceName:
    case CaveLogPropertyName.EntranceDescription:
    case CaveLogPropertyName.EntranceLocationQualityTagName:
    case CaveLogPropertyName.EntranceStatusTagName:
      newValRaw = d.valueString;
      prevValRaw = d.previousValueString;
      break;

    case CaveLogPropertyName.AlternateNames:
      newValRaw = d.valueStrings?.join(", ") ?? null;
      prevValRaw = d.previousValueStrings?.join(", ") ?? null;
      break;

    case CaveLogPropertyName.LengthFeet:
    case CaveLogPropertyName.DepthFeet:
    case CaveLogPropertyName.MaxPitDepthFeet:
      newValRaw = formatDistance(d.valueDouble ?? d.valueInt ?? undefined);
      prevValRaw = formatDistance(
        d.previousValueDouble ?? d.previousValueInt ?? undefined
      );
      break;

    case CaveLogPropertyName.Narrative:
      newValRaw = <ExpandableNarrative text={d.valueString || undefined} />;
      prevValRaw = d.previousValueString ? (
        <ExpandableNarrative
          text={d.previousValueString || undefined}
          style={{ color: "#888" }}
        />
      ) : null;
      break;

    case CaveLogPropertyName.NumberOfPits:
      newValRaw = formatNumber(d.valueInt);
      prevValRaw = formatNumber(d.previousValueInt);
      break;

    case CaveLogPropertyName.ReportedOn:
    case CaveLogPropertyName.EntranceReportedOn:
      newValRaw = formatDate(d.valueDateTime);
      prevValRaw = formatDate(d.previousValueDateTime);
      break;

    case CaveLogPropertyName.EntranceLatitude:
    case CaveLogPropertyName.EntranceLongitude:
      newValRaw = formatCoordinate(
        d.valueDouble ?? d.valueInt ?? parseFloat(d.valueString ?? "")
      );
      prevValRaw = formatCoordinate(
        d.previousValueDouble ??
          d.previousValueInt ??
          parseFloat(d.previousValueString ?? "")
      );
      break;

    case CaveLogPropertyName.EntranceElevationFeet:
    case CaveLogPropertyName.EntrancePitDepthFeet:
      newValRaw = formatDistance(
        d.valueDouble ?? d.valueInt ?? undefined,
        DistanceFormat.feet
      );
      prevValRaw = formatDistance(
        d.previousValueDouble ?? d.previousValueInt ?? undefined,
        DistanceFormat.feet
      );
      break;

    case CaveLogPropertyName.EntranceIsPrimary:
      newValRaw = formatBoolean(d.valueBool);
      prevValRaw = formatBoolean(d.previousValueBool);
      break;

    case CaveLogPropertyName.GeologyTagName:
    case CaveLogPropertyName.MapStatusTagName:
    case CaveLogPropertyName.GeologicAgeTagName:
    case CaveLogPropertyName.PhysiographicProvinceTagName:
    case CaveLogPropertyName.BiologyTagName:
    case CaveLogPropertyName.ArcheologyTagName:
    case CaveLogPropertyName.CartographerNameTagName:
    case CaveLogPropertyName.ReportedByNameTagName:
    case CaveLogPropertyName.OtherTagName:
    case CaveLogPropertyName.EntranceHydrologyTagName:
    case CaveLogPropertyName.EntranceFieldIndicationTagName:
    case CaveLogPropertyName.EntranceReportedByNameTagName:
      if (
        entry.type === ChangeRequestType.Rename ||
        entry.type === ChangeRequestType.Merge ||
        entry.type === ChangeRequestType.Delete
      ) {
        const requestTypeDisplay =
          entry.type == ChangeRequestType.Rename
            ? "renamed to"
            : entry.type == ChangeRequestType.Merge
            ? "merged into"
            : entry.type == ChangeRequestType.Delete
            ? "Deleted"
            : entry.type;
        newValRaw =
          entry.type == ChangeRequestType.Delete
            ? `${requestTypeDisplay} '${d.previousValueString}'`
            : `'${d.previousValueString ?? ""}' ${requestTypeDisplay} '${
                d.valueString ?? ""
              }'`;
        // prevValRaw = d.previousValueString;
        break;
      }
      newValRaw = toCommaString(d.valueStrings) ?? null;
      prevValRaw = toCommaString(d.previousValueStrings) ?? null;

      break;

    case CaveLogPropertyName.Entrance:
      showProperty = false;
      break;

    default:
      newValRaw = "Not implemented";
      prevValRaw = "Not implemented";
  }

  const newVal =
    typeof newValRaw === "string" ||
    newValRaw === null ||
    newValRaw === undefined
      ? defaultIfEmpty(newValRaw)
      : newValRaw;

  const showPrev =
    typeof prevValRaw === "string"
      ? !isNullOrWhiteSpace(prevValRaw)
      : prevValRaw !== null && prevValRaw !== undefined;

  const prevVal = showPrev
    ? typeof prevValRaw === "string" ||
      prevValRaw === null ||
      prevValRaw === undefined
      ? defaultIfEmpty(prevValRaw)
      : prevValRaw
    : null;

  if (showProperty) {
    return (
      <div style={{ marginBottom: 4, marginLeft: 16 }}>
        <strong>{getDisplayName(d.propertyName)}:</strong> {newVal}
        {showPrev && (
          <div style={{ color: "#888", fontSize: 12 }}>Previous: {prevVal}</div>
        )}
      </div>
    );
  }
};

const ChangeHeader = ({ entry }: { entry: CaveHistory }) => {
  const requestTypeDisplay =
    entry.type == ChangeRequestType.Submission
      ? "Submitted"
      : entry.type == ChangeRequestType.Import
      ? "Imported"
      : entry.type == ChangeRequestType.Merge
      ? "Merged"
      : entry.type == ChangeRequestType.Initial
      ? "Last Modified"
      : entry.type == ChangeRequestType.Rename
      ? "Tag Renamed"
      : entry.type == ChangeRequestType.Delete
      ? "Tag Deleted"
      : entry.type;
  return (
    <div style={{ marginBottom: 8 }}>
      {entry.reviewedOn && (
        <div>
          <strong>{formatDateTime(entry.reviewedOn)}</strong>
          {entry.approvedByUserId &&
            entry.type == ChangeRequestType.Submission && (
              <span style={{ fontSize: 12, color: "#888", marginLeft: 8 }}>
                Approved by{" "}
                <UserAvatarComponent showName userId={entry.approvedByUserId} />
              </span>
            )}
        </div>
      )}
      <div style={{ fontSize: 12, color: "#888", marginTop: 4 }}>
        <span>
          {requestTypeDisplay}{" "}
          {entry.type == ChangeRequestType.Submission &&
            formatDateTime(entry.submittedOn)}
        </span>
        <span>
          {" "}
          by <UserAvatarComponent showName userId={entry.changedByUserId!} />
        </span>
      </div>
    </div>
  );
};

export interface CaveHistoryModalProps {
  caveId?: string;
  caveName: string;
  visible: boolean;
  onClose: () => void;
}

const CaveHistoryModal: React.FC<CaveHistoryModalProps> = ({
  caveId,
  caveName,
  visible,
  onClose,
}) => {
  const [loading, setLoading] = useState(false);
  const [entries, setEntries] = useState<CaveHistory[]>([]);

  useEffect(() => {
    if (!visible || isNullOrWhiteSpace(caveId)) return;
    setLoading(true);
    CaveService.GetCaveHistory(caveId!)
      .then(setEntries)
      .finally(() => setLoading(false));
  }, [visible, caveId]);

  return (
    <PlanarianModal
      header={`History for ${caveName}`}
      open={visible}
      width={700}
      footer={null}
      onClose={onClose}
    >
      {loading ? (
        <div style={{ textAlign: "center", padding: 24 }}>
          <Spin />
        </div>
      ) : (
        <Timeline>
          {entries.map((entry, i) => (
            <Timeline.Item key={i} color="blue">
              <ChangeHeader entry={entry} />

              {/* Cave-level changes */}
              {entry.caveHistoryDetails.length > 0 && (
                <div style={{ marginBottom: 12 }}>
                  {entry.caveHistoryDetails.map((d, idx) => (
                    <React.Fragment key={idx}>
                      {renderDetail(d, entry)}
                    </React.Fragment>
                  ))}
                </div>
              )}

              {/* Entrance-level changes */}
              {entry.entranceHistorySummary.map((sum, idx) => (
                <div key={idx} style={{ marginBottom: 12 }}>
                  <div
                    style={{
                      fontWeight: 600,
                      marginBottom: 4,
                      display: "flex",
                      alignItems: "center",
                    }}
                  >
                    {sum.entranceName}
                    {/* only show Add/Delete */}
                    {sum.changeType !== ChangeType.Update && (
                      <Tag
                        color={getChangeTypeColor(sum.changeType)}
                        style={{ marginLeft: 8 }}
                      >
                        {getChangeTypeDisplay(sum.changeType)}
                      </Tag>
                    )}
                  </div>
                  {sum.details.map((d, j) => (
                    <React.Fragment key={j}>
                      {renderDetail(d, entry)}
                    </React.Fragment>
                  ))}
                </div>
              ))}
            </Timeline.Item>
          ))}
        </Timeline>
      )}
    </PlanarianModal>
  );
};

export { CaveHistoryModal };
