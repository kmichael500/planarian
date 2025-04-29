import {
  Collapse,
  Col,
  Grid,
  Descriptions,
  Row,
  Tag,
  Space,
  Tooltip,
  Card,
  Typography,
} from "antd";
import { diffLines, Change } from "diff";
import styled from "styled-components";

import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { StateTagComponent } from "../../../Shared/Components/Display/StateTagComponent";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import {
  defaultIfEmpty,
  formatDistance,
  DistanceFormat,
  formatNumber,
  formatDate,
  getDirectionsUrl,
  formatCoordinates,
  isNullOrWhiteSpace,
  formatBoolean,
} from "../../../Shared/Helpers/StringHelpers";
import { useFeatureEnabled } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { FileListComponent } from "../../Files/Components/FileListComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { CarOutlined } from "@ant-design/icons";
import { AddCaveVm } from "../Models/AddCaveVm";
import { AddEntranceVm } from "../Models/AddEntranceVm";
import { FileVm } from "../../Files/Models/FileVm";
import {
  CaveChangeLogVm,
  CaveLogPropertyName,
} from "../Models/ProposedChangeRequestVm";
import React from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";

const { Paragraph } = Typography;

const { Panel } = Collapse;

export interface CaveReviewComponentProps {
  cave?: AddCaveVm;
  originalCave?: AddCaveVm;
  isLoading: boolean;
  changes?: CaveChangeLogVm[];
}

const generateTags = (tagIds: string[] | undefined) => {
  if (!tagIds || tagIds.length === 0) {
    return defaultIfEmpty(null);
  }
  return tagIds.map((tagId) => (
    <Col key={tagId}>
      <TagComponent tagId={tagId} />
    </Col>
  ));
};

