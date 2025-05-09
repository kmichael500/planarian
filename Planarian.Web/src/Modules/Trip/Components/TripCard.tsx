import { Card, Row, Col, Divider, Typography } from "antd";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
import { TripVm } from "../Models/TripVm";
import { CheckCircleOutlined, CloseCircleOutlined } from "@ant-design/icons";
import { formatDate } from "../../../Shared/Helpers/StringHelpers";
const { Text } = Typography;

export interface TripCardProps {
  trip: TripVm;
}

const TripCardComponent: React.FC<TripCardProps> = ({ trip }) => {
  return (
    <Card
      style={{ height: "100%" }}
      title={
        <>
          {trip.name}{" "}
          <Row>
            <UserAvatarGroupComponent
              size={"small"}
              maxCount={4}
              userIds={trip.tripMemberIds}
            />
          </Row>
        </>
      }
      bordered={false}
      hoverable
    >
      <>
        <Row>
          {trip.tripTagTypeIds.map((tagId, index) => (
            <Col key={tagId}>
              <TagComponent tagId={tagId} />
            </Col>
          ))}
        </Row>
        <Divider />

        <Row>
          <Col>
            <Text>Description: {trip.description}</Text>
          </Col>
        </Row>
        <Row>
          <Col>
            <Text>
              Trip Report:{" "}
              {trip.isTripReportCompleted ? (
                <CheckCircleOutlined />
              ) : (
                <CloseCircleOutlined />
              )}
            </Text>
          </Col>
        </Row>
        <Row>
          <Col>
            <Text>Photos: {trip.numberOfPhotos}</Text>
          </Col>
        </Row>
        <Row>
          <Col>Created On: {formatDate(trip.createdOn)}</Col>
        </Row>
        <Row>
          <Col>
            <Text>
              Modified On:{" "}
              {trip.modifiedOn ? formatDate(trip.modifiedOn) : "Never"}
            </Text>
          </Col>
        </Row>
      </>
    </Card>
  );
};

export { TripCardComponent };
