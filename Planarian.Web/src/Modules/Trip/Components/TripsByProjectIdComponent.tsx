import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Col, Row, Typography } from "antd";
import { TripVm } from "../../Trip/Models/TripVm";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { ProjectService } from "../../Project/Services/ProjectService";
import { PagedResult } from "../../Search/Models/PagedResult";
import { TripCardComponent } from "./TripCard";
import { TagType } from "../../Tag/Models/TagType";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { TagFilterFormItem } from "../../Search/Components/TagFilterFormItem";
import { TextFilterFormItem } from "../../Search/Components/TextFilterFormItem";
import { SearchFormComponent as AdvancedSearchDrawerComponent } from "../../Search/Components/AdvancedSearchDrawerComponent";
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

  const onSearch = async () => {
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

      <AdvancedSearchDrawerComponent
        mainSearchField={"name"}
        mainSearchFieldLabel={"Name"}
        onSearch={onSearch}
        queryBuilder={queryBuilder}
      >
        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"tripReport"}
          label={"Trip Report"}
        />

        <TagFilterFormItem
          projectId={props.projectId}
          tagType={TagType.Trip}
          queryBuilder={queryBuilder}
          field={"tripTagTypeIds"}
          label={"Tags"}
        />
        <TagFilterFormItem
          projectId={props.projectId}
          tagType={TagType.ProjectMember}
          queryBuilder={queryBuilder}
          field={"tripMemberIds"}
          label={"Trip Members"}
        />
        <NumberComparisonFormItem
          queryBuilder={queryBuilder}
          field={"numberOfPhotos"}
          label={"Number of Photos"}
        />
      </AdvancedSearchDrawerComponent>

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
