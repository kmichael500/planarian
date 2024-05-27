import { Row, Col, Typography, Form, Checkbox, Space, Divider } from "antd";
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
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { CaveCreateButtonComponent } from "./CaveCreateButtonComponent";
import {
  NestedKeyOf,
  nameof,
  convertDistance,
  getDirectionsUrl,
  formatDateTime,
  defaultIfEmpty,
} from "../../../Shared/Helpers/StringHelpers";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EyeOutlined, CarOutlined } from "@ant-design/icons";
import { GridCard } from "../../../Shared/Components/CardGrid/GridCard";
import { SelectListItemKey } from "../../../Shared/Models/SelectListItem";
import { CaveSearchVm } from "../Models/CaveSearchVm";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { StateCountyFilterFormItem } from "../../Search/Components/StateFilterFormItem";
import { TagFilterFormItem } from "../../Search/Components/TagFilterFormItem";
import { TagType } from "../../Tag/Models/TagType";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { TextFilterFormItem } from "../../Search/Components/TextFilterFormItem";

const query = window.location.search.substring(1);
const queryBuilder = new QueryBuilder<CaveSearchParamsVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveSearchVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchParamsVm>();
  let [selectedFeatures, setSelectedFeatures] = useState<
    NestedKeyOf<CaveSearchVm>[]
  >([]);

  useEffect(() => {
    const savedFeatures = localStorage.getItem("selectedFeatures");
    if (savedFeatures) {
      setSelectedFeatures(JSON.parse(savedFeatures));
    } else {
      setSelectedFeatures(["displayId", "countyId", "reportedOn"]); // Default values
    }
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

  const possibleFeaturesToRender: SelectListItemKey<CaveSearchVm>[] = [
    { display: "ID", value: "displayId" },
    { display: "County", value: "countyId" },
    { display: "Reported On", value: "reportedOn" },
    { display: "Length", value: "lengthFeet" },
    { display: "Depth", value: "depthFeet" },
    { display: "Max Pit Depth", value: "maxPitDepthFeet" },
    { display: "Number of Pits", value: "numberOfPits" },
    { display: "Map Status", value: "mapStatusTagIds" },
    { display: "Geology", value: "geologyTagIds" },
    { display: "Geologic Age", value: "geologicAgeTagIds" },
    { display: "Archaeology", value: "archaeologyTagIds" },
    { display: "Biology", value: "biologyTagIds" },
    { display: "Cartographers", value: "cartographerNameTagIds" },
    {
      display: "Physiographic Province",
      value: "physiographicProvinceTagIds",
    },
    { display: "Reported By", value: "reportedByTagIds" },
    { display: "Other Tags", value: "otherTagIds" },
  ];

  const renderFeature = (
    cave: CaveSearchVm,
    featureKey: NestedKeyOf<CaveSearchVm>
  ) => {
    switch (featureKey) {
      case nameof<CaveSearchVm>("name"):
        return defaultIfEmpty(cave.name);
      case nameof<CaveSearchVm>("reportedOn"):
        return defaultIfEmpty(formatDateTime(cave.reportedOn));
      case nameof<CaveSearchVm>("isArchived"):
        return cave.isArchived;
      case nameof<CaveSearchVm>("depthFeet"):
        return defaultIfEmpty(convertDistance(cave.depthFeet));
      case nameof<CaveSearchVm>("lengthFeet"):
        return defaultIfEmpty(convertDistance(cave.lengthFeet));
      case nameof<CaveSearchVm>("maxPitDepthFeet"):
        return defaultIfEmpty(convertDistance(cave.maxPitDepthFeet));
      case nameof<CaveSearchVm>("numberOfPits"):
        return defaultIfEmpty(cave.numberOfPits?.toString());
      case nameof<CaveSearchVm>("countyId"):
        return <CountyTagComponent countyId={cave.countyId} />;
      case nameof<CaveSearchVm>("displayId"):
        return cave.displayId;
      case nameof<CaveSearchVm>("archaeologyTagIds"):
      case nameof<CaveSearchVm>("biologyTagIds"):
      case nameof<CaveSearchVm>("cartographerNameTagIds"):
      case nameof<CaveSearchVm>("geologicAgeTagIds"):
      case nameof<CaveSearchVm>("geologyTagIds"):
      case nameof<CaveSearchVm>("mapStatusTagIds"):
      case nameof<CaveSearchVm>("otherTagIds"):
      case nameof<CaveSearchVm>("physiographicProvinceTagIds"):
      case nameof<CaveSearchVm>("reportedByTagIds"):
        if (
          (cave[featureKey as keyof CaveSearchVm] as string[])?.length === 0
        ) {
          return defaultIfEmpty(null);
        }
        return (
          <div style={{ display: "flex", flexWrap: "wrap" }}>
            {(cave[featureKey as keyof CaveSearchVm] as string[])?.map(
              (tagId: string) => (
                <TagComponent key={tagId} tagId={tagId} />
              )
            )}
          </div>
        );
      default:
        return null;
    }
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
          field={"mapStatusTagIds"}
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
        />{" "}
      </AdvancedSearchDrawerComponent>

      <Space direction="vertical">
        <div
          style={{
            borderRadius: "2px",
            padding: "10px",
            border: "1px solid #d9d9d9",
            backgroundColor: "white",
          }}
        >
          <div style={{ fontWeight: 450 }}>Display</div>
          <Checkbox.Group
            options={possibleFeaturesToRender.map((feature) => ({
              label: feature.display,
              value: feature.value,
            }))}
            value={selectedFeatures}
            onChange={(checkedValues) => {
              setSelectedFeatures(checkedValues as NestedKeyOf<CaveSearchVm>[]);
              localStorage.setItem(
                "selectedFeatures",
                JSON.stringify(checkedValues)
              );
            }}
          />
        </div>

        <SpinnerCardComponent spinning={isCavesLoading}>
          <CardGridComponent
            noDataDescription={"No caves found"}
            noDataCreateButton={<CaveCreateButtonComponent />}
            renderItem={(cave) => (
              <GridCard
                style={{
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                }}
                title={`${cave.displayId} ${cave.name}`}
                actions={[
                  <Link to={`/caves/${cave.id}`}>
                    <PlanarianButton type="primary" icon={<EyeOutlined />}>
                      More Info
                    </PlanarianButton>
                  </Link>,
                  cave.primaryEntranceLatitude &&
                    cave.primaryEntranceLongitude && (
                      <a
                        href={getDirectionsUrl(
                          cave.primaryEntranceLatitude,
                          cave.primaryEntranceLongitude
                        )}
                        target="_blank"
                      >
                        <PlanarianButton icon={<CarOutlined />}>
                          Directions
                        </PlanarianButton>
                      </a>
                    ),
                ]}
              >
                <Space direction="vertical">
                  {selectedFeatures.map((featureKey) => (
                    <div
                      key={featureKey}
                      style={{
                        display: "flex",
                        flexWrap: "wrap",
                        alignItems: "center",
                      }}
                    >
                      <Typography.Text
                        style={{ marginRight: "8px", fontWeight: "bold" }}
                      >
                        {
                          possibleFeaturesToRender.find(
                            (f) => f.value === featureKey
                          )?.display
                        }
                        :
                      </Typography.Text>
                      {renderFeature(cave, featureKey)}
                    </div>
                  ))}
                </Space>
              </GridCard>
            )}
            itemKey={(cave) => cave.id}
            pagedItems={caves}
            queryBuilder={queryBuilder}
            onSearch={onSearch}
          />
        </SpinnerCardComponent>
      </Space>
    </>
  );
};

export { CavesComponent };
