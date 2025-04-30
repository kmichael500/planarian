import React, { useEffect, useState } from "react";
import { Modal, Timeline, Spin, Tag } from "antd";
import { CaveService } from "../Service/CaveService";
import {
  CaveHistory,
  HistoryDetail,
  EntranceHistorySummary,
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
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";

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

// Explicitly handle every property
// Helper function that can handle React nodes
const defaultIfEmptyNode = (
  value: string | null | React.ReactNode
): React.ReactNode => {
  if (
    value === null ||
    value === undefined ||
    (typeof value === "string" && isNullOrWhiteSpace(value))
  ) {
    return "-";
  } else {
    return value;
  }
};

const renderDetail = (d: HistoryDetail): JSX.Element => {
  let newValRaw: string | null | React.ReactNode = null;
  let prevValRaw: string | null | React.ReactNode = null;

  switch (d.propertyName) {
    // --- Cave-level fields ---
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
      newValRaw = formatDistance(
        d.valueDouble ?? d.valueInt ?? undefined,
        DistanceFormat.feet
      );
      prevValRaw = formatDistance(
        d.previousValueDouble ?? d.previousValueInt ?? undefined,
        DistanceFormat.feet
      );
      break;

    case CaveLogPropertyName.Narrative:
      newValRaw = d.valueString;
      prevValRaw = d.previousValueString;
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
      const newVal = defaultIfEmptyNode(newValRaw);
      const showPrev =
        prevValRaw !== null &&
        prevValRaw !== undefined &&
        prevValRaw !== newValRaw;
      const prevVal = showPrev ? defaultIfEmptyNode(prevValRaw) : null;

    default:
      newValRaw = "Not implemented";
      prevValRaw = "Not implemented";
  }

  const newVal = defaultIfEmpty(newValRaw);
  const showPrev = !isNullOrWhiteSpace(prevValRaw) && prevValRaw !== newValRaw;
  const prevVal = showPrev ? defaultIfEmpty(prevValRaw) : null;

  return (
    <div style={{ marginBottom: 4, marginLeft: 16 }}>
      <strong>{getDisplayName(d.propertyName)}:</strong> {newVal}
      {showPrev && (
        <div style={{ color: "#888", fontSize: 12 }}>Previous: {prevVal}</div>
      )}
    </div>
  );
};

export interface CaveHistoryModalProps {
  caveId?: string;
  visible: boolean;
  onClose: () => void;
}

const CaveHistoryModal: React.FC<CaveHistoryModalProps> = ({
  caveId,
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
      header="Change History"
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
              {/* Header: reviewed first, then submitted */}
              <div style={{ marginBottom: 8 }}>
                {entry.reviewedOn && (
                  <div>
                    <strong>
                      {formatDateTime(entry.reviewedOn, "MMM D, YYYY h:mm A")}
                    </strong>
                    {entry.approvedByUserId && (
                      <span
                        style={{ fontSize: 12, color: "#888", marginLeft: 8 }}
                      >
                        Approved by{" "}
                        <UserAvatarComponent
                          showName
                          userId={entry.approvedByUserId}
                        />
                      </span>
                    )}
                  </div>
                )}
                <div style={{ fontSize: 12, color: "#888", marginTop: 4 }}>
                  <span>
                    Submitted{" "}
                    {formatDateTime(entry.submittedOn, "MMM D, YYYY h:mm A")}
                  </span>
                  <span>
                    {" "}
                    by{" "}
                    <UserAvatarComponent
                      showName
                      userId={entry.changedByUserId!}
                    />
                  </span>
                </div>
              </div>

              {/* Cave-level changes */}
              {entry.caveHistoryDetails.length > 0 && (
                <div style={{ marginBottom: 12 }}>
                  {entry.caveHistoryDetails.map((d, idx) => (
                    <React.Fragment key={idx}>{renderDetail(d)}</React.Fragment>
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
                    <React.Fragment key={j}>{renderDetail(d)}</React.Fragment>
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
