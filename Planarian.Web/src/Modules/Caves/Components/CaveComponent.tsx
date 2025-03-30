import { useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { CarOutlined, CloudUploadOutlined } from "@ant-design/icons";
import {
  Card,
  Col,
  Collapse,
  Descriptions,
  Grid,
  Row,
  Space,
  Tag,
  Tooltip,
  Select,
  DatePicker,
  InputNumber,
} from "antd";
import { TagComponent } from "../../Tag/Components/TagComponent";
import {
  defaultIfEmpty,
  DistanceFormat,
  formatCoordinates,
  formatDate,
  formatDistance,
  formatNumber,
  getDirectionsUrl,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import { MapComponent } from "../../Map/Components/MapComponent";
import { FileListComponent } from "../../Files/Components/FileListComponent";
import { UploadComponent } from "../../Files/Components/UploadComponent";
import { FileService } from "../../Files/Services/FileService";
import { CaveService } from "../Service/CaveService";
import { useFeatureEnabled } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { EntranceVm } from "../Models/EntranceVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { Macrostrat } from "../../Map/Components/Macrostrat";
import moment from "moment";
import { RangeValue } from "rc-picker/lib/interface";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { StateTagComponent } from "../../../Shared/Components/Display/StateTagComponent";
import { GageList } from "../../Map/Components/GaugeList";

const { Panel } = Collapse;
const { Option } = Select;
const { RangePicker } = DatePicker;

export interface CaveComponentOptions {
  showMap?: boolean;
}
export interface CaveComponentProps {
  cave?: CaveVm;
  isLoading: boolean;
  options?: CaveComponentOptions;
  hasEditPermission?: boolean;
  updateCave?: () => void;
}

// Common function to generate tags
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

const CaveComponent = ({
  cave,
  isLoading,
  options,
  updateCave,
  hasEditPermission,
}: CaveComponentProps) => {
  const [isUploading, setIsUploading] = useState(false);
  const { isFeatureEnabled } = useFeatureEnabled();

  const [showMap, setShowMap] = useState(true);

  const [selectedEntrance, setSelectedEntrance] = useState<EntranceVm | null>(
    null
  );
  const [selectedGageEntrance, setSelectedGageEntrance] =
    useState<EntranceVm | null>(null);
  const [gageDateRange, setGageDateRange] = useState<RangeValue<moment.Moment>>(
    [moment().subtract(1, "month"), moment()]
  );

  const [gageDistance, setGageDistance] = useState<number>(20);

  const screens = Grid.useBreakpoint();
  const descriptionLayout = screens.md ? "horizontal" : "vertical";

  // have to do this because of a weird bug with the Descriptions component where wrappers don't work (elements still get displayed)
  const descriptionItems = [
    isFeatureEnabled(FeatureKey.EnabledFieldCaveId) && (
      <Descriptions.Item label="ID" key="id">
        {cave?.displayId}
      </Descriptions.Item>
    ),
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
    isFeatureEnabled(FeatureKey.EnabledFieldCaveCounty) && (
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
        {defaultIfEmpty(
          formatDistance(cave?.maxPitDepthFeet, DistanceFormat.feet)
        )}
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
  const entranceItems = (entrance: EntranceVm) =>
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

  useEffect(() => {
    if (!cave?.primaryEntrance) {
      if (
        (cave?.entrances && cave?.entrances.length <= 0) ||
        options?.showMap === false
      ) {
        setShowMap(false);
      }
    }
  }, [cave, isLoading]);

  useEffect(() => {
    if (cave?.entrances && cave.entrances.length > 0) {
      const primaryEntrance = cave.entrances.find(
        (entrance) => entrance.isPrimary
      );
      setSelectedEntrance(primaryEntrance || cave.entrances[0]);
      setSelectedGageEntrance(primaryEntrance || cave.entrances[0]);
    }
  }, [cave]);

  return (
    <>
      <Card
        bodyStyle={!isLoading ? { paddingTop: "0px" } : {}}
        loading={isLoading}
      >
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

        <PlanarianDividerComponent
          title="Files"
          element={
            <>
              {!isUploading && (
                <PlanarianButton
                  permissionKey={PermissionKey.Manager}
                  disabled={!hasEditPermission}
                  icon={<CloudUploadOutlined />}
                  onClick={() => {
                    setIsUploading(true);
                  }}
                >
                  Upload
                </PlanarianButton>
              )}
            </>
          }
        />

        {!isUploading && (
          <>
            <FileListComponent
              files={cave?.files}
              isUploading={isUploading}
              setIsUploading={(value) => setIsUploading(value)}
              customOrder={["Map"]}
              hasEditPermission={hasEditPermission}
            />
          </>
        )}
        {isUploading && (
          <UploadComponent
            onClose={() => {
              if (updateCave) {
                updateCave();
              }
              setIsUploading(false);
            }}
            uploadFunction={(params) =>
              CaveService.AddCaveFile(
                params.file,
                cave?.id as string,
                params.uid,
                params.onProgress
              )
            }
            updateFunction={FileService.UpdateFilesMetadata}
          />
        )}

        {cave?.entrances && cave?.entrances.length > 0 && selectedEntrance && (
          <>
            <PlanarianDividerComponent
              title="Geology"
              secondaryTitle="from Macrostrat"
              element={
                <Select
                  value={selectedEntrance.id}
                  style={{ width: 200 }}
                  onChange={(value) => {
                    const newEntrance = cave.entrances.find(
                      (entrance) => entrance.id === value
                    );
                    if (newEntrance) {
                      setSelectedEntrance(newEntrance);
                    }
                  }}
                >
                  {cave.entrances.map((entrance, index) => (
                    <Option
                      key={entrance.id || index}
                      value={entrance.id || index}
                    >
                      {entrance.name ? entrance.name : `Entrance ${index + 1}`}
                    </Option>
                  ))}
                </Select>
              }
            />
            {selectedEntrance.latitude && selectedEntrance.longitude && (
              <Macrostrat
                lat={selectedEntrance.latitude}
                lng={selectedEntrance.longitude}
              />
            )}

            <PlanarianDividerComponent
              title="Stream Gages"
              secondaryTitle="from USGS"
              element={
                <Row gutter={10}>
                  <Col>
                    <Select
                      value={selectedGageEntrance?.id}
                      style={{ width: 200 }}
                      onChange={(value) => {
                        const newEntrance = cave.entrances.find(
                          (entrance) => entrance.id === value
                        );
                        if (newEntrance) {
                          setSelectedGageEntrance(newEntrance);
                        }
                      }}
                    >
                      {cave.entrances.map((entrance, index) => (
                        <Option
                          key={entrance.id || index}
                          value={entrance.id || index}
                        >
                          {entrance.name
                            ? entrance.name
                            : `Entrance ${index + 1}`}
                        </Option>
                      ))}
                    </Select>
                  </Col>
                  <Col>
                    <InputNumber
                      value={gageDistance}
                      addonAfter="Miles"
                      min={1}
                      max={50}
                      onChange={(value) => setGageDistance(value as number)}
                    />
                  </Col>
                  <Col>
                    <RangePicker
                      value={gageDateRange}
                      onChange={(range) => setGageDateRange(range)}
                      style={{ width: 250 }}
                    />
                  </Col>
                </Row>
              }
            />
            {selectedGageEntrance &&
              selectedGageEntrance.latitude &&
              selectedGageEntrance.longitude && (
                <GageList
                  lat={selectedGageEntrance.latitude}
                  lng={selectedGageEntrance.longitude}
                  distanceMiles={gageDistance}
                  dateRange={gageDateRange}
                />
              )}
          </>
        )}

        {showMap && (
          <>
            <PlanarianDividerComponent title="Map" />
            {cave?.primaryEntrance !== null && (
              <div style={{ height: "590px" }}>
                <MapComponent
                  initialCenter={[
                    cave?.primaryEntrance?.latitude as number,
                    cave?.primaryEntrance?.longitude as number,
                  ]}
                  initialZoom={15}
                  showFullScreenControl
                />
              </div>
            )}
          </>
        )}
      </Card>
    </>
  );
};

export { CaveComponent };
