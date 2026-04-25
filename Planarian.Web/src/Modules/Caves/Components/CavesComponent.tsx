import { Typography, Form, Space, message } from "antd";
import { useState, useEffect } from "react";
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
import {
  GridCard,
  GridCardAction,
} from "../../../Shared/Components/CardGrid/GridCard";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import {
  CaveSearchSortByConstants,
  CaveSearchVm,
} from "../Models/CaveSearchVm";
import { CountyTagComponent } from "../../../Shared/Components/Display/CountyTagComponent";
import { useFeatureEnabled } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { NavigationService } from "../../../Shared/Services/NavigationService";
import FavoriteCave from "./FavoriteCave";
import { LocationHelpers } from "../../../Shared/Helpers/LocationHelpers";
import { FeatureCheckboxGroup } from "./FeatureCheckboxGroup";
import { CaveAdvancedSearchDrawer } from "./CaveAdvancedSearchDrawer";
import { SplitSortControl } from "../../Search/Components/SplitSortControl";
import { ScrollCollapseSection } from "../../../Shared/Components/ScrollCollapseSection/ScrollCollapseSection";
import {
  applyEntranceLocationFilterToQuery,
  EntranceLocationFilter,
  parseEntranceLocationFilter,
} from "../../Search/Helpers/EntranceLocationFilterHelpers";
import { ToolbarMetric } from "../../../Shared/Components/Toolbar/ResponsiveToolbar";
import {
  CAVE_SEARCH_DISPLAY_FEATURE_LABELS,
  CAVE_SEARCH_DISPLAY_FEATURES,
  CaveSearchDisplayFeature,
  DEFAULT_CAVE_SEARCH_DISPLAY_FEATURES,
  isCaveSearchTagDisplayFeature,
  migrateCaveSearchDisplayFeatureKeys,
} from "./CaveSearchDisplayFields";
import { DistanceFromMeComponent } from "../../../Shared/Components/Display/DistanceFromMeComponent";
import "./CavesComponent.scss";
const query = window.location.search.substring(1);
const queryBuilder = new QueryBuilder<CaveSearchParamsVm>(query);
const SEARCH_TOOLBAR_BREAKPOINT_PX = 720;

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
  const [isDisplayCollapsed, setIsDisplayCollapsed] = useState(false);
  const [hasAutoCollapsedDisplay, setHasAutoCollapsedDisplay] = useState(false);
  const [isResultsScrolled, setIsResultsScrolled] = useState(false);
  const [isBelowToolbarBreakpoint, setIsBelowToolbarBreakpoint] = useState(
    window.innerWidth < SEARCH_TOOLBAR_BREAKPOINT_PX
  );

  const accountId = AuthenticationService.GetAccountId();
  const featureStorageKey = accountId
    ? `${accountId}-selectedFeatures`
    : "selectedFeatures";
  const sortOptions = [
    ...(locationPermissionGranted !== false ? [{ display: "Distance From Me", value: CaveSearchSortByConstants.DistanceMiles }] : []),
    { display: "Cave ID", value: CaveSearchSortByConstants.DisplayId },
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

  useEffect(() => {
    const updateBreakpoint = () => {
      setIsBelowToolbarBreakpoint(
        window.innerWidth < SEARCH_TOOLBAR_BREAKPOINT_PX
      );
    };

    window.addEventListener("resize", updateBreakpoint);
    return () => window.removeEventListener("resize", updateBreakpoint);
  }, []);


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

  const persistSelectedFeatures = (
    features: NestedKeyOf<CaveSearchVm>[]
  ) => {
    if (featureStorageKey) {
      localStorage.setItem(featureStorageKey, JSON.stringify(features));
    }
  };

  const handleDisplayFeaturesChange = async (
    checkedValues: NestedKeyOf<CaveSearchVm>[]
  ) => {
    const isDistanceBeingChecked =
      checkedValues.includes("distanceMiles") &&
      !selectedFeatures.includes("distanceMiles");

    if (isDistanceBeingChecked) {
      const userLocation = await LocationHelpers.getUsersLocation(message);
      if (userLocation) {
        queryBuilder.setUserLocation(
          userLocation.latitude,
          userLocation.longitude
        );
        setSelectedFeatures(checkedValues);
        persistSelectedFeatures(checkedValues);
        await getCaves();
      } else {
        const updatedValues = checkedValues.filter(
          (value) => value !== "distanceMiles"
        );
        setSelectedFeatures(updatedValues);
        persistSelectedFeatures(updatedValues);
      }

      return;
    }

    queryBuilder.setUserLocation(undefined, undefined);
    queryBuilder.buildAsQueryString();
    setSelectedFeatures(checkedValues);
    persistSelectedFeatures(checkedValues);
  };

  const { isFeatureEnabled } = useFeatureEnabled();
  const [filteredFeatures, setFilteredFeatures] = useState<
    CaveSearchDisplayFeature[]
  >([]);

  useEffect(() => {
    const filterFeatures = async () => {
      const savedFeaturesJson = featureStorageKey
        ? localStorage.getItem(featureStorageKey)
        : null;
      let savedFeatures: NestedKeyOf<CaveSearchVm>[] = [];

      if (savedFeaturesJson !== null) {
        try {
          const parsedFeatures = JSON.parse(savedFeaturesJson);
          savedFeatures = Array.isArray(parsedFeatures)
            ? migrateCaveSearchDisplayFeatureKeys(parsedFeatures)
            : [];
        } catch {
          savedFeatures = [];
        }
      } else {
        savedFeatures = DEFAULT_CAVE_SEARCH_DISPLAY_FEATURES;
      }

      const enabledFeatures: CaveSearchDisplayFeature[] = [];
      for (const feature of CAVE_SEARCH_DISPLAY_FEATURES) {
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
      persistSelectedFeatures(filteredSelectedFeatures);

      await getCaves();
    };

    filterFeatures();
  }, []);

  const renderFeature = (
    cave: CaveSearchVm,
    featureKey: NestedKeyOf<CaveSearchVm>
  ) => {
    if (isCaveSearchTagDisplayFeature(featureKey)) {
      const tags = cave[featureKey as keyof CaveSearchVm] as
        | SelectListItem<string>[]
        | undefined;

      if (!tags || tags.length === 0) {
        return defaultIfEmpty(null);
      }

      return (
        <Space size={[3, 3]} className="caves-result-card__tags" wrap>
          {tags.map((tag) => (
            <TagComponent key={tag.value} item={tag} />
          ))}
        </Space>
      );
    }

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
        return (
          <DistanceFromMeComponent
            latitude={cave.primaryEntranceLatitude}
            longitude={cave.primaryEntranceLongitude}
          />
        );
      case nameof<CaveSearchVm>("county"):
        return <CountyTagComponent item={cave.county} />;
      case nameof<CaveSearchVm>("displayId"):
        return cave.displayId;
      default:
        return null;
    }
  };

  const displayFeatureOptions = filteredFeatures.map((feature) => ({
    label: feature.display,
    value: feature.value,
  }));
  const toolbarMetrics: ToolbarMetric[] = [
    {
      key: "results",
      className: "import-files-dashboard__toolbar-metric",
      label: hasAppliedFilters ? "Filtered" : "Results",
      value: isCavesLoading ? "..." : formatNumber(caves?.totalCount ?? 0),
    },
  ];
  const isMobile = isBelowToolbarBreakpoint;
  const showMobileRows = !isResultsScrolled;
  const mobileResultsMetric = toolbarMetrics[0];

  return (
    <div className="caves-component">
      <CaveAdvancedSearchDrawer
        onSearch={onSearch}
        queryBuilder={queryBuilder}
        form={form}
        sortOptions={sortOptions}
        onSortChange={handleSortChange}
        toolbarMetrics={isMobile ? [] : toolbarMetrics}
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

      <div className="caves-component__results">
        {isMobile ? (
          <ScrollCollapseSection visible={showMobileRows}>
            <div className="caves-mobile-toolbar-section__inner">
              <FeatureCheckboxGroup
                collapsible
                collapsed={isDisplayCollapsed}
                onCollapsedChange={(collapsed) => {
                  setIsDisplayCollapsed(collapsed);
                  if (!collapsed) {
                    setHasAutoCollapsedDisplay(true);
                  }
                }}
                title="Display Attributes"
                options={displayFeatureOptions}
                value={selectedFeatures}
                onChange={handleDisplayFeaturesChange}
              />
              <div className="caves-mobile-toolbar-row">
                <div className="caves-mobile-toolbar-row__sort">
                  <SplitSortControl
                    compact
                    isDescending={queryBuilder.getSortDescending() ?? false}
                    onSelect={handleSortChange}
                    onToggleDirection={async () => {
                      queryBuilder.setSortDescending(!queryBuilder.getSortDescending());
                      await getCaves();
                    }}
                    selectedValue={queryBuilder.getSortBy()}
                    sortOptions={sortOptions}
                  />
                </div>
                {mobileResultsMetric ? (
                  <div
                    className={[
                      "caves-mobile-toolbar-row__metric",
                      "planarian-search-toolbar__metric",
                      mobileResultsMetric.className,
                    ]
                      .filter(Boolean)
                      .join(" ")}
                  >
                    <span className="planarian-search-toolbar__metric-label">
                      {mobileResultsMetric.label}
                    </span>
                    <span className="planarian-search-toolbar__metric-value">
                      {mobileResultsMetric.value}
                    </span>
                  </div>
                ) : null}
              </div>
            </div>
          </ScrollCollapseSection>
        ) : (
          <FeatureCheckboxGroup
            collapsible
            collapsed={isDisplayCollapsed}
            onCollapsedChange={(collapsed) => {
              setIsDisplayCollapsed(collapsed);
              if (!collapsed) {
                setHasAutoCollapsedDisplay(true);
              }
            }}
            title="Display Attributes"
            options={displayFeatureOptions}
            value={selectedFeatures}
            onChange={handleDisplayFeaturesChange}
          />
        )}
        <SpinnerCardComponent spinning={isCavesLoading}>
          <CardGridComponent
            fillHeight
            noDataDescription={"No caves found"}
            noDataCreateButton={<CaveCreateButtonComponent />}
            renderItem={(cave) => {

              const distanceMilesKey = nameof<CaveSearchVm>("distanceMiles") as NestedKeyOf<CaveSearchVm>;
              const shouldShowDistanceFromMe =
                selectedFeatures.includes(distanceMilesKey) &&
                cave.primaryEntranceLatitude !== null &&
                cave.primaryEntranceLatitude !== undefined &&
                cave.primaryEntranceLongitude !== null &&
                cave.primaryEntranceLongitude !== undefined;

              const actions: GridCardAction[] = [
                {
                  key: "view",
                  label: "View",
                  icon: <EyeOutlined />,
                  to: `/caves/${cave.id}`,
                  type: "primary",
                },
              ];

              if (cave.primaryEntranceLatitude && cave.primaryEntranceLongitude) {
                actions.push({
                  key: "map",
                  label: "Map",
                  icon: <CompassOutlined />,
                  to: NavigationService.GenerateMapUrl(
                    cave.primaryEntranceLatitude,
                    cave.primaryEntranceLongitude,
                    15
                  ),
                });
              }

              return (
                <GridCard
                  actions={actions}
                  className="caves-result-card"
                  stickyFooter
                  stickyHeader
                  header={
                    <div className="caves-result-card__header">
                      <div className="caves-result-card__title">
                        <span className="caves-result-card__display-id">
                          {cave.displayId}
                        </span>{" "}
                        <span className="caves-result-card__name">
                          {cave.name}
                        </span>
                      </div>
                      {shouldShowDistanceFromMe && (
                        <div className="caves-result-card__distance-from-me">
                          <DistanceFromMeComponent
                            latitude={cave.primaryEntranceLatitude}
                            longitude={cave.primaryEntranceLongitude}
                          />
                        </div>
                      )}
                    </div>
                  }
                  headerExtra={
                    <FavoriteCave
                      initialIsFavorite={cave.isFavorite}
                      caveId={cave.id}
                      onlyShowWhenFavorite
                      disabled
                    />
                  }
                >
                  <Space direction="vertical" size={3}>
                    {selectedFeatures
                      .filter((featureKey) => featureKey !== nameof<CaveSearchVm>("distanceMiles"))
                      .map((featureKey) => (
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
                            {CAVE_SEARCH_DISPLAY_FEATURE_LABELS[featureKey]}
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
                                  : cave.narrativeSnippet.substring(0, 400) +
                                  "…",
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
              );
            }}
            itemKey={(cave) => cave.id}
            pagedItems={caves}
            queryBuilder={queryBuilder}
            onSearch={onSearch}
            onScrollStateChange={(isScrolled) => {
              setIsResultsScrolled(isScrolled);
              if (!isMobile && isScrolled && !hasAutoCollapsedDisplay) {
                setIsDisplayCollapsed(true);
                setHasAutoCollapsedDisplay(true);
              }
            }}
          />
        </SpinnerCardComponent>
      </div>
    </div>
  );
};

export { CavesComponent };