const CaveReviewComponent = ({
  cave,
  originalCave,
  changes,
  isLoading,
}: CaveReviewComponentProps) => {
  const { isFeatureEnabled } = useFeatureEnabled();

  const screens = Grid.useBreakpoint();
  const descriptionLayout = screens.md ? "horizontal" : "vertical";

  // Check if this is a new cave (all change logs have null caveId)
  const isNewCave =
    changes && changes.length > 0 && changes.every((c) => c.caveId === null);

  const [showNarrativeDiff, setShowNarrativeDiff] = React.useState(true);
  const narrativeChanged =
    !isNewCave &&
    changes?.some((c) => c.propertyName === CaveLogPropertyName.Narrative);

  const updatedDesceriptionItem = (
    label: React.ReactNode,
    value: React.ReactNode,
    originalValue: React.ReactNode | null,
    changePropertyName: CaveLogPropertyName,
    entranceId: string | null = null
  ) => {
    // If it's a new cave, don't show change indicators
    if (isNewCave) {
      return <Descriptions.Item label={label}>{value}</Descriptions.Item>;
    }

    const hasChanges = changes?.some(
      (c) =>
        c.propertyName === changePropertyName &&
        // if an entranceId was passed, only match changes for that entrance
        (entranceId ? c.entranceId === entranceId : true)
    );

    const hasCountyChange =
      changePropertyName === CaveLogPropertyName.CountyName && hasChanges;

    if (hasChanges) {
      return (
        <Descriptions.Item
          label={
            <>
              <Tooltip
                title={
                  <>Original Value: {originalValue ?? defaultIfEmpty(null)}</>
                }
              >
                {label} <Tag color="green">Changed</Tag>
              </Tooltip>
              {hasCountyChange && (
                <Tooltip title="This change will issue a new county cave number.">
                  <Tag color="#F8DB6A">Warning</Tag>
                </Tooltip>
              )}
            </>
          }
        >
          <Tooltip
            title={<>Original Value: {originalValue ?? defaultIfEmpty(null)}</>}
          >
            {value}
          </Tooltip>
        </Descriptions.Item>
      );
    }

    return <Descriptions.Item label={label}>{value}</Descriptions.Item>;
  };

  const descriptionItems = [
    isFeatureEnabled(FeatureKey.EnabledFieldCaveAlternateNames) &&
      updatedDesceriptionItem(
        "Alternative Names",
        <Row>
          {cave?.alternateNames.length === 0 && (
            <Col>{defaultIfEmpty(null)}</Col>
          )}
          {cave?.alternateNames.map((name) => (
            <Col key={name}>
              <Tag>{name}</Tag>
            </Col>
          ))}
        </Row>,
        <Row>
          {originalCave?.alternateNames.length === 0 && (
            <Col>{defaultIfEmpty(null)}</Col>
          )}
          {originalCave?.alternateNames.map((name) => (
            <Col key={name}>
              <Tag>{name}</Tag>
            </Col>
          ))}
        </Row>,
        CaveLogPropertyName.AlternateNames
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveState) &&
      updatedDesceriptionItem(
        "State",
        <StateTagComponent stateId={cave?.stateId} />,
        <StateTagComponent stateId={originalCave?.stateId} />,
        CaveLogPropertyName.StateName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveCounty) &&
      updatedDesceriptionItem(
        "County",
        !isNullOrWhiteSpace(cave?.countyId) && (
          <CountyTagComponent countyId={cave?.countyId} />
        ),
        !isNullOrWhiteSpace(originalCave?.countyId) && (
          <CountyTagComponent countyId={originalCave?.countyId} />
        ),
        CaveLogPropertyName.CountyName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveLengthFeet) &&
      updatedDesceriptionItem(
        "Length",
        defaultIfEmpty(formatDistance(cave?.lengthFeet)),
        defaultIfEmpty(formatDistance(originalCave?.lengthFeet)),
        CaveLogPropertyName.LengthFeet
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveDepthFeet) &&
      updatedDesceriptionItem(
        "Depth",
        defaultIfEmpty(formatDistance(cave?.depthFeet, DistanceFormat.feet)),
        defaultIfEmpty(
          formatDistance(originalCave?.depthFeet, DistanceFormat.feet)
        ),
        CaveLogPropertyName.DepthFeet
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveMaxPitDepthFeet) &&
      updatedDesceriptionItem(
        "Max Pit Depth",
        defaultIfEmpty(
          formatDistance(cave?.maxPitDepthFeet, DistanceFormat.feet)
        ),
        defaultIfEmpty(
          formatDistance(originalCave?.maxPitDepthFeet, DistanceFormat.feet)
        ),
        CaveLogPropertyName.MaxPitDepthFeet
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveNumberOfPits) &&
      updatedDesceriptionItem(
        "Number of Pits",
        defaultIfEmpty(formatNumber(cave?.numberOfPits)),
        defaultIfEmpty(formatNumber(originalCave?.numberOfPits)),
        CaveLogPropertyName.NumberOfPits
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedOn) &&
      updatedDesceriptionItem(
        "Reported On",
        cave?.reportedOn ? formatDate(cave.reportedOn) : defaultIfEmpty(null),
        originalCave?.reportedOn
          ? formatDate(originalCave.reportedOn)
          : defaultIfEmpty(null),
        CaveLogPropertyName.ReportedOn
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedByNameTags) &&
      updatedDesceriptionItem(
        "Reported By",
        <Row>{generateTags(cave?.reportedByNameTagIds)}</Row>,
        <Row>{generateTags(originalCave?.reportedByNameTagIds)}</Row>,
        CaveLogPropertyName.ReportedByNameTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologyTags) &&
      updatedDesceriptionItem(
        "Geology",
        <Row>{generateTags(cave?.geologyTagIds)}</Row>,
        <Row>{generateTags(originalCave?.geologyTagIds)}</Row>,
        CaveLogPropertyName.GeologyTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologicAgeTags) &&
      updatedDesceriptionItem(
        "Geologic Age",
        <Row>{generateTags(cave?.geologicAgeTagIds)}</Row>,
        <Row>{generateTags(originalCave?.geologicAgeTagIds)}</Row>,
        CaveLogPropertyName.GeologicAgeTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCavePhysiographicProvinceTags) &&
      updatedDesceriptionItem(
        "Physiographic Province",
        <Row>{generateTags(cave?.physiographicProvinceTagIds)}</Row>,
        <Row>{generateTags(originalCave?.physiographicProvinceTagIds)}</Row>,
        CaveLogPropertyName.PhysiographicProvinceTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveBiologyTags) &&
      updatedDesceriptionItem(
        "Biology",
        <Row>{generateTags(cave?.biologyTagIds)}</Row>,
        <Row>{generateTags(originalCave?.biologyTagIds)}</Row>,
        CaveLogPropertyName.BiologyTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveArcheologyTags) &&
      updatedDesceriptionItem(
        "Archeology",
        <Row>{generateTags(cave?.archeologyTagIds)}</Row>,
        <Row>{generateTags(originalCave?.archeologyTagIds)}</Row>,
        CaveLogPropertyName.ArcheologyTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveMapStatusTags) &&
      updatedDesceriptionItem(
        "Map Status",
        <Row>{generateTags(cave?.mapStatusTagIds)}</Row>,
        <Row>{generateTags(originalCave?.mapStatusTagIds)}</Row>,
        CaveLogPropertyName.MapStatusTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveCartographerNameTags) &&
      updatedDesceriptionItem(
        "Cartographers",
        <Row>{generateTags(cave?.cartographerNameTagIds)}</Row>,
        <Row>{generateTags(originalCave?.cartographerNameTagIds)}</Row>,
        CaveLogPropertyName.CartographerNameTagName
      ),

    isFeatureEnabled(FeatureKey.EnabledFieldCaveOtherTags) &&
      updatedDesceriptionItem(
        "Other",
        <Row>{generateTags(cave?.otherTagIds)}</Row>,
        <Row>{generateTags(originalCave?.otherTagIds)}</Row>,
        CaveLogPropertyName.OtherTagName
      ),
  ].filter(Boolean);

  // Entrance details for each entrance panel
  const entranceItems = (entrance: AddEntranceVm) => {
    const originalEnt = originalCave?.entrances?.find(
      (e) => e.id === entrance.id
    );

    // Check if this entrance was deleted
    const isDeleted =
      entrance.id &&
      !cave?.entrances.some((e) => e.id === entrance.id) &&
      originalCave?.entrances.some((e) => e.id === entrance.id);

    // For deleted entrances, don't highlight individual property changes
    const updatedEntranceItem = (
      label: React.ReactNode,
      value: React.ReactNode,
      originalValue: React.ReactNode | null,
      changePropertyName: CaveLogPropertyName,
      entranceId: string | null = null
    ) => {
      // If entrance is deleted, don't show change highlights
      if (isDeleted) {
        return <Descriptions.Item label={label}>{value}</Descriptions.Item>;
      }

      // Otherwise use the normal change detection and highlighting
      return updatedDesceriptionItem(
        label,
        value,
        originalValue,
        changePropertyName,
        entranceId
      );
    };

    return [
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceName) &&
        updatedEntranceItem(
          "Name",
          entrance.name || defaultIfEmpty(null),
          originalEnt?.name ?? defaultIfEmpty(null),
          CaveLogPropertyName.EntranceName,
          entrance.id
        ),
      updatedEntranceItem(
        "Is Primary",
        formatBoolean(entrance.isPrimary),
        formatBoolean(originalEnt?.isPrimary),
        CaveLogPropertyName.EntranceIsPrimary,
        entrance.id
      ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceCoordinates) &&
        updatedEntranceItem(
          <Space>
            Coordinates
            <a
              href={getDirectionsUrl(entrance.latitude, entrance.longitude)}
              target="_blank"
              className={isDeleted ? "strikethrough-all" : ""}
            >
              <Tooltip title="Directions">
                <CarOutlined />
              </Tooltip>
            </a>
          </Space>,
          <span className={isDeleted ? "strikethrough-all" : ""}>
            {formatCoordinates(entrance.latitude, entrance.longitude)}
          </span>,
          originalEnt
            ? formatCoordinates(originalEnt.latitude, originalEnt.longitude)
            : defaultIfEmpty(null),
          CaveLogPropertyName.Entrance,
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceDescription) &&
        updatedEntranceItem(
          "Description",
          entrance.description || defaultIfEmpty(null),
          originalEnt?.description ?? defaultIfEmpty(null),
          CaveLogPropertyName.EntranceDescription,
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceElevation) &&
        updatedEntranceItem(
          "Elevation",
          defaultIfEmpty(
            formatDistance(entrance.elevationFeet, DistanceFormat.feet)
          ),
          defaultIfEmpty(
            formatDistance(originalEnt?.elevationFeet, DistanceFormat.feet)
          ),
          CaveLogPropertyName.Entrance, // or a finer‐grained enum
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceLocationQuality) &&
        updatedEntranceItem(
          "Location Quality",
          <TagComponent tagId={entrance.locationQualityTagId} />,
          originalEnt ? (
            <TagComponent tagId={originalEnt.locationQualityTagId} />
          ) : (
            defaultIfEmpty(null)
          ),
          CaveLogPropertyName.EntranceLocationQualityTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedOn) &&
        updatedEntranceItem(
          "Reported On",
          entrance.reportedOn
            ? formatDate(entrance.reportedOn)
            : defaultIfEmpty(null),
          originalEnt?.reportedOn
            ? formatDate(originalEnt.reportedOn)
            : defaultIfEmpty(null),
          CaveLogPropertyName.EntranceReportedOn,
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedByNameTags) &&
        updatedEntranceItem(
          "Reported By",
          <Row>{generateTags(entrance.reportedByNameTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.reportedByNameTagIds)}</Row>,
          CaveLogPropertyName.EntranceReportedByNameTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntrancePitDepth) &&
        updatedEntranceItem(
          "Pit Depth",
          defaultIfEmpty(formatDistance(entrance.pitFeet)),
          defaultIfEmpty(formatDistance(originalEnt?.pitFeet)),
          CaveLogPropertyName.EntrancePitDepthFeet,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceStatusTags) &&
        updatedEntranceItem(
          "Status",
          <Row>{generateTags(entrance.entranceStatusTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.entranceStatusTagIds)}</Row>,
          CaveLogPropertyName.EntranceStatusTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceFieldIndicationTags) &&
        updatedEntranceItem(
          "Field Indication",
          <Row>{generateTags(entrance.fieldIndicationTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.fieldIndicationTagIds)}</Row>,
          CaveLogPropertyName.EntranceFieldIndicationTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceHydrologyTags) &&
        updatedEntranceItem(
          "Hydrology",
          <Row>{generateTags(entrance.entranceHydrologyTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.entranceHydrologyTagIds)}</Row>,
          CaveLogPropertyName.EntranceHydrologyTagName,
          entrance.id
        ),
    ].filter(Boolean);
  };

  const narrativeDiff: Change[] = React.useMemo(() => {
    return diffLines(originalCave?.narrative ?? "", cave?.narrative ?? "");
  }, [originalCave?.narrative, cave?.narrative]);

  // Combine current and deleted entrances for display
  const entrancesToDisplay = React.useMemo(() => {
    if (!cave && !originalCave) return [];

    const currentEntrances = cave?.entrances || [];
    const deletedEntrances: AddEntranceVm[] = [];

    // Find entrances that exist in originalCave but not in current cave
    if (originalCave?.entrances) {
      originalCave.entrances.forEach((originalEntrance) => {
        // Check if this entrance still exists in current cave
        const entranceStillExists = currentEntrances.some(
          (e) => e.id === originalEntrance.id
        );

        if (!entranceStillExists) {
          // If entrance doesn't exist in current cave, it was deleted
          deletedEntrances.push(originalEntrance);
        }
      });
    }

    return [...currentEntrances, ...deletedEntrances];
  }, [cave, originalCave]);

  const entrancesWithChanges = React.useMemo(() => {
    if (!changes || !entrancesToDisplay.length) return [];

    return entrancesToDisplay
      .map((entrance, index) => {
        const hasChanges = changes.some(
          (change) => change.entranceId === entrance.id
        );
        return hasChanges ? index.toString() : null;
      })
      .filter(Boolean) as string[];
  }, [changes, entrancesToDisplay]);

  const narrativeSection = isFeatureEnabled(
    FeatureKey.EnabledFieldCaveNarrative
  ) && (
    <>
      <PlanarianDividerComponent
        title={
          <Space>
            Narrative
            {narrativeChanged && <Tag color="gold">Changed</Tag>}
          </Space>
        }
        element={
          narrativeChanged ? (
            <PlanarianButton
              onClick={() => setShowNarrativeDiff((p) => !p)}
              icon={undefined}
            >
              {showNarrativeDiff ? "Hide Diff" : "Show Diff"}
            </PlanarianButton>
          ) : null
        }
      />

      {narrativeChanged && showNarrativeDiff ? (
        <Typography>
          {narrativeDiff.map((part, i) => {
            const bg = part.added
              ? "#e6ffed"
              : part.removed
              ? "#ffeef0"
              : "transparent";
            const prefix = part.added ? "+ " : part.removed ? "- " : "  ";

            const paragraphs = part.value.split("\n");

            return paragraphs.map((paragraph, j) => (
              <Paragraph
                key={`${i}-${j}`}
                style={{
                  backgroundColor: bg,
                  margin: part.added || part.removed ? "0" : undefined,
                }}
              >
                {prefix}
                {paragraph}
              </Paragraph>
            ));
          })}
        </Typography>
      ) : (
        !isNullOrWhiteSpace(cave?.narrative) && (
          <ParagraphDisplayComponent text={cave?.narrative} />
        )
      )}
    </>
  );

  // Check if the entire cave is deleted
  const isCaveDeleted = !!originalCave && !cave;

  const content = (
    <>
      <PlanarianDividerComponent
        title="Information"
        element={<>{isNewCave && <Tag color="blue">New</Tag>}</>}
      />
      <Descriptions
        layout={descriptionLayout}
        bordered
        className={isCaveDeleted ? "deleted-cave" : ""}
      >
        {descriptionItems}
      </Descriptions>

      {cave?.entrances && cave?.entrances.length > 0 && (
        <>
          <PlanarianDividerComponent title="Entrances" />
          <Collapse bordered defaultActiveKey={entrancesWithChanges}>
            {entrancesToDisplay.map((entrance, index) => {
              // Check if this entrance was deleted - not in current cave but was in original
              const isDeleted =
                entrance.id &&
                !cave?.entrances.some((e) => e.id === entrance.id) &&
                originalCave?.entrances.some((e) => e.id === entrance.id);

              // Check if this entrance was added - in current cave but wasn't in original
              const isNew =
                entrance.id &&
                cave?.entrances.some((e) => e.id === entrance.id) &&
                !originalCave?.entrances.some((e) => e.id === entrance.id);

              return (
                <Panel
                  header={
                    <>
                      <Row>
                        Entrance {index + 1}
                        {!isNullOrWhiteSpace(entrance.name)
                          ? " - " + entrance.name
                          : ""}
                        {entrance.isPrimary && (
                          <>
                            <Col flex="auto"></Col>
                            <Tag>Primary</Tag>
                          </>
                        )}
                        {isDeleted && (
                          <>
                            <Col flex="auto"></Col>
                            <Tag color="red">Removed</Tag>
                          </>
                        )}
                        {isNew && (
                          <>
                            <Col flex="auto"></Col>
                            <Tag color="blue">New</Tag>
                          </>
                        )}
                      </Row>
                    </>
                  }
                  key={index}
                  style={isDeleted ? { opacity: 0.7 } : undefined}
                >
                  <Descriptions
                    bordered
                    layout={descriptionLayout}
                    // Apply strikethrough to all properties of deleted entrances
                    className={isDeleted ? "deleted-entrance" : ""}
                  >
                    {entranceItems(entrance)}
                  </Descriptions>
                </Panel>
              );
            })}
          </Collapse>
        </>
      )}

      {narrativeSection}

      <PlanarianDividerComponent title="Files" />

      <FileListComponent
        files={cave?.files?.map(
          (file) =>
            ({
              displayName: file.displayName,
              fileTypeTagId: file.fileTypeTagId,
            } as FileVm)
        )}
        customOrder={["Map"]}
        isUploading={false}
      />
    </>
  );

  return (
    <StyledComponents.DeletedEntrance>
      <Card
        bodyStyle={!isLoading ? { paddingTop: "0px" } : {}}
        loading={isLoading}
      >
        {content}
      </Card>
    </StyledComponents.DeletedEntrance>
  );
};

const StyledComponents = {
  DeletedEntrance: styled.div`
    /* Apply strikethrough to all deleted entrance content */
    .deleted-entrance .ant-descriptions-item-label,
    .deleted-entrance .ant-descriptions-item-content,
    .deleted-cave .ant-descriptions-item-label,
    .deleted-cave .ant-descriptions-item-content {
      text-decoration: line-through !important;
    }

    /* Force strikethrough on all nested elements */
    .deleted-entrance .ant-descriptions-item-content *,
    .deleted-cave .ant-descriptions-item-content * {
      text-decoration: line-through !important;
    }

    /* Specifically target coordinates field */
    .deleted-entrance .ant-space,
    .deleted-entrance .ant-space-item,
    .deleted-entrance .anticon {
      text-decoration: line-through !important;
    }

    /* But keep tooltip content normal */
    .ant-tooltip-inner,
    .ant-tooltip-inner * {
      text-decoration: none !important;
    }
  `,
};

export { CaveReviewComponent };
