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

  const [showNarrativeDiff, setShowNarrativeDiff] = React.useState(true);
  const narrativeChanged = changes?.some(
    (c) => c.propertyName === CaveLogPropertyName.Narrative
  );

  const updatedDesceriptionItem = (
    label: React.ReactNode,
    value: React.ReactNode,
    originalValue: React.ReactNode | null,
    changePropertyName: CaveLogPropertyName,
    entranceId: string | null = null
  ) => {
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
                  <>Original Value: {originalValue ?? defaultIfEmpty(null)}</>
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
            title={<>Original Value: {originalValue ?? defaultIfEmpty(null)}</>}
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

    return [
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceName) &&
        updatedDesceriptionItem(
          "Name",
          entrance.name || defaultIfEmpty(null),
          originalEnt?.name ?? defaultIfEmpty(null),
          CaveLogPropertyName.EntranceName,
          entrance.id
        ),
      updatedDesceriptionItem(
        "Is Primary",
        formatBoolean(entrance.isPrimary),
        formatBoolean(originalEnt?.isPrimary),
        CaveLogPropertyName.EntranceIsPrimary,
        entrance.id
      ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceCoordinates) &&
        updatedDesceriptionItem(
          <Space>
            Coordinates
            <a
              href={getDirectionsUrl(entrance.latitude, entrance.longitude)}
              target="_blank"
            >
              <Tooltip title="Directions">
                <CarOutlined />
              </Tooltip>
            </a>
          </Space>,
          formatCoordinates(entrance.latitude, entrance.longitude),
          originalEnt
            ? formatCoordinates(originalEnt.latitude, originalEnt.longitude)
            : defaultIfEmpty(null),
          CaveLogPropertyName.Entrance, // or create a specific enum if needed
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceDescription) &&
        updatedDesceriptionItem(
          "Description",
          entrance.description || defaultIfEmpty(null),
          originalEnt?.description ?? defaultIfEmpty(null),
          CaveLogPropertyName.EntranceDescription,
          entrance.id
        ),

      isFeatureEnabled(FeatureKey.EnabledFieldEntranceElevation) &&
        updatedDesceriptionItem(
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
        updatedDesceriptionItem(
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
        updatedDesceriptionItem(
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
        updatedDesceriptionItem(
          "Reported By",
          <Row>{generateTags(entrance.reportedByNameTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.reportedByNameTagIds)}</Row>,
          CaveLogPropertyName.EntranceReportedByNameTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntrancePitDepth) &&
        updatedDesceriptionItem(
          "Pit Depth",
          defaultIfEmpty(formatDistance(entrance.pitFeet)),
          defaultIfEmpty(formatDistance(originalEnt?.pitFeet)),
          CaveLogPropertyName.EntrancePitDepthFeet,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceStatusTags) &&
        updatedDesceriptionItem(
          "Status",
          <Row>{generateTags(entrance.entranceStatusTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.entranceStatusTagIds)}</Row>,
          CaveLogPropertyName.EntranceStatusTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceFieldIndicationTags) &&
        updatedDesceriptionItem(
          "Field Indication",
          <Row>{generateTags(entrance.fieldIndicationTagIds)}</Row>,
          <Row>{generateTags(originalEnt?.fieldIndicationTagIds)}</Row>,
          CaveLogPropertyName.EntranceFieldIndicationTagName,
          entrance.id
        ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceHydrologyTags) &&
        updatedDesceriptionItem(
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

  const entrancesWithChanges = React.useMemo(() => {
    if (!changes || !cave?.entrances) return [];

    return cave.entrances
      .map((entrance, index) => {
        const hasChanges = changes.some(
          (change) => change.entranceId === entrance.id
        );
        return hasChanges ? index.toString() : null;
      })
      .filter(Boolean) as string[];
  }, [changes, cave?.entrances]);

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

  const content = (
    <>
      <PlanarianDividerComponent title="Information" />
      <Descriptions layout={descriptionLayout} bordered>
        {descriptionItems}
      </Descriptions>

      {cave?.entrances && cave?.entrances.length > 0 && (
        <>
          <PlanarianDividerComponent title="Entrances" />
          <Collapse bordered defaultActiveKey={entrancesWithChanges}>
            {cave.entrances.map((entrance, index) => (
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
                    </Row>
                  </>
                }
                key={index}
              >
                <Descriptions bordered layout={descriptionLayout}>
                  {entranceItems(entrance)}
                </Descriptions>
              </Panel>
            ))}
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
    <Card
      bodyStyle={!isLoading ? { paddingTop: "0px" } : {}}
      loading={isLoading}
    >
      {content}
    </Card>
  );
};

export { CaveReviewComponent };
