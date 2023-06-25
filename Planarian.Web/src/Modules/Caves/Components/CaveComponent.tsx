import { ReactNode, useState } from "react";
import { CaveVm } from "../Models/CaveVm";
import { CarOutlined, EditOutlined } from "@ant-design/icons";
import {
  Card,
  Col,
  Collapse,
  Descriptions,
  Divider,
  Row,
  Space,
  Tag,
  Tooltip,
  Typography,
} from "antd";
import { TagComponent } from "../../Tag/Components/TagComponent";
import {
  convertDistance,
  formatDateTime,
  getDirectionsUrl,
  isNullOrWhiteSpace,
} from "../../../Shared/Helpers/StringHelpers";
import MapPointComponent from "./MapPointComponent";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { StateTagComponent } from "../../../Shared/Components/Display/StateTagComponent";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { UploadComponent } from "../../Files/Components/FIleUploadComponent";
import { CloudDownloadOutlined } from "@ant-design/icons";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { FileCardComponent } from "../../Files/Components/FileCardComponent";

const { Panel } = Collapse;
const { Paragraph } = Typography;

export interface CaveComponentProps {
  cave?: CaveVm;
  isLoading: boolean;
}

const CaveComponent = ({ cave, isLoading }: CaveComponentProps) => {
  return (
    <>
      <Card loading={isLoading}>
        <Divider orientation="left">Information</Divider>

        <Descriptions bordered>
          <Descriptions.Item label="ID">{cave?.displayId}</Descriptions.Item>
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
            {cave?.reportedByName}
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
        </Descriptions>
        <Divider orientation="left">Entrances</Divider>
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
                <Descriptions.Item label="Elevation">
                  {convertDistance(entrance.elevationFeet)}
                </Descriptions.Item>
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
                  {entrance.reportedByName}
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
                <Descriptions.Item label="Hydrology Frequency">
                  <Row>
                    {entrance.entranceHydrologyFrequencyTagIds.map((tagId) => (
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
        <br />
        <Divider orientation="left">Narrative</Divider>
        <ParagraphDisplayComponent text={cave?.narrative} />
        <Divider orientation="left">Files</Divider>
        <CardGridComponent
          renderItem={(file) => {
            return <FileCardComponent file={file} />;
          }}
          itemKey={(item) => {
            return item.id;
          }}
          items={cave?.files}
        ></CardGridComponent>

        <UploadComponent caveId={cave?.id} />
      </Card>
      {/* <Card title="Files">
        <UploadComponent caveId={cave?.id} />
      </Card> */}
    </>
  );
};

export { CaveComponent };
