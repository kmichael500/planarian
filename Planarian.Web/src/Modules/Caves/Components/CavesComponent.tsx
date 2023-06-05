import { Row, Col, Typography } from "antd";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { AdvancedSearchDrawerComponent } from "../../Search/Components/AdvancedSearchDrawerComponent";
import { PagedResult } from "../../Search/Models/PagedResult";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { TripCardComponent } from "../../Trip/Components/TripCard";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { CaveService } from "../Service/CaveService";
import { CaveVm } from "../Models/CaveVm";
import { CaveCreateButtonComponent } from "./CaveCreateButtonComponent";

const query = window.location.search.substring(1);

const queryBuilder = new QueryBuilder<CaveVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);

  useEffect(() => {
    if (caves === undefined) {
      getCaves();
    }
  });

  const getCaves = async () => {
    setIsCavesLoading(true);

    const cavesResponse = await CaveService.GetCaves(queryBuilder);
    setCaves(cavesResponse);
    setIsCavesLoading(false);
  };

  const onSearch = async () => {
    await getCaves();
  };

  return (
    <>
      <AdvancedSearchDrawerComponent
        mainSearchField={"name"}
        mainSearchFieldLabel={"Name"}
        onSearch={onSearch}
        queryBuilder={queryBuilder}
      ></AdvancedSearchDrawerComponent>

      <SpinnerCardComponent spinning={isCavesLoading}>
        <CardGridComponent
          noDataDescription={"No caves found"}
          noDataCreateButton={<CaveCreateButtonComponent />}
          renderItem={(cave) => (
            <Link to={`trip/${cave.id}`}>
              {/* <TripCardComponent trip={trip} /> */}
            </Link>
          )}
          itemKey={(trip) => trip.id}
          pagedItems={caves}
          queryBuilder={queryBuilder}
          onSearch={onSearch}
        />
      </SpinnerCardComponent>
    </>
  );
};

export { CavesComponent };
