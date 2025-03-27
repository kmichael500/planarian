import { Typography, Form, Checkbox, Space, Divider, message } from "antd";
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
  formatDistance,
  formatDate,
  defaultIfEmpty,
  formatNumber,
} from "../../../Shared/Helpers/StringHelpers";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  EyeOutlined,
  CompassOutlined,
  DownloadOutlined,
} from "@ant-design/icons";
import { GridCard } from "../../../Shared/Components/CardGrid/GridCard";
import {
  SelectListItem,
  SelectListItemKey,
} from "../../../Shared/Models/SelectListItem";
import {
  CaveSearchSortByConstants,
  CaveSearchVm,
} from "../Models/CaveSearchVm";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { StateCountyFilterFormItem } from "../../Search/Components/StateFilterFormItem";
import { TagFilterFormItem } from "../../Search/Components/TagFilterFormItem";
import { TagType } from "../../Tag/Models/TagType";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { TextFilterFormItem } from "../../Search/Components/TextFilterFormItem";
import {
  ShouldDisplay,
  useFeatureEnabled,
} from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { saveAs } from "file-saver";
import { AccountService } from "../../Account/Services/AccountService";
import { AppService } from "../../../Shared/Services/AppService";

const query = window.location.search.substring(1);
const queryBuilder = new QueryBuilder<CaveSearchParamsVm>(query);
const sortOptions = [
  { display: "Length", value: CaveSearchSortByConstants.LengthFeet },
  { display: "Depth", value: CaveSearchSortByConstants.DepthFeet },
  {
    display: "Max Pit Depth",
    value: CaveSearchSortByConstants.MaxPitDepthFeet,
  },
  { display: "Number of Pits", value: CaveSearchSortByConstants.NumberOfPits },
  { display: "Reported On", value: CaveSearchSortByConstants.ReportedOn },
  { display: "Name", value: CaveSearchSortByConstants.Name },
] as SelectListItem<string>[];

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveSearchVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchParamsVm>();
  let [selectedFeatures, setSelectedFeatures] = useState<
    NestedKeyOf<CaveSearchVm>[]
  >([]);

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

  const onExportGpx = async () => {
    const hide = message.loading("Exporting GPX...", 0);
    try {
      const response = await CaveService.ExportCavesGpx(queryBuilder);

      const accountName = AuthenticationService.GetAccountName();
      const localDateTime = new Date().toISOString();

      const fileName = `${accountName} ${localDateTime}.gpx`;

      saveAs(response, fileName);
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      hide(); // Remove the loading message
    }
  };

  const possibleFeaturesToRender: SelectListItemKey<CaveSearchVm>[] = [
    {
      display: "ID",
      value: "displayId",
      data: { key: FeatureKey.EnabledFieldCaveId },
    },
    {
      display: "County",
      value: "countyId",
      data: { key: FeatureKey.EnabledFieldCaveCounty },
    },
    {
      display: "Length",
      value: "lengthFeet",
      data: { key: FeatureKey.EnabledFieldCaveLengthFeet },
    },
    {
      display: "Depth",
      value: "depthFeet",
      data: { key: FeatureKey.EnabledFieldCaveDepthFeet },
    },
    {
      display: "Reported On",
      value: "reportedOn",
      data: { key: FeatureKey.EnabledFieldCaveReportedOn },
    },
    {
      display: "Max Pit Depth",
      value: "maxPitDepthFeet",
      data: { key: FeatureKey.EnabledFieldCaveMaxPitDepthFeet },
    },
    {
      display: "Number of Pits",
      value: "numberOfPits",
      data: { key: FeatureKey.EnabledFieldCaveNumberOfPits },
    },
    {
      display: "Map Status",
      value: "mapStatusTagIds",
      data: { key: FeatureKey.EnabledFieldCaveMapStatusTags },
    },
    {
      display: "Geology",
      value: "geologyTagIds",
      data: { key: FeatureKey.EnabledFieldCaveGeologyTags },
    },
    {
      display: "Geologic Age",
      value: "geologicAgeTagIds",
      data: { key: FeatureKey.EnabledFieldCaveGeologicAgeTags },
    },
    {
      display: "Archaeology",
      value: "archaeologyTagIds",
      data: { key: FeatureKey.EnabledFieldCaveArcheologyTags },
    },
    {
      display: "Biology",
      value: "biologyTagIds",
      data: { key: FeatureKey.EnabledFieldCaveBiologyTags },
    },
    {
      display: "Cartographers",
      value: "cartographerNameTagIds",
      data: { key: FeatureKey.EnabledFieldCaveCartographerNameTags },
    },
    {
      display: "Physiographic Province",
      value: "physiographicProvinceTagIds",
      data: { key: FeatureKey.EnabledFieldCavePhysiographicProvinceTags },
    },
    {
      display: "Reported By",
      value: "reportedByTagIds",
      data: { key: FeatureKey.EnabledFieldCaveReportedByNameTags },
    },
    {
      display: "Other Tags",
      value: "otherTagIds",
      data: { key: FeatureKey.EnabledFieldCaveOtherTags },
    },
  ];
  const { isFeatureEnabled } = useFeatureEnabled();
  const [filteredFeatures, setFilteredFeatures] = useState<
    SelectListItemKey<CaveSearchVm>[]
  >([]);

  useEffect(() => {
    const filterFeatures = () => {
      const savedFeaturesJson = localStorage.getItem(
        `${AuthenticationService.GetAccountId()}-selectedFeatures`
      );
      let savedFeatures: NestedKeyOf<CaveSearchVm>[] = [];
      if (savedFeaturesJson) {
        savedFeatures = JSON.parse(savedFeaturesJson);
      } else {
        savedFeatures = ["countyId", "lengthFeet", "depthFeet", "reportedOn"];
      }

      const enabledFeatures = possibleFeaturesToRender.filter((feature) => {
        const isEnabled = isFeatureEnabled(feature.data.key);

        return isEnabled;
      });

      setFilteredFeatures(enabledFeatures);
      setSelectedFeatures(
        savedFeatures.filter((feature) =>
          enabledFeatures.map((f) => f.value).includes(feature)
        )
      );
    };

    filterFeatures();
  }, []);

  const renderFeature = (
    cave: CaveSearchVm,
    featureKey: NestedKeyOf<CaveSearchVm>
  ) => {
    switch (featureKey) {
      case nameof<CaveSearchVm>("name"):
        return defaultIfEmpty(cave.name);
      case nameof<CaveSearchVm>("reportedOn"):
        return defaultIfEmpty(formatDate(cave.reportedOn));
      case nameof<CaveSearchVm>("isArchived"):
        return cave.isArchived;
      case nameof<CaveSearchVm>("depthFeet"):
        return defaultIfEmpty(formatDistance(cave.depthFeet));
      case nameof<CaveSearchVm>("lengthFeet"):
        return defaultIfEmpty(formatDistance(cave.lengthFeet));
      case nameof<CaveSearchVm>("maxPitDepthFeet"):
        return defaultIfEmpty(formatDistance(cave.maxPitDepthFeet));
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
            {" "}
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
        sortOptions={sortOptions}
        onExportGpx={onExportGpx}
      >
        <Divider>Cave</Divider>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveNarrative}>
          <TextFilterFormItem
            queryBuilder={queryBuilder}
            field={"narrative"}
            label={"Narrative"}
            queryOperator={QueryOperator.FreeText}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveState}>
          <StateCountyFilterFormItem
            queryBuilder={queryBuilder}
            autoSelectFirst={true}
            stateField={"stateId"}
            stateLabel={"State"}
            countyField={"countyId"}
            countyLabel={"County"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveLengthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"lengthFeet"}
            label={"Length (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveDepthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"depthFeet"}
            label={"Depth (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveNumberOfPits}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"numberOfPits"}
            label={"Number of Pits (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMaxPitDepthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"maxPitDepthFeet"}
            label={"Max Pit Depth (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMapStatusTags}>
          <TagFilterFormItem
            tagType={TagType.MapStatus}
            queryBuilder={queryBuilder}
            field={"mapStatusTagIds"}
            label={"Map Status"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldCaveCartographerNameTags}
        >
          <TagFilterFormItem
            tagType={TagType.People}
            queryBuilder={queryBuilder}
            field={"cartographerNamePeopleTagIds"}
            label={"Cartographer Names"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveGeologyTags}>
          <TagFilterFormItem
            tagType={TagType.Geology}
            queryBuilder={queryBuilder}
            field={"geologyTagIds"}
            label={"Geology"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveGeologicAgeTags}>
          <TagFilterFormItem
            tagType={TagType.GeologicAge}
            queryBuilder={queryBuilder}
            field={"geologicAgeTagIds"}
            label={"Geologic Age"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldCavePhysiographicProvinceTags}
        >
          <TagFilterFormItem
            tagType={TagType.PhysiographicProvince}
            queryBuilder={queryBuilder}
            field={"physiographicProvinceTagIds"}
            label={"Physiographic Province"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveBiologyTags}>
          <TagFilterFormItem
            tagType={TagType.Biology}
            queryBuilder={queryBuilder}
            field={"biologyTagIds"}
            label={"Biology"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveArcheologyTags}>
          <TagFilterFormItem
            tagType={TagType.Archeology}
            queryBuilder={queryBuilder}
            field={"archaeologyTagIds" as any}
            label={"Archeology"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveOtherTags}>
          <TagFilterFormItem
            tagType={TagType.CaveOther}
            queryBuilder={queryBuilder}
            field={"caveOtherTagIds"}
            label={"Other"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldCaveReportedByNameTags}
        >
          <TagFilterFormItem
            tagType={TagType.People}
            queryBuilder={queryBuilder}
            field={"caveReportedByNameTagIds"}
            label={"Reported By"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveReportedOn}>
          <NumberComparisonFormItem
            inputType={"date"}
            queryBuilder={queryBuilder}
            field={"caveReportedOnDate"}
            label={"Reported On"}
          />
        </ShouldDisplay>
        <Divider>Entrance</Divider>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceElevation}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"elevationFeet"}
            label={"Elevation (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceDescription}>
          <TextFilterFormItem
            queryBuilder={queryBuilder}
            field={"entranceDescription"}
            label={"Entrance Description"}
            queryOperator={QueryOperator.FreeText}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceStatusTags}>
          <TagFilterFormItem
            tagType={TagType.EntranceStatus}
            queryBuilder={queryBuilder}
            field={"entranceStatusTagIds"}
            label={"Entrance Status"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldEntranceFieldIndicationTags}
        >
          <TagFilterFormItem
            tagType={TagType.FieldIndication}
            queryBuilder={queryBuilder}
            field={"entranceFieldIndicationTagIds"}
            label={"Entrance Field Indication"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldEntranceLocationQuality}
        >
          <TagFilterFormItem
            tagType={TagType.LocationQuality}
            queryBuilder={queryBuilder}
            field={"locationQualityTagIds"}
            label={"Entrance Location Quality"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldEntranceHydrologyTags}
        >
          <TagFilterFormItem
            tagType={TagType.EntranceHydrology}
            queryBuilder={queryBuilder}
            field={"entranceHydrologyTagIds"}
            label={"Entrance Hydrology"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMaxPitDepthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"entrancePitDepthFeet"}
            label={"Entrance Pit Depth (Feet)"}
          />
        </ShouldDisplay>
        <ShouldDisplay
          featureKey={FeatureKey.EnabledFieldEntranceReportedByNameTags}
        >
          <TagFilterFormItem
            tagType={TagType.People}
            queryBuilder={queryBuilder}
            field={"entranceReportedByPeopleTagIds"}
            label={"Entrance Reported By"}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceReportedOn}>
          <NumberComparisonFormItem
            inputType={"date"}
            queryBuilder={queryBuilder}
            field={"entranceReportedOnDate"}
            label={"Entrance Reported On"}
          />
        </ShouldDisplay>
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

      <Space direction="vertical" style={{ width: "100%" }}>
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
            options={filteredFeatures.map((feature) => ({
              label: feature.display,
              value: feature.value,
            }))}
            value={selectedFeatures}
            onChange={(checkedValues) => {
              setSelectedFeatures(checkedValues as NestedKeyOf<CaveSearchVm>[]);
              localStorage.setItem(
                `${AuthenticationService.GetAccountId()}-selectedFeatures`,
                JSON.stringify(checkedValues)
              );
            }}
          />
        </div>
        {caves && (
          <Typography.Text>
            {formatNumber(caves.totalCount)} results found
          </Typography.Text>
        )}
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
                    <PlanarianButton
                      alwaysShowChildren
                      type="primary"
                      icon={<EyeOutlined />}
                    >
                      View
                    </PlanarianButton>
                  </Link>,
                  cave.primaryEntranceLatitude &&
                    cave.primaryEntranceLongitude && (
                      <Link
                        to={NavigationService.GenerateMapUrl(
                          cave.primaryEntranceLatitude,
                          cave.primaryEntranceLongitude,
                          15
                        )}
                      >
                        <PlanarianButton
                          alwaysShowChildren
                          icon={<CompassOutlined />}
                        >
                          Map
                        </PlanarianButton>
                      </Link>
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
