import { Card, Typography } from "antd";
import { useContext, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { ProjectCreateButtonComponent } from "../Components/ProjectCreateButtonComponent";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { PagedResult } from "../../Search/Models/PagedResult";
import { AdvancedSearchDrawerComponent } from "../../Search/Components/AdvancedSearchDrawerComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";

const { Paragraph } = Typography;
const query = window.location.search.substring(1);

const queryBuilder = new QueryBuilder<ProjectVm>(query);

const ProjectsPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<ProjectCreateButtonComponent />]);
    setHeaderTitle(["Projects"]);
  }, []);

  let [trips, setTrips] = useState<PagedResult<ProjectVm>>();
  let [isTripsLoading, setIsTripsLoading] = useState(true);

  useEffect(() => {
    if (trips === undefined) {
      getTrips();
    }
  });

  const getTrips = async () => {
    setIsTripsLoading(true);

    const tripsResponse = await ProjectService.GetProjects(queryBuilder);
    setTrips(tripsResponse);
    setIsTripsLoading(false);
  };

  const onSearch = async () => {
    await getTrips();
  };

  return (
    <div className="site-card-wrapper">
      <AdvancedSearchDrawerComponent
        mainSearchField={"name"}
        mainSearchFieldLabel={"Name"}
        onSearch={onSearch}
        queryBuilder={queryBuilder}
      >
        <NumberComparisonFormItem
          inputType="number"
          queryBuilder={queryBuilder}
          field={"numberOfTrips"}
          label={"Number of Trips"}
        />
        <NumberComparisonFormItem
          inputType="number"
          queryBuilder={queryBuilder}
          field={"numberOfProjectMembers"}
          label={"Number of Project Members"}
        />
      </AdvancedSearchDrawerComponent>

      <SpinnerCardComponent spinning={isTripsLoading}>
        <CardGridComponent
          queryBuilder={queryBuilder}
          onSearch={onSearch}
          noDataDescription="No projects found"
          noDataCreateButton={<ProjectCreateButtonComponent />}
          renderItem={(project) => (
            <Link to={project.id}>
              <Card
                style={{ height: "100%" }}
                loading={isTripsLoading}
                hoverable
                title={project.name}
                bordered={false}
              >
                <Paragraph>
                  Project Members: {project.numberOfProjectMembers}
                </Paragraph>
                <Paragraph>Trips: {project.numberOfTrips}</Paragraph>
              </Card>
            </Link>
          )}
          itemKey={(project) => project.id}
          pagedItems={trips}
        />
      </SpinnerCardComponent>
      {/* <SpinnerCardComponent spinning={isTripsLoading}>
        <CardGridComponent
          noDataDescription="No projects found"
          noDataCreateButton={<ProjectCreateButtonComponent />}
          renderItem={(project) => (
            <Link to={project.id}>
              <Card
                style={{ height: "100%" }}
                loading={isTripsLoading}
                hoverable
                title={project.name}
                bordered={false}
              >
                <Paragraph>
                  Project Members: {project.numberOfProjectMembers}
                </Paragraph>
                <Paragraph>Trips: {project.numberOfTrips}</Paragraph>
              </Card>
            </Link>
          )}
          itemKey={(project) => project.id}
          pagedItems={trips}
        />
      </SpinnerCardComponent> */}
    </div>
  );
};

export { ProjectsPage };
