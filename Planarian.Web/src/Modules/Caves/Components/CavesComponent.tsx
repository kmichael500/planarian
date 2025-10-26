import { Typography, Form, Space, message, Tag, Spin } from "antd";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { PagedResult } from "../../Search/Models/PagedResult";
import DOMPurify from "dompurify";

import { QueryBuilder } from "../../Search/Services/QueryBuilder";
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
import { EyeOutlined, CompassOutlined } from "@ant-design/icons";
import { GridCard } from "../../../Shared/Components/CardGrid/GridCard";
import {
  SelectListItem,
  SelectListItemKey,
} from "../../../Shared/Models/SelectListItem";
import {
  CaveSearchSortByConstants,
  CaveSearchVm,
} from "../Models/CaveSearchVm";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import {
  ShouldDisplay,
  useFeatureEnabled,
} from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import FavoriteCave from "./FavoriteCave";
import { LocationHelpers } from "../../../Shared/Helpers/LocationHelpers";
import { FeatureCheckboxGroup } from "./FeatureCheckboxGroup";
import { CaveAdvancedSearchDrawer } from "./CaveAdvancedSearchDrawer";
import {
  applyEntranceLocationFilterToQuery,
  EntranceLocationFilter,
  parseEntranceLocationFilter,
} from "../../Search/Helpers/EntranceLocationFilterHelpers";
const query = window.location.search.substring(1);
const queryBuilder = new QueryBuilder<CaveSearchParamsVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveSearchVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchParamsVm>();
  let [selectedFeatures, setSelectedFeatures] = useState<
    NestedKeyOf<CaveSearchVm>[]
  >([]);
  const [hasAppliedFilters, setHasAppliedFilters] = useState(
    queryBuilder.hasFilters()
  );
  const [filterClearSignal, setFilterClearSignal] = useState(0);
  const [polygonResetSignal, setPolygonResetSignal] = useState(0);

  const [locationPermissionGranted, setLocationPermissionGranted] = useState<boolean | null>(null);

  const [expandedNarratives, setExpandedNarratives] = useState<
    Record<string, boolean>
  >({});

  const accountId = AuthenticationService.GetAccountId();
  const featureStorageKey = accountId
    ? `${accountId}-selectedFeatures`
    : "selectedFeatures";
  const sortOptions = [
    ...(locationPermissionGranted !== false ? [{ display: "Distance", value: CaveSearchSortByConstants.DistanceMiles }] : []),
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


  const [entranceLocationFilter, setEntranceLocationFilter] =
    useState<EntranceLocationFilter>(() =>
      parseEntranceLocationFilter(
        queryBuilder.getFieldValue("entranceLocation") as string | undefined
      )
    );
  const [isFetchingEntranceLocation, setIsFetchingEntranceLocation] =
    useState(false);


  const handleSortChange = async (sortValue: string) => {
    if (sortValue === CaveSearchSortByConstants.DistanceMiles) {

      if (locationPermissionGranted !== true) {
        const position = await LocationHelpers.getUsersLocation(message);
        if (!position) {
          setLocationPermissionGranted(false);
          return;
        }
        queryBuilder.setUserLocation(position.latitude, position.longitude);
        setLocationPermissionGranted(true);
      }

      queryBuilder.setSort(sortValue);
      await getCaves();
      return;
    }

    queryBuilder.setSort(sortValue);
    await getCaves();
  };
  const applyEntranceLocationFilter = (next: EntranceLocationFilter) => {
    setEntranceLocationFilter(next);
    applyEntranceLocationFilterToQuery(
      queryBuilder,
      "entranceLocation" as NestedKeyOf<CaveSearchParamsVm>,
      next
    );
  };

  const handleEntranceLocationChange = (
    field: keyof EntranceLocationFilter,
    rawValue: number | string | null
  ) => {
    let parsedValue: number | undefined;

    if (rawValue === null || rawValue === undefined || rawValue === "") {
      parsedValue = undefined;
    } else {
      const numericValue = Number(rawValue);
      parsedValue = Number.isFinite(numericValue) ? numericValue : undefined;
    }

    applyEntranceLocationFilter({
      ...entranceLocationFilter,
      [field]: parsedValue,
    });
  };

  const syncEntranceLocationState = () => {
    const currentValue = queryBuilder.getFieldValue(
      "entranceLocation"
    ) as string | undefined;
    setEntranceLocationFilter(parseEntranceLocationFilter(currentValue));
  };

  const handleUseCurrentLocation = () => {
    if (!navigator?.geolocation) {
      message.error("Geolocation is not supported in this browser.");
      return;
    }

    setIsFetchingEntranceLocation(true);

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const { latitude, longitude } = position.coords;
        const radius = entranceLocationFilter.radius ?? 5;

        applyEntranceLocationFilter({
          latitude,
          longitude,
          radius,
        });
        setIsFetchingEntranceLocation(false);
      },
      (error) => {
        message.error(error.message || "Unable to fetch current location.");
        setIsFetchingEntranceLocation(false);
      },
      {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0,
      }
    );
  };

  const toggleNarrative = (caveId: string) => {
    setExpandedNarratives((prev) => ({
      ...prev,
      [caveId]: !prev[caveId],
    }));
  };


  const getCaves = async () => {
    setIsCavesLoading(true);

    if (
      queryBuilder.getSortBy() === CaveSearchSortByConstants.DistanceMiles ||
      (selectedFeatures as string[]).includes("distanceMiles")
    ) {
      if (!queryBuilder.getUserLocation().latitude || !queryBuilder.getUserLocation().longitude) {
        const userLocation = await LocationHelpers.getUsersLocation(message);
        if (userLocation) {
          queryBuilder.setUserLocation(userLocation.latitude, userLocation.longitude);
        }
      }
    }

    const cavesResponse = await CaveService.GetCaves(queryBuilder);
    setCaves(cavesResponse);
    setHasAppliedFilters(queryBuilder.hasFilters());
    setIsCavesLoading(false);
  };

  const onSearch = async () => {
    syncEntranceLocationState();
    await getCaves();
  };

  const possibleFeaturesToRender: SelectListItemKey<CaveSearchVm>[] = [
    {
      display: "ID",
      value: "displayId",
      data: { key: FeatureKey.EnabledFieldCaveId },
    },
    {
      display: "Distance",
      value: "distanceMiles",
      data: { key: FeatureKey.EnabledFieldCaveDistance },
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
    const filterFeatures = async () => {
      const savedFeaturesJson = featureStorageKey
        ? localStorage.getItem(featureStorageKey)
        : null;
      let savedFeatures: NestedKeyOf<CaveSearchVm>[] = [];

      if (savedFeaturesJson !== null) {
        try {
          savedFeatures = JSON.parse(savedFeaturesJson);
        } catch {
          savedFeatures = [];
        }
      } else {
        savedFeatures = ["countyId", "lengthFeet", "depthFeet", "reportedOn"];
      }

      const enabledFeatures: SelectListItemKey<CaveSearchVm>[] = [];
      for (const feature of possibleFeaturesToRender) {
        const isEnabled = isFeatureEnabled(feature.data.key);
        if (isEnabled) {
          enabledFeatures.push(feature);
        }
      }

      // Filter defaults against enabled features
      let filteredSelectedFeatures = savedFeatures.filter((featureKey) =>
        enabledFeatures.some((enabled) => enabled.value === featureKey)
      );

      // Location check for initial setup if distanceMiles is enabled and selected
      if (filteredSelectedFeatures.includes("distanceMiles")) {
        const userLocation = await LocationHelpers.getUsersLocation(message);
        if (userLocation) {
          queryBuilder.setUserLocation(
            userLocation.latitude,
            userLocation.longitude
          );
        } else {
          // Remove distanceMiles from selection if permission denied
          filteredSelectedFeatures = filteredSelectedFeatures.filter(
            (value) => value !== "distanceMiles"
          );
        }
      }

      setFilteredFeatures(enabledFeatures);
      setSelectedFeatures(filteredSelectedFeatures);

      if (featureStorageKey) {
        localStorage.setItem(
          featureStorageKey,
          JSON.stringify(filteredSelectedFeatures)
        );
      }

      await getCaves();
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

      case nameof<CaveSearchVm>("distanceMiles"):
        if (cave.distanceMiles) {
          return defaultIfEmpty(formatDistance(cave.distanceMiles * 5280));
        }
        return defaultIfEmpty(null);
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

  const displayFeatureOptions = filteredFeatures.map((feature) => ({
    label: feature.display,
    value: feature.value,
  }));

  return (
    <>
      <CaveAdvancedSearchDrawer
        onSearch={onSearch}
        queryBuilder={queryBuilder}
        form={form}
        sortOptions={sortOptions}
        onSortChange={handleSortChange}
        onFiltersCleared={() => {
          applyEntranceLocationFilter({});
          setFilterClearSignal((previous) => previous + 1);
          setPolygonResetSignal((previous) => previous + 1);
        }}
        entranceLocationFilter={entranceLocationFilter}
        isFetchingEntranceLocation={isFetchingEntranceLocation}
        onEntranceLocationChange={handleEntranceLocationChange}
        onUseCurrentLocation={handleUseCurrentLocation}
        onClearEntranceLocation={() => applyEntranceLocationFilter({})}
        polygonResetSignal={polygonResetSignal}
        filterClearSignal={filterClearSignal}
      />

      <Space direction="vertical" style={{ width: "100%" }}>
        <FeatureCheckboxGroup
          title="Display"
          options={displayFeatureOptions}
          value={selectedFeatures}
          onChange={async (checkedValues) => {
            const previousFeatures = selectedFeatures;
            const isDistanceBeingChecked = checkedValues.includes(
              "distanceMiles" as NestedKeyOf<CaveSearchVm>
            ) && !previousFeatures.includes("distanceMiles");

            if (isDistanceBeingChecked) {
              const userLocation = await LocationHelpers.getUsersLocation(message);
              if (userLocation) {
                queryBuilder.setUserLocation(
                  userLocation.latitude,
                  userLocation.longitude
                );
                setSelectedFeatures(checkedValues);
                if (featureStorageKey) {
                  localStorage.setItem(
                    featureStorageKey,
                    JSON.stringify(checkedValues)
                  );
                }
                await getCaves();
              } else {
                // Remove distanceMiles from selection if permission denied
                const updatedValues = checkedValues.filter(
                  (value) => value !== "distanceMiles"
                );
                setSelectedFeatures(updatedValues);
                if (featureStorageKey) {
                  localStorage.setItem(
                    featureStorageKey,
                    JSON.stringify(updatedValues)
                  );
                }
              }
            } else {
              queryBuilder.setUserLocation(undefined, undefined);
              queryBuilder.buildAsQueryString(); // Clear out user location from URL

              setSelectedFeatures(checkedValues);
              if (featureStorageKey) {
                localStorage.setItem(
                  featureStorageKey,
                  JSON.stringify(checkedValues)
                );
              }
            }
          }}
        />
        {isCavesLoading ? (
          <Space align="center">
            <Spin size="small" />
            <Typography.Text type="secondary">
              Loading results...
            </Typography.Text>
          </Space>
        ) : (
          caves && (
            <>
              {hasAppliedFilters ? (
                <Tag color="#F8DB6A" style={{ color: "black" }}>
                  <Typography.Text style={{ color: "black" }}>
                    Filtered: {formatNumber(caves.totalCount)} results found
                  </Typography.Text>
                </Tag>
              ) : (
                <Tag>
                  <Typography.Text>
                    {formatNumber(caves.totalCount)} results found
                  </Typography.Text>
                </Tag>
              )}
            </>
          )
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
                title={
                  <span>
                    <span style={{ color: '#3874f6', fontWeight: "bold" }}>{cave.displayId}</span>
                    {" "}{cave.name}
                  </span>}
                extra={
                  <FavoriteCave
                    initialIsFavorite={cave.isFavorite}
                    caveId={cave.id}
                    onlyShowWhenFavorite
                    disabled
                  />
                }
                actions={[
                  <Link to={`/caves/${cave.id}`} key="view">
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
                      key="map"
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
                  {cave.narrativeSnippet && (
                    <>
                      <Typography.Text
                        style={{ marginRight: "8px", fontWeight: "bold" }}
                      >
                        Narrative:
                      </Typography.Text>
                      <Typography.Paragraph style={{ marginBottom: 8 }}>
                        <span
                          dangerouslySetInnerHTML={{
                            __html: DOMPurify.sanitize(
                              expandedNarratives[cave.id] ||
                                cave.narrativeSnippet.length <= 400
                                ? cave.narrativeSnippet
                                : cave.narrativeSnippet.substring(0, 400) + "…",
                              { ALLOWED_TAGS: ["mark", "br"] }
                            ),
                          }}
                        />
                      </Typography.Paragraph>
                      {cave.narrativeSnippet.length > 400 && (
                        <div style={{ marginBottom: 8 }}>
                          <PlanarianButton
                            alwaysShowChildren
                            type="link"
                            size="small"
                            onClick={() => toggleNarrative(cave.id)}
                            icon={undefined}
                          >
                            {expandedNarratives[cave.id]
                              ? "Show Less"
                              : "Show More"}
                          </PlanarianButton>
                        </div>
                      )}
                    </>
                  )}
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
