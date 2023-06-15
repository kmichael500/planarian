import { Row, Col, Typography, Card, Form } from "antd";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { AdvancedSearchDrawerComponent } from "../../Search/Components/AdvancedSearchDrawerComponent";
import { PagedResult } from "../../Search/Models/PagedResult";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { CaveService } from "../Service/CaveService";
import { CaveVm } from "../Models/CaveVm";
import { CaveCreateButtonComponent } from "./CaveCreateButtonComponent";
import {
  convertDistance,
  getDirectionsUrl,
} from "../../../Shared/Helpers/StringHelpers";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EyeOutlined, CarOutlined } from "@ant-design/icons";
import { TagFilterFormItem } from "../../Search/Components/TagFilterFormItem";
import { TagType } from "../../Tag/Models/TagType";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { StateCountyFilterFormItem } from "../../Search/Components/StateFilterFormItem";
import { TextFilterFormItem } from "../../Search/Components/TextFilterFormItem";

const query = window.location.search.substring(1);

const queryBuilder = new QueryBuilder<CaveVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveVm>();

  useEffect(() => {
    getCaves();
  }, []);

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
        form={form}
      >
        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"narrative"}
          label={"Narrative"}
          queryOperator={QueryOperator.FreeText}
        />
        <StateCountyFilterFormItem
          queryBuilder={queryBuilder}
          stateField={"stateId"}
          stateLabel={"State"}
          countyField={"countyId"}
          countyLabel={"County"}
        />

        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"lengthFeet"}
          label={"Length (Feet)"}
        />
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"depthFeet"}
          label={"Depth (Feet)"}
        />

        <TagFilterFormItem
          tagType={TagType.EntranceStatus}
          queryBuilder={queryBuilder}
          field={"primaryEntrance.entranceStatusTagIds"}
          label={"Primary Entrance Status"}
        />
        <TagFilterFormItem
          tagType={TagType.EntranceHydrology}
          queryBuilder={queryBuilder}
          field={"primaryEntrance.entranceHydrologyTagIds"}
          label={"Primary Entrance Hydrology"}
        />
        <TagFilterFormItem
          tagType={TagType.EntranceHydrologyFrequency}
          queryBuilder={queryBuilder}
          field={"primaryEntrance.entranceHydrologyFrequencyTagIds"}
          label={"Primary Entrance Hydroloy Frequency"}
        />
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"numberOfPits"}
          label={"Numberof Pits"}
        />
        <TagFilterFormItem
          tagType={TagType.Geology}
          queryBuilder={queryBuilder}
          field={"geologyTagIds"}
          label={"Geology"}
        />
      </AdvancedSearchDrawerComponent>

      <SpinnerCardComponent spinning={isCavesLoading}>
        <CardGridComponent
          noDataDescription={"No caves found"}
          noDataCreateButton={<CaveCreateButtonComponent />}
          renderItem={(cave) => (
            // <Link to={`trip/${cave.id}`}>
            <Card
              title={`${cave.displayId} ${cave.name} `}
              actions={[
                <Link to={"/caves/" + cave.id}>
                  <PlanarianButton type="primary" icon={<EyeOutlined />}>
                    More Info
                  </PlanarianButton>
                </Link>,
                <a
                  href={getDirectionsUrl(
                    cave.primaryEntrance.latitude,
                    cave.primaryEntrance.longitude
                  )}
                  target="_blank"
                >
                  <PlanarianButton icon={<CarOutlined />}>
                    Directions
                  </PlanarianButton>
                </a>,
              ]}
            >
              <Typography.Paragraph>
                Length: {convertDistance(cave.lengthFeet)}
              </Typography.Paragraph>
              <Typography.Paragraph>
                Depth: {convertDistance(cave.depthFeet)}
              </Typography.Paragraph>
              <Typography.Paragraph>
                Elevation: {convertDistance(cave.primaryEntrance.elevationFeet)}
              </Typography.Paragraph>
              <Typography.Paragraph>
                Max Pit Depth: {convertDistance(cave.maxPitDepthFeet)}
              </Typography.Paragraph>
              <Typography.Paragraph>
                {" "}
                Geology:{" "}
                <Row>
                  {cave.geologyTagIds.map((tagId, index) => (
                    <Col key={tagId}>
                      <TagComponent tagId={tagId} />
                    </Col>
                  ))}
                </Row>
              </Typography.Paragraph>
            </Card>
            // </Link>
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
