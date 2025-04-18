import {
  Collapse,
  Select,
  DatePicker,
  Col,
  Grid,
  Descriptions,
  Row,
  Tag,
  Space,
  Tooltip,
  Card,
} from "antd";
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
} from "../../../Shared/Helpers/StringHelpers";
import { useFeatureEnabled } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { FileListComponent } from "../../Files/Components/FileListComponent";
import { MapComponent } from "../../Map/Components/MapComponent";
import { PublicAccessDetails } from "../../Map/Components/PublicAccesDetails";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { CaveVm } from "./CaveVm";
import { EntranceVm } from "./EntranceVm";
import { CarOutlined } from "@ant-design/icons";
import { AddCaveVm } from "./AddCaveVm";
import { AddEntranceVm } from "./AddEntranceVm";
import { FileVm } from "../../Files/Models/FileVm";

const { Panel } = Collapse;

export interface CaveReviewComponentProps {
  cave?: AddCaveVm;
  isLoading: boolean;
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

const CaveReviewComponent = ({ cave, isLoading }: CaveReviewComponentProps) => {
  const { isFeatureEnabled } = useFeatureEnabled();

  const screens = Grid.useBreakpoint();
  const descriptionLayout = screens.md ? "horizontal" : "vertical";

  const descriptionItems = [
    isFeatureEnabled(FeatureKey.EnabledFieldCaveAlternateNames) && (
      <Descriptions.Item label="Alternative Names" key="alternative-names">
        <Row>
          {cave?.alternateNames.length === 0 && (
            <Col>{defaultIfEmpty(null)}</Col>
          )}
          {cave?.alternateNames.map((name) => (
            <Col key={name}>
              <Tag>{name}</Tag>
            </Col>
          ))}
        </Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveState) && (
      <Descriptions.Item label="State" key="state">
        <StateTagComponent stateId={cave?.stateId} />
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveCounty) &&
      !isNullOrWhiteSpace(cave?.countyId) && (
        <Descriptions.Item label="County" key="county">
          <CountyTagComponent countyId={cave?.countyId} />
        </Descriptions.Item>
      ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveLengthFeet) && (
      <Descriptions.Item label="Length" key="length">
        {defaultIfEmpty(formatDistance(cave?.lengthFeet))}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveDepthFeet) && (
      <Descriptions.Item label="Depth" key="depth">
        {defaultIfEmpty(formatDistance(cave?.depthFeet, DistanceFormat.feet))}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveMaxPitDepthFeet) && (
      <Descriptions.Item label="Max Pit Depth" key="max-pit-depth">
        {defaultIfEmpty(
          formatDistance(cave?.maxPitDepthFeet, DistanceFormat.feet)
        )}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveNumberOfPits) && (
      <Descriptions.Item label="Number of Pits" key="number-of-pits">
        {defaultIfEmpty(formatNumber(cave?.numberOfPits))}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedOn) && (
      <Descriptions.Item label="Reported On" key="reported-on">
        {cave?.reportedOn ? formatDate(cave.reportedOn) : defaultIfEmpty(null)}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedByNameTags) && (
      <Descriptions.Item label="Reported By" key="reported-by">
        <Row>{generateTags(cave?.reportedByNameTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologyTags) && (
      <Descriptions.Item label="Geology" key="geology">
        <Row>{generateTags(cave?.geologyTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologicAgeTags) && (
      <Descriptions.Item label="Geologic Age" key="geologic-age">
        <Row>{generateTags(cave?.geologicAgeTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCavePhysiographicProvinceTags) && (
      <Descriptions.Item
        label="Physiographic Province"
        key="physiographic-province"
      >
        <Row>{generateTags(cave?.physiographicProvinceTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveBiologyTags) && (
      <Descriptions.Item label="Biology" key="biology">
        <Row>{generateTags(cave?.biologyTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveArcheologyTags) && (
      <Descriptions.Item label="Archeology" key="archeology">
        <Row>{generateTags(cave?.archeologyTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveMapStatusTags) && (
      <Descriptions.Item label="Map Status" key="map-status">
        <Row>{generateTags(cave?.mapStatusTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveCartographerNameTags) && (
      <Descriptions.Item label="Cartographers" key="cartographers">
        <Row>{generateTags(cave?.cartographerNameTagIds)}</Row>
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveOtherTags) && (
      <Descriptions.Item label="Other" key="other">
        <Row>{generateTags(cave?.otherTagIds)}</Row>
      </Descriptions.Item>
    ),
  ].filter(Boolean);

  // Entrance details for each entrance panel
  const entranceItems = (entrance: AddEntranceVm) =>
    [
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceCoordinates) && (
        <Descriptions.Item
          label={
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
            </Space>
          }
          key="coordinates"
        >
          {formatCoordinates(entrance.latitude, entrance.longitude)}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceDescription) && (
        <Descriptions.Item label="Description" key="description">
          {entrance.description}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceElevation) && (
        <Descriptions.Item label="Elevation" key="elevation">
          {defaultIfEmpty(
            formatDistance(entrance.elevationFeet, DistanceFormat.feet)
          )}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceLocationQuality) && (
        <Descriptions.Item label="Location Quality" key="location-quality">
          <TagComponent tagId={entrance.locationQualityTagId} />
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceName) && (
        <Descriptions.Item label="Name" key="name">
          {entrance.name}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedOn) && (
        <Descriptions.Item label="Reported On" key="reported-on">
          {entrance.reportedOn
            ? formatDate(entrance.reportedOn)
            : defaultIfEmpty(null)}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedByNameTags) && (
        <Descriptions.Item label="Reported By" key="reported-by">
          <Row>{generateTags(entrance.reportedByNameTagIds)}</Row>
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntrancePitDepth) && (
        <Descriptions.Item label="Pit Depth" key="pit-depth">
          {defaultIfEmpty(formatDistance(entrance.pitFeet))}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceStatusTags) && (
        <Descriptions.Item label="Status" key="status">
          <Row>{generateTags(entrance.entranceStatusTagIds)}</Row>
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceFieldIndicationTags) && (
        <Descriptions.Item label="Field Indication" key="field-indication">
          <Row>{generateTags(entrance.fieldIndicationTagIds)}</Row>
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceHydrologyTags) && (
        <Descriptions.Item label="Hydrology" key="hydrology">
          <Row>{generateTags(entrance.entranceHydrologyTagIds)}</Row>
        </Descriptions.Item>
      ),
    ].filter(Boolean);

  const content = (
    <>
      <PlanarianDividerComponent title="Information" />
      <Descriptions layout={descriptionLayout} bordered>
        {descriptionItems}
      </Descriptions>

      {cave?.entrances && cave?.entrances.length > 0 && (
        <>
          <PlanarianDividerComponent title="Entrances" />
          <Collapse bordered defaultActiveKey={["0"]}>
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

      {isFeatureEnabled(FeatureKey.EnabledFieldCaveNarrative) && (
        <>
          {!isNullOrWhiteSpace(cave?.narrative) && (
            <>
              <PlanarianDividerComponent title="Narrative" />
              <ParagraphDisplayComponent text={cave?.narrative} />
            </>
          )}
        </>
      )}

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
