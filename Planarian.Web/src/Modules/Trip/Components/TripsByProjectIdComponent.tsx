import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Button,
  Card,
  Col,
  Divider,
  Drawer,
  Form,
  Input,
  List,
  Pagination,
  Row,
  Space,
  Typography,
} from "antd";
import { CheckCircleOutlined, CloseCircleOutlined } from "@ant-design/icons";
import { TripVm } from "../../Trip/Models/TripVm";
import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { TripSearchComponent } from "../../Trip/Components/TripSearchComponent";
import { ProjectService } from "../../Project/Services/ProjectService";
import { PagedResult } from "../../Search/Models/PagedResult";
import Sider from "antd/lib/layout/Sider";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
const { Title, Text } = Typography;
interface TripsByProjectIdComponentProps {
  projectId: string;
}

const queryBuilder = new QueryBuilder<TripVm>();

const TripsByProjectIdComponent: React.FC<TripsByProjectIdComponentProps> = (
  props
) => {
  let [trips, setTrips] = useState<PagedResult<TripVm>>();
  let [isTripsLoading, setIsTripsLoading] = useState(true);

  useEffect(() => {
    if (trips === undefined) {
      getTrips();
    }
  });

  const getTrips = async () => {
    setIsTripsLoading(true);

    const tripsResponse = await ProjectService.GetTrips(
      props.projectId,
      queryBuilder
    );
    setTrips(tripsResponse);
    setIsTripsLoading(false);
  };

  const [isAdvancedSearchOpen, setIsAdvancedSearchOpen] = useState(false);
  const onSearch = async (value?: string) => {
    if (value !== undefined) {
      queryBuilder.filterBy("name", QueryOperator.Contains, value);
    }
    setIsAdvancedSearchOpen(false);
    await getTrips();
  };

  return (
    <>
      <Row align="middle" gutter={10}>
        <Col>
          <Typography.Title level={3}>Trips</Typography.Title>
        </Col>

        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>

        <Col>
          <TripCreateButtonComponent projectId={props.projectId} />
        </Col>
      </Row>
      <Row style={{ marginBottom: 10 }} gutter={5}>
        <Col>
          <Input.Search onSearch={onSearch} />
        </Col>
        <Col>
          <Button onClick={(e) => setIsAdvancedSearchOpen(true)}>
            Advanced Search
          </Button>
          <Drawer
            open={isAdvancedSearchOpen}
            onClose={(e) => setIsAdvancedSearchOpen(false)}
          >
            <Form
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  onSearch();
                }
              }}
              layout="vertical"
            >
              <Form.Item
                name={nameof<TripVm>("tripReport")}
                label="Trip Report"
              >
                <Input
                  onChange={(e) => {
                    console.log(
                      queryBuilder.filterBy(
                        "tripReport",
                        QueryOperator.Contains,
                        e.target.value
                      )
                    );
                  }}
                />
              </Form.Item>
            </Form>
            <Button
              onClick={() => {
                onSearch();
              }}
            >
              Search
            </Button>
          </Drawer>
        </Col>
      </Row>

      <SpinnerCardComponent spinning={isTripsLoading}>
        <CardGridComponent
          pagination={{
            onChange: async (pageNumber, pageSize) => {
              queryBuilder.changePage(pageNumber, pageSize);
              await getTrips();
            },
            showSizeChanger: false,
            responsive: true,
            current: trips?.pageNumber,
            pageSize: trips?.pageSize,
            position: "bottom",
            total: trips?.totalCount,
          }}
          noDataDescription={"No trips found"}
          noDataCreateButton={
            <TripCreateButtonComponent projectId={props.projectId} />
          }
          items={trips?.results.map((trip) => ({
            item: (
              <Link to={`trip/${trip.id}`}>
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
                  loading={isTripsLoading}
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
                  </>
                </Card>
              </Link>
            ),
            key: trip.id,
          }))}
        />
      </SpinnerCardComponent>
    </>
  );
};

export { TripsByProjectIdComponent };
