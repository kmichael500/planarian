import React, { useEffect, useState } from "react";
import { Modal, Timeline, Spin, Tag } from "antd";
import dayjs from "dayjs";
import { CaveService } from "../Service/CaveService";
import {
  CaveChangeLogVm,
  CaveLogPropertyName,
  ChangeType,
  ChangeValueType,
} from "../Models/ProposedChangeRequestVm";
import {
  formatDate,
  formatDateTime,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";

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
  const [history, setHistory] = useState<CaveChangeLogVm[]>([]);

  useEffect(() => {
    if (isNullOrWhiteSpace(caveId)) {
      return;
    }
    if (visible) {
      setLoading(true);
      CaveService.GetCaveHistory(caveId)
        .then((entries) => setHistory(entries))
        .finally(() => setLoading(false));
    }
  }, [visible, caveId]);

  const renderValue = (entry: CaveChangeLogVm) => {
    switch (entry.changeValueType) {
      case "String":
        return entry.valueString;
      case "Int":
        return entry.valueInt;
      case "Double":
        return entry.valueDouble;
      case "Bool":
        return entry.valueBool ? "Yes" : "No";
      case "DateTime":
        return entry.valueDateTime
          ? dayjs(entry.valueDateTime).format("MMM D, YYYY HH:mm")
          : "";
      default:
        return "";
    }
  };

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
      case CaveLogPropertyName.EntranceOtherTagName:
        return "Entrance Other";
      case CaveLogPropertyName.Entrance:
        return "Entrance";
      case CaveLogPropertyName.Cave:
        return "Cave";
      default:
        return propertyName;
    }
  };

  const getChangeTypeColor = (changeType: string): string => {
    switch (changeType) {
      case ChangeType.Add:
        return "success";
      case ChangeType.Update:
        return "processing";
      case ChangeType.Delete:
        return "error";
      default:
        return "default";
    }
  };

  const getChangeTypeDisplay = (changeType: string): string => {
    switch (changeType) {
      case ChangeType.Add:
        return "Added";
      case ChangeType.Update:
        return "Updated";
      case ChangeType.Delete:
        return "Deleted";
      default:
        return changeType;
    }
  };

  return (
    <Modal
      title="Change History"
      visible={visible}
      footer={null}
      onCancel={onClose}
      width={600}
    >
      {loading ? (
        <div style={{ textAlign: "center", padding: 24 }}>
          <Spin />
        </div>
      ) : (
        <Timeline>
          {history.map((entry, i) => {
            const isEntranceChange =
              entry.entranceId || entry.propertyName.startsWith("Entrance");
            return (
              <Timeline.Item
                key={i}
                color={isEntranceChange ? "blue" : "green"}
              >
                <div className="history-item" style={{ marginBottom: "12px" }}>
                  {/* Context label */}
                  <div>
                    <Tag
                      style={{ marginRight: "8px" }}
                      color={isEntranceChange ? "blue" : "green"}
                    >
                      {isEntranceChange
                        ? entry.entranceName || "Entrance"
                        : "Cave"}
                    </Tag>
                    <Tag color={getChangeTypeColor(entry.changeType)}>
                      {getChangeTypeDisplay(entry.changeType)}
                    </Tag>
                  </div>

                  {/* Change detail */}
                  <div
                    style={{
                      marginTop: "6px",
                      padding: "8px",
                      backgroundColor: "#fafafa",
                      borderRadius: "4px",
                    }}
                  >
                    <strong style={{ fontSize: "14px" }}>
                      {getDisplayName(entry.propertyName)}
                    </strong>

                    {entry.changeType !== ChangeType.Delete && (
                      <div style={{ marginTop: "3px" }}>
                        <span style={{ color: "#666" }}>Value: </span>
                        <span>{renderValue(entry)}</span>
                      </div>
                    )}
                  </div>

                  {/* Timestamp */}
                  {entry.createdOn && (
                    <div
                      style={{
                        fontSize: "12px",
                        color: "#888",
                        marginTop: "4px",
                      }}
                    >
                      {formatDateTime(entry.createdOn)}
                    </div>
                  )}
                </div>
              </Timeline.Item>
            );
          })}
        </Timeline>
      )}
    </Modal>
  );
};

export { CaveHistoryModal };
