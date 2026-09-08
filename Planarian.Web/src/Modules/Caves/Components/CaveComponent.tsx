import { useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { CloudUploadOutlined } from "@ant-design/icons";
import { FeatureCollection, GeoJsonProperties, Geometry } from "geojson";
import {
  Col,
  Collapse,
  Row,
  Space,
  Select,
  DatePicker,
  InputNumber,
  Skeleton,
} from "antd";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import {
  defaultIfEmpty,
  DistanceFormat,
  formatCoordinates,
  formatDate,
  formatDistance,
  formatNumber,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  PlanarianDescription,
  type PlanarianDescriptionItem,
} from "../../../Shared/Components/Buttons/PlanarianDescription";
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
import dayjs, { Dayjs } from "dayjs";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { StateTagComponent } from "../../../Shared/Components/Display/StateTagComponent";
import { GageList } from "../../Map/Components/GaugeList";
import { PublicAccessDetails } from "../../Map/Components/PublicAccesDetails";
import { PlanarianDateRange } from "../../../Shared/Components/Buttons/PlanarianDateRange";
import { GeoJsonSaveModal } from "./GeoJsonSaveModal";
import { DistanceFromMeComponent } from "../../../Shared/Components/Display/DistanceFromMeComponent";

const { Panel } = Collapse;
const { Option } = Select;
const { RangePicker } = DatePicker;

const SkeletonDescriptionGrid = ({ rows }: { rows: number }) => (
  <PlanarianDescription
    copyable={false}
    items={Array.from({ length: rows }, (_, index) => ({
      key: index,
      label: <Skeleton.Input active size="small" />,
      children: <Skeleton.Input active block size="small" />,
    }))}
  />
);

const CaveDetailSkeleton = () => (
  <>
    <PlanarianDividerComponent title="Information" hideTopSpacing />
    <SkeletonDescriptionGrid rows={10} />

    <PlanarianDividerComponent title="Entrances" />
    <SkeletonDescriptionGrid rows={6} />

    <PlanarianDividerComponent title="Narrative" />
    <Skeleton active paragraph={{ rows: 4 }} title={false} />

    <PlanarianDividerComponent title="Files" />
    <Skeleton active paragraph={{ rows: 3 }} title={false} />

    <PlanarianDividerComponent title="Map" />
    <Skeleton.Node active style={{ height: 360, width: "100%" }} />
  </>
);

export interface CaveComponentOptions {
  showMap?: boolean;
  inCardContainer?: boolean;
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
  return (
    <Space size={[8, 8]} wrap>
      {tagIds.map((tagId) => (
        <TagComponent key={tagId} tagId={tagId} />
      ))}
    </Space>
  );
};

const isDescriptionItem = (
  item: PlanarianDescriptionItem | false
): item is PlanarianDescriptionItem => item !== false;

const CaveComponent = ({
  cave,
  isLoading,
  options = {}, // Default to empty object
  updateCave,
  hasEditPermission,
}: CaveComponentProps) => {
  // Set default for inCardContainer within options
  const inCardContainer = options.inCardContainer !== false; // Default to true unless explicitly set to false

  const [isUploading, setIsUploading] = useState(false);
  const { isFeatureEnabled } = useFeatureEnabled();

  const [showMap, setShowMap] = useState(true);

  const [selectedEntrance, setSelectedEntrance] = useState<EntranceVm | null>(
    null
  );
  const [selectedGageEntrance, setSelectedGageEntrance] =
    useState<EntranceVm | null>(null);

  const [gageDateRange, setGageDateRange] = useState<
    [Dayjs | null, Dayjs | null]
  >([dayjs().subtract(1, "month"), dayjs()]);

  const [gageDistance, setGageDistance] = useState<number>(25);

  const [showGeology, setShowGeology] = useState(false);
  const [showGages, setShowGages] = useState(false);

  const [geoJsonToSave, setGeoJsonToSave] = useState<string | null>(null);
  const [isGeoJsonModalVisible, setIsGeoJsonModalVisible] = useState(false);

  const handleGeoJsonReceived = (
    data: FeatureCollection<Geometry, GeoJsonProperties>[]
  ) => {
    if (data && data.length > 0) {
      const geoJsonString = JSON.stringify(data, null, 2);
      setGeoJsonToSave(geoJsonString);
      setIsGeoJsonModalVisible(true);
    } else {
      // Optionally handle the case when data is empty.
      setGeoJsonToSave(null);
      setIsGeoJsonModalVisible(false);
    }
  };

  const descriptionItemCandidates: (PlanarianDescriptionItem | false)[] = [
    isFeatureEnabled(FeatureKey.EnabledFieldCaveId) && {
      key: "id",
      label: "ID",
      children: cave?.displayId,
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveAlternateNames) && {
      key: "alternative-names",
      label: "Alternative Names",
      children: (
        <Row>
          {cave?.alternateNames.length === 0 && (
            <Col>{defaultIfEmpty(null)}</Col>
          )}
          {cave?.alternateNames.map((name) => (
            <Col key={name}>
              <PlanarianTag>{name}</PlanarianTag>
            </Col>
          ))}
        </Row>
      ),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveState) && {
      key: "state",
      label: "State",
      children: <StateTagComponent stateId={cave?.stateId} />,
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveCounty) && {
      key: "county",
      label: "County",
      children: <CountyTagComponent countyId={cave?.countyId} />,
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveLengthFeet) && {
      key: "length",
      label: "Length",
      children: defaultIfEmpty(formatDistance(cave?.lengthFeet)),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveDepthFeet) && {
      key: "depth",
      label: "Depth",
      children: defaultIfEmpty(
        formatDistance(cave?.depthFeet, DistanceFormat.feet)
      ),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveMaxPitDepthFeet) && {
      key: "max-pit-depth",
      label: "Max Pit Depth",
      children: defaultIfEmpty(
        formatDistance(cave?.maxPitDepthFeet, DistanceFormat.feet)
      ),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveNumberOfPits) && {
      key: "number-of-pits",
      label: "Number of Pits",
      children: defaultIfEmpty(formatNumber(cave?.numberOfPits)),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedOn) && {
      key: "reported-on",
      label: "Reported On",
      children: cave?.reportedOn
        ? formatDate(cave.reportedOn)
        : defaultIfEmpty(null),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveReportedByNameTags) && {
      key: "reported-by",
      label: "Reported By",
      children: generateTags(cave?.reportedByNameTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologyTags) && {
      key: "geology",
      label: "Geology",
      children: generateTags(cave?.geologyTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveGeologicAgeTags) && {
      key: "geologic-age",
      label: "Geologic Age",
      children: generateTags(cave?.geologicAgeTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCavePhysiographicProvinceTags) && {
      key: "physiographic-province",
      label: "Physiographic Province",
      children: generateTags(cave?.physiographicProvinceTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveBiologyTags) && {
      key: "biology",
      label: "Biology",
      children: generateTags(cave?.biologyTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveArcheologyTags) && {
      key: "archeology",
      label: "Archeology",
      children: generateTags(cave?.archeologyTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveMapStatusTags) && {
      key: "map-status",
      label: "Map Status",
      children: generateTags(cave?.mapStatusTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveCartographerNameTags) && {
      key: "cartographers",
      label: "Cartographers",
      children: generateTags(cave?.cartographerNameTagIds),
    },
    isFeatureEnabled(FeatureKey.EnabledFieldCaveOtherTags) && {
      key: "other",
      label: "Other",
      children: generateTags(cave?.otherTagIds),
    },
  ];
  const descriptionItems = descriptionItemCandidates.filter(isDescriptionItem);

  const entranceItems = (entrance: EntranceVm): PlanarianDescriptionItem[] => {
    const items: (PlanarianDescriptionItem | false)[] = [
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceCoordinates) && {
        key: "coordinates",
        label: (
          <Space direction="vertical" size={0}>
            <span>Coordinates</span>
            <DistanceFromMeComponent
              latitude={entrance.latitude}
              longitude={entrance.longitude}
            />
          </Space>
        ),
        copyLabel: "Coordinates",
        copyText: `${entrance.latitude}, ${entrance.longitude}`,
        children: formatCoordinates(entrance.latitude, entrance.longitude),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceDescription) && {
        key: "description",
        label: "Description",
        children: entrance.description,
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceElevation) && {
        key: "elevation",
        label: "Elevation",
        children: defaultIfEmpty(
          formatDistance(entrance.elevationFeet, DistanceFormat.feet)
        ),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceLocationQuality) && {
        key: "location-quality",
        label: "Location Quality",
        children: <TagComponent tagId={entrance.locationQualityTagId} />,
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceName) && {
        key: "name",
        label: "Name",
        children: entrance.name,
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedOn) && {
        key: "reported-on",
        label: "Reported On",
        children: entrance.reportedOn
          ? formatDate(entrance.reportedOn)
          : defaultIfEmpty(null),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceReportedByNameTags) && {
        key: "reported-by",
        label: "Reported By",
        children: generateTags(entrance.reportedByNameTagIds),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntrancePitDepth) && {
        key: "pit-depth",
        label: "Pit Depth",
        children: defaultIfEmpty(formatDistance(entrance.pitFeet)),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceStatusTags) && {
        key: "status",
        label: "Status",
        children: generateTags(entrance.entranceStatusTagIds),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceFieldIndicationTags) && {
        key: "field-indication",
        label: "Field Indication",
        children: generateTags(entrance.fieldIndicationTagIds),
      },
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceHydrologyTags) && {
        key: "hydrology",
        label: "Hydrology",
        children: generateTags(entrance.entranceHydrologyTagIds),
      },
      {
        key: "land-access",
        label: "Land Access",
        span: "filled",
        children: (
          <PublicAccessDetails
            lat={entrance.latitude}
            lng={entrance.longitude}
          />
        ),
      },
    ];

    return items.filter(isDescriptionItem);
  };

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

  const content = (
    <>
      <PlanarianDividerComponent title="Information" hideTopSpacing />
      <PlanarianDescription items={descriptionItems} />

      {cave?.entrances && cave?.entrances.length > 0 && (
        <>
          <PlanarianDividerComponent title="Entrances" />
          <Collapse bordered defaultActiveKey={["0"]} size="small">
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
                          <PlanarianTag>Primary</PlanarianTag>
                        </>
                      )}
                    </Row>
                  </>
                }
                key={index}
              >
                <PlanarianDescription
                  items={entranceItems(entrance)}
                  size="small"
                />
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
            secondaryTitle="from Macrostrat and NGMDB Map Viewer"
            element={
              <div style={{ textAlign: "right" }}>
                <a onClick={() => setShowGeology(!showGeology)}>
                  {showGeology ? "Show less" : "Show more"}
                </a>
              </div>
            }
          />
          {!showGeology && (
            <div style={{ marginBottom: "8px" }}>
              <p>
                Access geological data through Macrostrat's comprehensive
                database and view geologic maps from the National Geologic Map
                Database Mapviewer.
              </p>
            </div>
          )}
          {showGeology &&
            selectedEntrance.latitude &&
            selectedEntrance.longitude && (
              <div>
                {cave.entrances.length > 1 && (
                  <Row style={{ marginBottom: "16px" }}>
                    <Col>
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
                            {entrance.name
                              ? entrance.name
                              : `Entrance ${index + 1}`}
                          </Option>
                        ))}
                      </Select>
                    </Col>
                  </Row>
                )}
                <h4>Geology Information</h4>
                <Macrostrat
                  lat={selectedEntrance.latitude}
                  lng={selectedEntrance.longitude}
                />
              </div>
            )}

          <PlanarianDividerComponent
            title="Stream Gages"
            secondaryTitle="from USGS NWIS"
            element={
              <div style={{ textAlign: "right" }}>
                <a onClick={() => setShowGages(!showGages)}>
                  {showGages ? "Show less" : "Show more"}
                </a>
              </div>
            }
          />
          {!showGages && (
            <div style={{ marginBottom: "8px" }}>
              <p>
                View real-time water data from USGS's network of over 11,800
                streamgages across the United States. These monitoring stations
                measure and transmit water levels and flow rates, providing
                valuable information about local water conditions.
              </p>
            </div>
          )}
          {showGages &&
            selectedGageEntrance &&
            selectedGageEntrance.latitude &&
            selectedGageEntrance.longitude && (
              <div style={{}}>
                <Row gutter={[16, 16]}>
                  {cave.entrances.length > 1 && (
                    <Col xs={24} sm={24} md={8} lg={8}>
                      <Select
                        value={selectedGageEntrance?.id}
                        style={{ width: "100%" }}
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
                  )}
                  <Col xs={24} sm={12} md={6} lg={6}>
                    <InputNumber
                      value={gageDistance}
                      addonAfter="Miles"
                      min={1}
                      max={50}
                      onChange={(value) => setGageDistance(value as number)}
                      style={{ width: "100%" }}
                    />
                  </Col>
                  <Col
                    style={{ marginBottom: "16px" }}
                    xs={24}
                    sm={12}
                    md={10}
                    lg={10}
                  >
                    <PlanarianDateRange
                      value={gageDateRange}
                      onChange={(range, dateStrings) =>
                        setGageDateRange(range || [null, null])
                      }
                    />
                  </Col>
                </Row>

                <GageList
                  lat={selectedGageEntrance.latitude}
                  lng={selectedGageEntrance.longitude}
                  distanceMiles={gageDistance}
                  dateRange={gageDateRange}
                />
              </div>
            )}
        </>
      )}

      {showMap && options && options.showMap == true && (
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
                showSearchBar={false}
                showGeolocateControl={false}
                onShapefileUploaded={handleGeoJsonReceived}
              />
            </div>
          )}
        </>
      )}
    </>
  );

  return (
    <>
      {inCardContainer ? isLoading ? <CaveDetailSkeleton /> : content : content}

      {geoJsonToSave && (
        <GeoJsonSaveModal
          isVisible={isGeoJsonModalVisible}
          caveId={cave?.id as string}
          geoJson={geoJsonToSave}
          onCancel={() => {
            setIsGeoJsonModalVisible(false);
            setGeoJsonToSave(null);
          }}
          onSaved={() => {
            setIsGeoJsonModalVisible(false);
            setGeoJsonToSave(null);
            updateCave && updateCave();
          }}
        />
      )}
    </>
  );
};

export { CaveComponent };
