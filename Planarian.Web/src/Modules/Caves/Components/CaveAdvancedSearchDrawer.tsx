import {
  Button,
  Divider,
  Form,
  FormInstance,
  InputNumber,
  Space,
  Typography,
} from "antd";
import {
  AimOutlined,
  DownloadOutlined,
} from "@ant-design/icons";
import {
  AdvancedSearchDrawerComponent,
  AdvancedSearchInlineControlsContext,
} from "../../Search/Components/AdvancedSearchDrawerComponent";
import { QueryBuilder, QueryOperator } from "../../Search/Services/QueryBuilder";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { StateCountyFilterFormItem } from "../../Search/Components/StateFilterFormItem";
import { BooleanFilterFormItem } from "../../Search/Components/BooleanFilterFormItem";
import { TextFilterFormItem } from "../../Search/Components/TextFilterFormItem";
import { NumberComparisonFormItem } from "../../Search/Components/NumberFilterFormItem";
import { TagFilterFormItem } from "../../Search/Components/TagFilterFormItem";
import { EntrancePolygonFilterFormItem } from "../../Search/Components/EntrancePolygonFilterFormItem";
import { ShouldDisplay, useFeatureEnabled } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { TagType } from "../../Tag/Models/TagType";
import {
  Fragment,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { FeatureCheckboxGroup } from "./FeatureCheckboxGroup";
import { performCaveExport, ExportType } from "../Service/CaveExportService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { getFeatureKeyLabel } from "../../Account/Models/FeatureKeyHelpers";
import { EntranceLocationFilter } from "../../Search/Helpers/EntranceLocationFilterHelpers";

export interface CaveAdvancedSearchDrawerProps {
  queryBuilder: QueryBuilder<CaveSearchParamsVm>;
  onSearch: () => Promise<void>;
  onFiltersCleared?: () => void;
  form?: FormInstance<CaveSearchParamsVm>;
  sortOptions?: SelectListItem<string>[];
  onSortChange?: (sortBy: string) => Promise<void> | void;
  inlineControls?: (
    context: AdvancedSearchInlineControlsContext<CaveSearchParamsVm>
  ) => React.ReactNode;
  entranceLocationFilter: EntranceLocationFilter;
  isFetchingEntranceLocation: boolean;
  onEntranceLocationChange: (
    field: keyof EntranceLocationFilter,
    value: number | string | null
  ) => void;
  onUseCurrentLocation: () => void;
  onClearEntranceLocation: () => void;
  polygonResetSignal?: number;
  filterClearSignal?: number;
}

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

const CaveAdvancedSearchDrawer: React.FC<CaveAdvancedSearchDrawerProps> = ({
  queryBuilder,
  onSearch,
  onFiltersCleared,
  form,
  sortOptions,
  onSortChange,
  inlineControls,
  entranceLocationFilter,
  isFetchingEntranceLocation,
  onEntranceLocationChange,
  onUseCurrentLocation,
  onClearEntranceLocation,
  polygonResetSignal,
  filterClearSignal,
}) => {
  const { isFeatureEnabled } = useFeatureEnabled();
  const accountId = AuthenticationService.GetAccountId();
  const exportFeatureStorageKey = accountId
    ? `${accountId}-exportFeatures`
    : "exportFeatures";

  const [enabledExportKeys, setEnabledExportKeys] = useState<FeatureKey[]>([]);
  const [exportFeatureKeys, setExportFeatureKeys] = useState<FeatureKey[]>([]);
  const [isExportModalVisible, setIsExportModalVisible] = useState(false);
  const [pendingExportType, setPendingExportType] = useState<ExportType | null>(
    null
  );
  const [isExporting, setIsExporting] = useState(false);

  useEffect(() => {
    const allFeatureKeys = Object.values(FeatureKey) as FeatureKey[];
    const enabledKeys = allFeatureKeys.filter((key) => {
      if (key === FeatureKey.EnabledFieldCaveDistance) {
        return false;
      }
      return isFeatureEnabled(key);
    });

    const sortedKeys = [...enabledKeys].sort((a, b) => {
      const orderA = getFeatureOrder(a);
      const orderB = getFeatureOrder(b);
      if (orderA !== orderB) {
        return orderA - orderB;
      }
      return getFeatureKeyLabel(a).localeCompare(getFeatureKeyLabel(b));
    });

    setEnabledExportKeys(sortedKeys);

    let savedKeys = sortedKeys;
    if (exportFeatureStorageKey) {
      const savedJson = localStorage.getItem(exportFeatureStorageKey);
      if (savedJson) {
        try {
          const parsed = JSON.parse(savedJson) as FeatureKey[];
          const filtered = parsed.filter((key) => sortedKeys.includes(key));
          if (filtered.length > 0) {
            savedKeys = filtered;
          }
        } catch {
          savedKeys = sortedKeys;
        }
      }
    }

    setExportFeatureKeys(savedKeys);
    if (exportFeatureStorageKey) {
      localStorage.setItem(
        exportFeatureStorageKey,
        JSON.stringify(savedKeys)
      );
    }
  }, [exportFeatureStorageKey, isFeatureEnabled]);

  const exportFeatureOptions = useMemo(
    () =>
      enabledExportKeys.map((key) => ({
        label: getFeatureKeyLabel(key),
        value: key,
      })),
    [enabledExportKeys]
  );

  const handleExportFeatureChange = useCallback(
    (values: FeatureKey[]) => {
      setExportFeatureKeys(values);
      if (exportFeatureStorageKey) {
        localStorage.setItem(exportFeatureStorageKey, JSON.stringify(values));
      }
    },
    [exportFeatureStorageKey]
  );

  const beginExport = useCallback(async (type: ExportType) => {
    setPendingExportType(type);
    setIsExportModalVisible(true);
  }, []);

  const onExportGpx = useCallback(async () => {
    await beginExport("gpx");
  }, [beginExport]);

  const onExportCsv = useCallback(async () => {
    await beginExport("csv");
  }, [beginExport]);

  const handleExportConfirm = useCallback(async () => {
    if (!pendingExportType) {
      return;
    }

    setIsExporting(true);
    try {
      await performCaveExport(
        pendingExportType,
        queryBuilder,
        exportFeatureKeys
      );
      setIsExportModalVisible(false);
      setPendingExportType(null);
    } finally {
      setIsExporting(false);
    }
  }, [pendingExportType, queryBuilder, exportFeatureKeys]);

  const handleExportCancel = useCallback(() => {
    setIsExportModalVisible(false);
    setPendingExportType(null);
  }, []);

  return (
    <Fragment>
      <AdvancedSearchDrawerComponent
        queryBuilder={queryBuilder}
        onSearch={onSearch}
        mainSearchField={"name" as NestedKeyOf<CaveSearchParamsVm>}
        mainSearchFieldLabel={"Name or ID"}
        form={form}
        sortOptions={sortOptions}
        onSortChange={onSortChange}
        onExportGpx={onExportGpx}
        onExportCsv={onExportCsv}
        onFiltersCleared={onFiltersCleared}
        inlineControls={inlineControls}
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
                  onEntranceLocationChange("latitude", value)
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
                  onEntranceLocationChange("longitude", value)
                }
              />
              <InputNumber
                value={entranceLocationFilter.radius}
                placeholder="Radius (miles)"
                min={0}
                step={0.25}
                style={{ width: 160 }}
                onChange={(value) =>
                  onEntranceLocationChange("radius", value)
                }
              />
              <Button
                size="small"
                loading={isFetchingEntranceLocation}
                icon={<AimOutlined />}
                onClick={onUseCurrentLocation}
              >
                Use Current Location
              </Button>
              <Button
                size="small"
                onClick={onClearEntranceLocation}
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
            label={"Number of Pits"}
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
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldCaveReportedByNameTags}>
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
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntranceHydrologyTags}>
          <TagFilterFormItem
            tagType={TagType.EntranceHydrology}
            queryBuilder={queryBuilder}
            field={"entranceHydrologyTagIds"}
            label={"Entrance Hydrology"}
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
        <ShouldDisplay featureKey={FeatureKey.EnabledFieldEntrancePitDepth}>
          <NumberComparisonFormItem
            inputType={"number"}
            queryBuilder={queryBuilder}
            field={"entrancePitDepthFeet"}
            label={"Entrance Pit Depth (Feet)"}
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
      <PlanarianModal
        open={isExportModalVisible}
        onClose={handleExportCancel}
        header={
          pendingExportType
            ? `Export ${pendingExportType.toUpperCase()}`
            : "Select Export Fields"
        }
        footer={[
          <CancelButtonComponent key="cancel" onClick={handleExportCancel} />,
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
              onChange={handleExportFeatureChange}
            />
          ) : (
            <Typography.Text type="secondary">
              No exportable fields are currently enabled.
            </Typography.Text>
          )}
        </Space>
      </PlanarianModal>
    </Fragment>
  );
};

export { CaveAdvancedSearchDrawer };
