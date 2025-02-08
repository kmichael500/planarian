import { useEffect, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { CarOutlined, CloudUploadOutlined } from "@ant-design/icons";

import {
  Card,
  Col,
  Collapse,
  Descriptions,
  Row,
  Space,
  Tag,
  Tooltip,
} from "antd";
import { TagComponent } from "../../Tag/Components/TagComponent";
import {
  convertDistance,
  defaultIfEmpty,
  formatDate,
  formatNumber,
  getDirectionsUrl,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { StateTagComponent } from "../../../Shared/Components/Display/StateTagComponent";
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

const { Panel } = Collapse;

export interface CaveComponentOptions {
  showMap?: boolean;
}
export interface CaveComponentProps {
  cave?: CaveVm;
  isLoading: boolean;
  options?: CaveComponentOptions;
  updateCave?: () => void;
}

// Common function to generate tags
const generateTags = (tagIds: string[] | undefined) => {
  if (!tagIds || tagIds?.length === 0) {
    return defaultIfEmpty(null);
  }

  return tagIds?.map((tagId) => (
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
}: CaveComponentProps) => {
  const [isUploading, setIsUploading] = useState(false);
  const { isFeatureEnabled } = useFeatureEnabled();

  const [showMap, setShowMap] = useState(true);

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
        {defaultIfEmpty(convertDistance(cave?.lengthFeet))}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveDepthFeet) && (
      <Descriptions.Item label="Depth" key="depth">
        {defaultIfEmpty(convertDistance(cave?.maxPitDepthFeet))}
      </Descriptions.Item>
    ),
    isFeatureEnabled(FeatureKey.EnabledFieldCaveMaxPitDepthFeet) && (
      <Descriptions.Item label="Max Pit Depth" key="max-pit-depth">
        {defaultIfEmpty(convertDistance(cave?.maxPitDepthFeet))}
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
          {entrance.latitude}, {entrance.longitude}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceDescription) && (
        <Descriptions.Item label="Description" key="description">
          {entrance.description}
        </Descriptions.Item>
      ),
      isFeatureEnabled(FeatureKey.EnabledFieldEntranceElevation) && (
        <Descriptions.Item label="Elevation" key="elevation">
          {defaultIfEmpty(convertDistance(entrance.elevationFeet))}
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
          {defaultIfEmpty(convertDistance(entrance.pitFeet))}
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

  return (
    <>
      <Card
        bodyStyle={!isLoading ? { paddingTop: "0px" } : {}}
        loading={isLoading}
      >
        <PlanarianDividerComponent title="Information" />
        <Descriptions bordered>{descriptionItems}</Descriptions>
        {cave?.entrances && cave?.entrances.length > 0 && (
          <>
            <PlanarianDividerComponent title="Entrances" />

            <Collapse bordered defaultActiveKey={["0"]}>
              {cave?.entrances.map((entrance, index) => (
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
                  <Descriptions bordered>
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

        {showMap && (
          <>
            <PlanarianDividerComponent title="Map" />
            {cave?.primaryEntrance !== null && (
              <div style={{ height: "400px" }}>
                <MapComponent
                  initialCenter={[
                    cave?.primaryEntrance?.latitude as number,
                    cave?.primaryEntrance?.longitude as number,
                  ]}
                  initialZoom={15}
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
