import {
  Typography,
  Form,
  Space,
  Divider,
  message,
  Tag,
  Spin,
  InputNumber,
  Button,
} from "antd";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { AdvancedSearchDrawerComponent } from "../../Search/Components/AdvancedSearchDrawerComponent";
import { PagedResult } from "../../Search/Models/PagedResult";
import DOMPurify from "dompurify";

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
  formatCoordinateNumber,
} from "../../../Shared/Helpers/StringHelpers";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { EyeOutlined, CompassOutlined, AimOutlined, DownloadOutlined } from "@ant-design/icons";
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
import FavoriteCave from "./FavoriteCave";
import { BooleanFilterFormItem } from "../../Search/Components/BooleanFilterFormItem";
import { EntrancePolygonFilterFormItem } from "../../Search/Components/EntrancePolygonFilterFormItem";
import { LocationHelpers } from "../../../Shared/Helpers/LocationHelpers";
import { FeatureCheckboxGroup } from "./FeatureCheckboxGroup";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { getFeatureKeyLabel } from "../../Account/Models/FeatureKeyHelpers";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";

const query = window.location.search.substring(1);
const queryBuilder = new QueryBuilder<CaveSearchParamsVm>(query);

const CavesComponent: React.FC = () => {
  let [caves, setCaves] = useState<PagedResult<CaveSearchVm>>();
  let [isCavesLoading, setIsCavesLoading] = useState(true);
  const [form] = Form.useForm<CaveSearchParamsVm>();
  let [selectedFeatures, setSelectedFeatures] = useState<
    NestedKeyOf<CaveSearchVm>[]
  >([]);
  const [exportFeatureKeys, setExportFeatureKeys] = useState<FeatureKey[]>([]);
  const [enabledExportKeys, setEnabledExportKeys] = useState<FeatureKey[]>([]);
  const [hasAppliedFilters, setHasAppliedFilters] = useState(
    queryBuilder.hasFilters()
  );
  const [filterClearSignal, setFilterClearSignal] = useState(0);
  const [polygonResetSignal, setPolygonResetSignal] = useState(0);

  const [locationPermissionGranted, setLocationPermissionGranted] = useState<boolean | null>(null);

  const [expandedNarratives, setExpandedNarratives] = useState<
    Record<string, boolean>
  >({});
  const [isExportModalVisible, setIsExportModalVisible] = useState(false);
  const [pendingExportType, setPendingExportType] = useState<
    "csv" | "gpx" | null
  >(null);
  const [isExporting, setIsExporting] = useState(false);

  const accountId = AuthenticationService.GetAccountId();
  const featureStorageKey = accountId
    ? `${accountId}-selectedFeatures`
    : "selectedFeatures";
  const exportFeatureStorageKey = accountId
    ? `${accountId}-exportFeatures`
    : "exportFeatures";

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

  type EntranceLocationFilter = {
    latitude?: number;
    longitude?: number;
    radius?: number;
  };

  const parseEntranceLocationFilter = (
    value: string | undefined | null
  ): EntranceLocationFilter => {
    if (!value) {
      return {};
    }

    const parts = value.split(",").map((part) => part.trim());
    if (parts.length !== 3) {
      return {};
    }

    const latitude = Number(parts[0]);
    const longitude = Number(parts[1]);
    const radius = Number(parts[2]);

    if (
      [latitude, longitude, radius].some(
        (entry) => Number.isNaN(entry) || !Number.isFinite(entry)
      )
    ) {
      return {};
    }

    return { latitude, longitude, radius };
  };

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

    const { latitude, longitude, radius } = next;

    if (
      latitude !== undefined &&
      longitude !== undefined &&
      radius !== undefined &&
      !Number.isNaN(latitude) &&
      !Number.isNaN(longitude) &&
      !Number.isNaN(radius) &&
      radius > 0
    ) {
      const formattedLatitude = formatCoordinateNumber(latitude);
      const formattedLongitude = formatCoordinateNumber(longitude);
      const formattedRadius = radius;

      const serializedValue = `${formattedLatitude},${formattedLongitude},${formattedRadius}`;

      queryBuilder.filterBy(
        "entranceLocation" as NestedKeyOf<CaveSearchParamsVm>,
        QueryOperator.Equal,
        serializedValue as any
      );
    } else {
      queryBuilder.removeFromDictionary("entranceLocation");
    }
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

  type ExportType = "csv" | "gpx";

  const beginExport = (type: ExportType) => {
    setPendingExportType(type);
    setIsExportModalVisible(true);
  };

  const handleCloseExportModal = () => {
    setIsExportModalVisible(false);
    setPendingExportType(null);
    setIsExporting(false);
  };

  const handleExportConfirm = async () => {
    if (!pendingExportType) {
      return;
    }

    setIsExporting(true);
    const exportLabel = pendingExportType.toUpperCase();
    const hide = message.loading(`Exporting ${exportLabel}...`, 0);

    try {
      const response =
        pendingExportType === "gpx"
          ? await CaveService.ExportCavesGpx(queryBuilder, exportFeatureKeys)
          : await CaveService.ExportCavesCsv(queryBuilder, exportFeatureKeys);

      const accountName = AuthenticationService.GetAccountName();
      const localDateTime = new Date().toISOString();
      const fileExtension = pendingExportType;
      const fileName = `${accountName} ${localDateTime}.${fileExtension}`;

      saveAs(response, fileName);
      handleCloseExportModal();
    } catch (err) {
      const error = err as ApiErrorResponse;
      message.error(error.message);
    } finally {
      hide();
      setIsExporting(false);
    }
  };

  const onExportGpx = async () => {
    beginExport("gpx");
  };

  const onExportCsv = async () => {
    beginExport("csv");
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
  const exportFeatureOrder: FeatureKey[] = [
    FeatureKey.EnabledFieldCaveId,
    FeatureKey.EnabledFieldCaveDistance,
    FeatureKey.EnabledFieldCaveCounty,
    FeatureKey.EnabledFieldCaveLengthFeet,
    FeatureKey.EnabledFieldCaveDepthFeet,
    FeatureKey.EnabledFieldCaveReportedOn,
    FeatureKey.EnabledFieldCaveMaxPitDepthFeet,
    FeatureKey.EnabledFieldCaveNumberOfPits,
    FeatureKey.EnabledFieldCaveMapStatusTags,
    FeatureKey.EnabledFieldCaveGeologyTags,
    FeatureKey.EnabledFieldCaveGeologicAgeTags,
    FeatureKey.EnabledFieldCaveArcheologyTags,
    FeatureKey.EnabledFieldCaveBiologyTags,
    FeatureKey.EnabledFieldCaveCartographerNameTags,
    FeatureKey.EnabledFieldCavePhysiographicProvinceTags,
    FeatureKey.EnabledFieldCaveReportedByNameTags,
    FeatureKey.EnabledFieldCaveOtherTags,
    FeatureKey.EnabledFieldCaveName,
    FeatureKey.EnabledFieldCaveAlternateNames,
    FeatureKey.EnabledFieldCaveState,
    FeatureKey.EnabledFieldCaveNarrative,
    FeatureKey.EnabledFieldEntranceName,
    FeatureKey.EnabledFieldEntranceDescription,
    FeatureKey.EnabledFieldEntranceReportedOn,
    FeatureKey.EnabledFieldEntrancePitDepth,
    FeatureKey.EnabledFieldEntranceCoordinates,
    FeatureKey.EnabledFieldEntranceElevation,
    FeatureKey.EnabledFieldEntranceLocationQuality,
    FeatureKey.EnabledFieldEntranceStatusTags,
    FeatureKey.EnabledFieldEntranceFieldIndicationTags,
    FeatureKey.EnabledFieldEntranceHydrologyTags,
    FeatureKey.EnabledFieldEntranceReportedByNameTags,
  ];

  const getFeatureOrder = (key: FeatureKey) => {
    const index = exportFeatureOrder.indexOf(key);
    return index === -1 ? Number.MAX_SAFE_INTEGER : index;
  };
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

      const allFeatureKeys = Object.values(FeatureKey) as FeatureKey[];
      const enabledExportKeysList = allFeatureKeys.filter(
        (key) =>
          key !== FeatureKey.EnabledFieldCaveDistance &&
          isFeatureEnabled(key)
      );
      const sortedExportKeys = [...enabledExportKeysList].sort((a, b) => {
        const orderA = getFeatureOrder(a);
        const orderB = getFeatureOrder(b);

        if (orderA !== orderB) {
          return orderA - orderB;
        }

        return getFeatureKeyLabel(a).localeCompare(getFeatureKeyLabel(b));
      });
      setEnabledExportKeys(sortedExportKeys);

      let savedExportFeatureKeys: FeatureKey[] = sortedExportKeys;
      const savedExportJson = exportFeatureStorageKey
        ? localStorage.getItem(exportFeatureStorageKey)
        : null;

      if (savedExportJson) {
        try {
          const parsed = JSON.parse(savedExportJson) as FeatureKey[];
          const filtered = parsed.filter((key) =>
            enabledExportKeysList.includes(key)
          );
          if (filtered.length > 0) {
            savedExportFeatureKeys = filtered;
          }
        } catch {
          savedExportFeatureKeys = sortedExportKeys;
        }
      }

      setFilteredFeatures(enabledFeatures);
      setSelectedFeatures(filteredSelectedFeatures);
      setExportFeatureKeys(savedExportFeatureKeys);

      if (featureStorageKey) {
        localStorage.setItem(
          featureStorageKey,
          JSON.stringify(filteredSelectedFeatures)
        );
      }

      if (exportFeatureStorageKey) {
        localStorage.setItem(
          exportFeatureStorageKey,
          JSON.stringify(savedExportFeatureKeys)
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

  const exportFeatureOptions = enabledExportKeys
    .slice()
    .sort((a, b) => {
      const orderA = getFeatureOrder(a);
      const orderB = getFeatureOrder(b);

      if (orderA !== orderB) {
        return orderA - orderB;
      }

      return getFeatureKeyLabel(a).localeCompare(getFeatureKeyLabel(b));
    })
    .map((key) => ({
      label: getFeatureKeyLabel(key),
      value: key,
    }));

  return (
    <>
      <AdvancedSearchDrawerComponent
        mainSearchField={"name"}
        mainSearchFieldLabel={"Name or ID"}
        onSearch={onSearch}
        queryBuilder={queryBuilder}
        form={form}
        sortOptions={sortOptions}
        onSortChange={handleSortChange}
        onExportGpx={onExportGpx}
        onExportCsv={onExportCsv}
        onFiltersCleared={() => {
          applyEntranceLocationFilter({});
          setFilterClearSignal((previous) => previous + 1);
          setPolygonResetSignal((previous) => previous + 1);
        }}
      >
        <Divider>Cave</Divider>
        <BooleanFilterFormItem
          queryBuilder={queryBuilder}
          field={"isFavorite"}
          label={"Favorites"}
        />
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
        <Form.Item label="Entrance Radius Search">
          <Space direction="vertical" size={8} style={{ width: "100%" }}>
            <Space wrap>
              <InputNumber
                value={entranceLocationFilter.latitude}
                placeholder="Latitude"
                min={-90}
                max={90}
                step={0.000001}
                style={{ width: 140 }}
                onChange={(value) =>
                  handleEntranceLocationChange("latitude", value)
                }
              />
              <InputNumber
                value={entranceLocationFilter.longitude}
                placeholder="Longitude"
                min={-180}
                max={180}
                step={0.000001}
                style={{ width: 140 }}
                onChange={(value) =>
                  handleEntranceLocationChange("longitude", value)
                }
              />
              <InputNumber
                value={entranceLocationFilter.radius}
                placeholder="Radius (miles)"
                min={0}
                step={0.25}
                style={{ width: 160 }}
                onChange={(value) =>
                  handleEntranceLocationChange("radius", value)
                }
              />
              <Button
                size="small"
                loading={isFetchingEntranceLocation}
                icon={<AimOutlined />}
                onClick={handleUseCurrentLocation}
              >
                Use Current Location
              </Button>
              <Button
                size="small"
                onClick={() => applyEntranceLocationFilter({})}
              >
                Clear
              </Button>
            </Space>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Finds caves with an entrance within the selected radius of the
              specified point.
            </Typography.Text>
          </Space>
        </Form.Item>
        <EntrancePolygonFilterFormItem
          queryBuilder={queryBuilder}
          field={"entrancePolygon" as NestedKeyOf<CaveSearchParamsVm>}
          label={"Entrance Polygon Search"}
          resetSignal={polygonResetSignal}
          helpText={
            "Draw a polygon on the map or drag a zipped shapefile onto the map to return caves whose entrances fall inside the area. Note: The shapefile must contain a polygon; lines or points are not supported."
          }
        />
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveLengthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"lengthFeet"}
            label={"Length (Feet)"}
            onFiltersCleared={filterClearSignal}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveDepthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"depthFeet"}
            label={"Depth (Feet)"}
            onFiltersCleared={filterClearSignal}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveNumberOfPits}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"numberOfPits"}
            label={"Number of Pits (Feet)"}
            onFiltersCleared={filterClearSignal}
          />
        </ShouldDisplay>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveMaxPitDepthFeet}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"maxPitDepthFeet"}
            label={"Max Pit Depth (Feet)"}
            onFiltersCleared={filterClearSignal}
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
            onFiltersCleared={filterClearSignal}
          />
        </ShouldDisplay>
        <Divider>Entrance</Divider>
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceElevation}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"elevationFeet"}
            label={"Elevation (Feet)"}
            onFiltersCleared={filterClearSignal}
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
            onFiltersCleared={filterClearSignal}
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
            onFiltersCleared={filterClearSignal}
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
        />
        <TextFilterFormItem
          queryBuilder={queryBuilder}
          field={"fileExtension"}
          label={"File Extension"}
          queryOperator={QueryOperator.EndsWith}
          helpText={"Enter values like pdf or .pdf"}
        />
      </AdvancedSearchDrawerComponent>

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
                  <div style={{ display: "flex", alignItems: "center" }}>
                    <span style={{ fontSize: 18, fontWeight: 700 }}>
                      <span style={{ color: '#3874f6', fontWeight: 800, marginRight: 8 }}>{cave.displayId}</span>
                      {cave.name}
                    </span>
                  </div>}
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
      <PlanarianModal
        open={isExportModalVisible}
        onClose={handleCloseExportModal}
        header={
          pendingExportType
            ? `Export ${pendingExportType.toUpperCase()}`
            : "Select Export Fields"
        }
        footer={[
          <CancelButtonComponent

            key="cancel"
            onClick={handleCloseExportModal}
          />
          ,
          <PlanarianButton
            key="export"
            type="primary"
            alwaysShowChildren
            onClick={handleExportConfirm}
            loading={isExporting}
            disabled={exportFeatureKeys.length === 0 || isExporting}
            icon={<DownloadOutlined />}
          >
            {pendingExportType
              ? `Export ${pendingExportType.toUpperCase()}`
              : "Export"}
          </PlanarianButton>,
        ]}
      >
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <Typography.Paragraph style={{ marginBottom: 0 }}>
            Choose which fields to include in the export file.
          </Typography.Paragraph>
          {exportFeatureOptions.length > 0 ? (
            <FeatureCheckboxGroup
              title="Export Fields"
              options={exportFeatureOptions}
              value={exportFeatureKeys}
              onChange={(values) => {
                setExportFeatureKeys(values);
                if (exportFeatureStorageKey) {
                  localStorage.setItem(
                    exportFeatureStorageKey,
                    JSON.stringify(values)
                  );
                }
              }}
            />
          ) : (
            <Typography.Text type="secondary">
              No exportable fields are currently enabled.
            </Typography.Text>
          )}
        </Space>
      </PlanarianModal>
    </>
  );
};

export { CavesComponent };
