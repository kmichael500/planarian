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
  formatDateTime,
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
          <Descriptions.Item label="ID">{cave?.displayId}</Descriptions.Item>
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
            {convertDistance(cave?.lengthFeet)}
          </Descriptions.Item>
          <Descriptions.Item label="Depth">
            {convertDistance(cave?.depthFeet)}
          </Descriptions.Item>
          <Descriptions.Item label="Max Pit Depth">
            {convertDistance(cave?.maxPitDepthFeet)}
          </Descriptions.Item>
          <Descriptions.Item label="Number of Pits">
            {cave?.numberOfPits}
          </Descriptions.Item>
          <Descriptions.Item label="Reported On">
            {formatDateTime(cave?.reportedOn)}
          </Descriptions.Item>
          <Descriptions.Item label="Reported By">
            <Row>
              {cave?.reportedByNameTagIds.map((e) => (
                <Col key={e}>
                  <TagComponent tagId={e} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Geology">
            <Row>
              {cave?.geologyTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Geologic Age">
            <Row>
              {cave?.geologicAgeTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Physigraphic Province">
            <Row>
              {cave?.physiographicProvinceTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Biology">
            <Row>
              {cave?.biologyTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Archeology">
            <Row>
              {cave?.archeologyTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Map Status">
            <Row>
              {cave?.mapStatusTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Cartographers">
            <Row>
              {cave?.cartographerNameTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
          </Descriptions.Item>
          <Descriptions.Item label="Other">
            <Row>
              {cave?.otherTagIds.map((tagId) => (
                <Col key={tagId}>
                  <TagComponent tagId={tagId} />
                </Col>
              ))}
            </Row>
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
                        {convertDistance(entrance.elevationFeet)}
                      </Descriptions.Item>
                    )}
                    <Descriptions.Item label="Location Quality">
                      <TagComponent tagId={entrance.locationQualityTagId} />
                    </Descriptions.Item>
                    <Descriptions.Item label="Name">
                      {entrance.name}
                    </Descriptions.Item>

                    <Descriptions.Item label="Reported On">
                      {formatDateTime(entrance.reportedOn)}
                    </Descriptions.Item>

                    <Descriptions.Item label="Reported By">
                      <Row>
                        {entrance?.reportedByNameTagIds.map((e) => (
                          <Col key={e}>
                            <TagComponent tagId={e} />
                          </Col>
                        ))}
                      </Row>
                    </Descriptions.Item>
                    <Descriptions.Item label="Pit Depth">
                      {convertDistance(entrance.pitFeet)}
                    </Descriptions.Item>
                    <Descriptions.Item label="Status">
                      <Row>
                        {entrance.entranceStatusTagIds.map((tagId) => (
                          <Col key={tagId}>
                            <TagComponent tagId={tagId} key={tagId} />
                          </Col>
                        ))}
                      </Row>
                    </Descriptions.Item>

                    <Descriptions.Item label="Field Indication">
                      <Row>
                        {entrance.fieldIndicationTagIds.map((tagId) => (
                          <Col key={tagId}>
                            <TagComponent tagId={tagId} />
                          </Col>
                        ))}
                      </Row>
                    </Descriptions.Item>
                    <Descriptions.Item label="Hydrology">
                      <Row>
                        {entrance.entranceHydrologyTagIds.map((tagId) => (
                          <Col key={tagId}>
                            <TagComponent tagId={tagId} />
                          </Col>
                        ))}
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
