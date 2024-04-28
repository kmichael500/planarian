import { Row, Col, Typography, Form, Divider } from "antd";
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
import { CaveSearchVm } from "../Models/CaveSearchVm";
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
import { GridCard } from "../../../Shared/Components/CardGrid/GridCard";

const query = window.location.search.substring(1);

const queryBuilder = new QueryBuilder<CaveSearchVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchVm>();

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
        mainSearchFieldLabel={"Name or ID"}
        onSearch={onSearch}
        queryBuilder={queryBuilder}
        form={form}
      >
        <Divider>Cave</Divider>

        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"narrative"}
          label={"Narrative"}
          queryOperator={QueryOperator.FreeText}
        />
        <StateCountyFilterFormItem
          queryBuilder={queryBuilder}
          autoSelectFirst={true}
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
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"elevationFeet"}
          label={"Elevation (Feet)"}
        />
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"numberOfPits"}
          label={"Number of Pits (Feet)"}
        />
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"maxPitDepthFeet"}
          label={"Max Pit Depth (Feet)"}
        />

        <TagFilterFormItem
          tagType={TagType.MapStatus}
          queryBuilder={queryBuilder}
          field={"mapStatuses"}
          label={"Map Status"}
        />
        <TagFilterFormItem
          tagType={TagType.People}
          queryBuilder={queryBuilder}
          field={"cartographerNamePeopleTagIds"}
          label={"Cartographer Names"}
        />
        <TagFilterFormItem
          tagType={TagType.Geology}
          queryBuilder={queryBuilder}
          field={"geologyTagIds"}
          label={"Geology"}
        />
        <TagFilterFormItem
          tagType={TagType.GeologicAge}
          queryBuilder={queryBuilder}
          field={"geologicAgeTagIds"}
          label={"Geologic Age"}
        />
        <TagFilterFormItem
          tagType={TagType.PhysiographicProvince}
          queryBuilder={queryBuilder}
          field={"physiographicProvinceTagIds"}
          label={"Physiographic Province"}
        />
        <TagFilterFormItem
          tagType={TagType.Biology}
          queryBuilder={queryBuilder}
          field={"biologyTagIds"}
          label={"Biology"}
        />
        <TagFilterFormItem
          tagType={TagType.Archeology}
          queryBuilder={queryBuilder}
          field={"archaeologyTagIds" as any}
          label={"Archeology"}
        />
        <TagFilterFormItem
          tagType={TagType.CaveOther}
          queryBuilder={queryBuilder}
          field={"caveOtherTagIds"}
          label={"Other"}
        />
        <TagFilterFormItem
          tagType={TagType.People}
          queryBuilder={queryBuilder}
          field={"caveReportedByNameTagIds"}
          label={"Reported By"}
        />
        <NumberComparisonFormItem
          inputType={"date"}
          queryBuilder={queryBuilder}
          field={"caveReportedOnDate"}
          label={"Reported On"}
        />

        <Divider>Entrance</Divider>

        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"entranceDescription"}
          label={"Entrance Description"}
          queryOperator={QueryOperator.FreeText}
        />
        <TagFilterFormItem
          tagType={TagType.EntranceStatus}
          queryBuilder={queryBuilder}
          field={"entranceStatusTagIds"}
          label={"Entrance Status"}
        />
        <TagFilterFormItem
          tagType={TagType.FieldIndication}
          queryBuilder={queryBuilder}
          field={"entranceFieldIndicationTagIds"}
          label={"Entrance Field Indication"}
        />

        <TagFilterFormItem
          tagType={TagType.LocationQuality}
          queryBuilder={queryBuilder}
          field={"locationQualityTagIds"}
          label={"Entrance Location Quality"}
        />
        <TagFilterFormItem
          tagType={TagType.EntranceHydrology}
          queryBuilder={queryBuilder}
          field={"entranceHydrologyTagIds"}
          label={"Entrance Hydrology"}
        />
        <NumberComparisonFormItem
          inputType={"number"}
          queryBuilder={queryBuilder}
          field={"entrancePitDepthFeet"}
          label={"Entrance Pit Depth (Feet)"}
        />
        <TagFilterFormItem
          tagType={TagType.People}
          queryBuilder={queryBuilder}
          field={"entranceReportedByPeopleTagIds"}
          label={"Entrance Reported By"}
        />
        <NumberComparisonFormItem
          inputType={"date"}
          queryBuilder={queryBuilder}
          field={"entranceReportedOnDate"}
          label={"Entrance Reported On"}
        />

        <Divider>Files</Divider>
        <TagFilterFormItem
          tagType={TagType.File}
          queryBuilder={queryBuilder}
          field={"fileTypeTagIds"}
          label={"File Types"}
        />
        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"fileDisplayName"}
          label={"File Name"}
          queryOperator={QueryOperator.Contains}
        />
      </AdvancedSearchDrawerComponent>

      <SpinnerCardComponent spinning={isCavesLoading}>
        <CardGridComponent
          noDataDescription={"No caves found"}
          noDataCreateButton={<CaveCreateButtonComponent />}
          renderItem={(cave) => (
            // <Link to={`trip/${cave.id}`}>
            <GridCard
              style={{
                height: "100%",
                display: "flex",
                flexDirection: "column",
              }}
              title={`${cave.displayId} ${cave.name} `}
              actions={[
                <Link to={"/caves/" + cave.id}>
                  <PlanarianButton type="primary" icon={<EyeOutlined />}>
                    More Info
                  </PlanarianButton>
                </Link>,
                <>
                  {cave.primaryEntrance && (
                    <>
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
                      </a>
                    </>
                  )}
                </>,
              ]}
            >
              <Typography.Paragraph>
                Length: {convertDistance(cave.lengthFeet)}
              </Typography.Paragraph>
              <Typography.Paragraph>
                Depth: {convertDistance(cave.depthFeet)}
              </Typography.Paragraph>
              {cave.primaryEntrance && (
                <Typography.Paragraph>
                  Elevation:{" "}
                  {convertDistance(cave.primaryEntrance.elevationFeet)}
                </Typography.Paragraph>
              )}
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
            </GridCard>

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
