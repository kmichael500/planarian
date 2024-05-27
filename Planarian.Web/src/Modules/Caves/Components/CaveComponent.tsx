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
  formatDateTime,
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

  useEffect(() => {
    if (cave?.primaryEntrance) {
    }
  }, [cave, isLoading]);

  return (
    <>
      <Card
        bodyStyle={!isLoading ? { paddingTop: "0px" } : {}}
        loading={isLoading}
      >
        <PlanarianDividerComponent title="Information" />
        <Descriptions bordered>
          <Descriptions.Item label="ID">
            {defaultIfEmpty(cave?.displayId)}
          </Descriptions.Item>
          <Descriptions.Item label="Alternative Names">
            <Row>
              {cave?.alternateNames.map((name) => (
                <Col key={name}>
                  <Tag>{name}</Tag>
                </Col>
              ))}
            </Row>
          </Descriptions.Item>

          <Descriptions.Item label="State">
            <StateTagComponent stateId={cave?.stateId} />
          </Descriptions.Item>
          <Descriptions.Item label="County">
            <CountyTagComponent countyId={cave?.countyId} />
          </Descriptions.Item>
          <Descriptions.Item label="Length">
            {defaultIfEmpty(convertDistance(cave?.lengthFeet))}
          </Descriptions.Item>
          <Descriptions.Item label="Depth">
            {defaultIfEmpty(convertDistance(cave?.depthFeet))}
          </Descriptions.Item>
          <Descriptions.Item label="Max Pit Depth">
            {defaultIfEmpty(convertDistance(cave?.maxPitDepthFeet))}
          </Descriptions.Item>
          <Descriptions.Item label="Number of Pits">
            {defaultIfEmpty(formatNumber(cave?.numberOfPits)).toString()}
          </Descriptions.Item>
          <Descriptions.Item label="Reported On">
            {defaultIfEmpty(formatDateTime(cave?.reportedOn))}
          </Descriptions.Item>
          <Descriptions.Item label="Reported By">
            <Row>{generateTags(cave?.reportedByNameTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Geology">
            <Row>{generateTags(cave?.geologyTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Geologic Age">
            <Row>{generateTags(cave?.geologicAgeTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Physigraphic Province">
            <Row>{generateTags(cave?.physiographicProvinceTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Biology">
            <Row>{generateTags(cave?.biologyTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Archeology">
            <Row>{generateTags(cave?.archeologyTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Map Status">
            <Row>{generateTags(cave?.mapStatusTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Cartographers">
            <Row>{generateTags(cave?.cartographerNameTagIds)}</Row>
          </Descriptions.Item>
          <Descriptions.Item label="Other">
            <Row>{generateTags(cave?.otherTagIds)}</Row>
          </Descriptions.Item>
        </Descriptions>
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
                    <Descriptions.Item
                      label={
                        <Space>
                          Coordinates
                          <a
                            href={getDirectionsUrl(
                              entrance.latitude,
                              entrance.longitude
                            )}
                            target="_blank"
                          >
                            <Tooltip title="Directions">
                              <CarOutlined />
                            </Tooltip>
                          </a>
                        </Space>
                      }
                    >
                      {entrance.latitude}, {entrance.longitude}
                    </Descriptions.Item>
                    {cave.primaryEntrance && (
                      <Descriptions.Item label="Elevation">
                        {defaultIfEmpty(
                          convertDistance(entrance.elevationFeet)
                        )}
                      </Descriptions.Item>
                    )}
                    <Descriptions.Item label="Location Quality">
                      <TagComponent tagId={entrance.locationQualityTagId} />
                    </Descriptions.Item>
                    <Descriptions.Item label="Name">
                      {entrance.name}
                    </Descriptions.Item>

                    <Descriptions.Item label="Reported On">
                      {defaultIfEmpty(formatDateTime(entrance.reportedOn))}
                    </Descriptions.Item>

                    <Descriptions.Item label="Reported By">
                      <Row>{generateTags(entrance?.reportedByNameTagIds)}</Row>
                    </Descriptions.Item>
                    <Descriptions.Item label="Pit Depth">
                      {defaultIfEmpty(convertDistance(entrance.pitFeet))}
                    </Descriptions.Item>
                    <Descriptions.Item label="Status">
                      <Row>{generateTags(entrance.entranceStatusTagIds)}</Row>
                    </Descriptions.Item>

                    <Descriptions.Item label="Field Indication">
                      <Row>{generateTags(entrance.fieldIndicationTagIds)}</Row>
                    </Descriptions.Item>
                    <Descriptions.Item label="Hydrology">
                      <Row>
                        {generateTags(entrance.entranceHydrologyTagIds)}
                      </Row>
                    </Descriptions.Item>
                  </Descriptions>
                  {!isNullOrWhiteSpace(entrance.description) && (
                    <>
                      <br />
                      {entrance.description}
                    </>
                  )}
                </Panel>
              ))}
            </Collapse>
          </>
        )}

        <PlanarianDividerComponent title="Narrative" />
        <ParagraphDisplayComponent text={cave?.narrative} />

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

        {options?.showMap !== false && (
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
