import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { SlidersOutlined } from "@ant-design/icons";
import {
  Button,
  Col,
  Drawer,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Tooltip,
  Typography,
} from "antd";
import { TripVm } from "../../Trip/Models/TripVm";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { ProjectService } from "../../Project/Services/ProjectService";
import { PagedResult } from "../../Search/Models/PagedResult";
import { nameof } from "../../../Shared/Helpers/StringHelpers";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { TripCardComponent } from "./TripCard";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
const { Option } = Select;
interface TripsByProjectIdComponentProps {
  projectId: string;
}

const query = window.location.search.substring(1);

const queryBuilder = new QueryBuilder<TripVm>(query);

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
  const onSearch = async () => {
    setIsAdvancedSearchOpen(false);
    await getTrips();
  };
  const colSpanProps = {
    xs: { span: 24 },
    sm: { span: 24 },
    md: { span: 12 },
    lg: { span: 8 },
    xl: { span: 6 },
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
          <Input.Search
            defaultValue={queryBuilder.getFieldValue("name") as string}
            onChange={(e) => {
              queryBuilder.filterBy(
                "name",
                QueryOperator.Contains,
                e.target.value
              );
            }}
            onSearch={onSearch}
          />
        </Col>
        <Col>
          <PlanarianButton
            icon={<SlidersOutlined />}
            onClick={(e) => setIsAdvancedSearchOpen(true)}
          >
            Advanced
          </PlanarianButton>
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
              initialValues={
                {
                  tripReport: queryBuilder.getFieldValue(
                    "tripReport"
                  ) as string,
                  tripTagTypeIds: queryBuilder.getFieldValue(
                    "tripTagTypeIds"
                  ) as string[],
                } as TripVm
              }
            >
              <Form.Item
                name={nameof<TripVm>("tripReport")}
                label="Trip Report"
              >
                <Input
                  onChange={(e) => {
                    queryBuilder.filterBy(
                      "tripReport",
                      QueryOperator.Contains,
                      e.target.value
                    );
                  }}
                />
              </Form.Item>
              <Form.Item name={nameof<TripVm>("tripTagTypeIds")} label="Tags">
                <TagSelectComponent
                  projectId={props.projectId}
                  tagType={TagType.Trip}
                  defaultValue={
                    queryBuilder.getFieldValue("tripTagTypeIds") as string[]
                  }
                  onChange={(e) => {
                    queryBuilder.filterBy(
                      "tripTagTypeIds",
                      QueryOperator.In,
                      e
                    );
                  }}
                />
              </Form.Item>
              <Form.Item
                name={nameof<TripVm>("tripMemberIds")}
                label="Trip Members"
              >
                <TagSelectComponent
                  projectId={props.projectId}
                  tagType={TagType.ProjectMember}
                  defaultValue={
                    queryBuilder.getFieldValue("tripMemberIds") as string[]
                  }
                  onChange={(e) => {
                    queryBuilder.filterBy("tripMemberIds", QueryOperator.In, e);
                  }}
                />
              </Form.Item>
              {/* <Col>
                <Row gutter={5}>
                  <Col span={24}>Number of Photos</Col>
                  <Col span={7}>
                    <InputNumber
                      size="large"
                      placeholder="0"
                      style={{ width: "100%" }}
                      onChange={(e) => {
                        queryBuilder.filterBy(
                          "numberOfPhotos",
                          QueryOperator.GreaterThanOrEqual,
                          e
                        );
                      }}
                    ></InputNumber>
                  </Col>
                  <Col span={10}>
                    <Select
                      size="large"
                      style={{ width: "100%", textAlign: "center" }}
                      defaultValue={QueryOperator.LessThanOrEqual}
                      onChange={(e) => {
                        queryBuilder.reverseOperators("numberOfPhotos", e);
                      }}
                    >
                      <Option
                        style={{ textAlign: "center" }}
                        value={QueryOperator.GreaterThanOrEqual}
                      >
                        <Tooltip title="Greater than or equal to x and less or equal to y.">
                          <div>{"x <= L <= y"}</div>
                        </Tooltip>
                      </Option>
                      <Option
                        style={{ textAlign: "center" }}
                        value={QueryOperator.GreaterThan}
                      >
                        <Tooltip title="Greater than x and less than y.">
                          <div>{"x < L < y"}</div>
                        </Tooltip>
                      </Option>
                    </Select>
                  </Col>

                  <Col span={7}>
                    <InputNumber
                      size="large"
                      style={{ width: "100%" }}
                      placeholder="10"
                      onChange={(e) => {
                        queryBuilder.filterBy(
                          "numberOfPhotos",
                          QueryOperator.LessThanOrEqual,
                          e
                        );
                      }}
                    ></InputNumber>
                  </Col>
                </Row>
              </Col> */}
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
                <TripCardComponent trip={trip} />
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
