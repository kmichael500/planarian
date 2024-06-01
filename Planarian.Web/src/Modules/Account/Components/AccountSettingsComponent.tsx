import { Card, Space, Tabs } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import TagTypeEditComponent from "./TagTypeEditComponent";
import { TagType } from "../../Tag/Models/TagType";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";
import { useLocation, useNavigate } from "react-router-dom";
import { toEnum } from "../../../Shared/Helpers/EnumHelpers";
import CreateEditCountiesComponent from "./CreateEditCountiesComponent";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { MiscAccountSettings } from "./MiscAccountSettingsComponent";
import { EnabledFieldsComponent } from "./EnabledFieldsComponent";
import { FeatureKey, FeatureSettingVm } from "../Models/FeatureSettingVm";
import { AccountService } from "../Services/AccountService";

const AccountSettingsComponent = () => {
  const tagTypeFeatureMap: { [key in TagType]?: FeatureKey | null } = {
    [TagType.EntranceHydrology]: FeatureKey.EnabledFieldEntranceHydrologyTags,
    [TagType.People]:
      FeatureKey.EnabledFieldCaveReportedByNameTags ||
      FeatureKey.EnabledFieldEntranceReportedByNameTags ||
      FeatureKey.EnabledFieldCaveCartographerNameTags,
    [TagType.EntranceStatus]: FeatureKey.EnabledFieldEntranceStatusTags,
    [TagType.LocationQuality]: FeatureKey.EnabledFieldEntranceLocationQuality,
    [TagType.FieldIndication]:
      FeatureKey.EnabledFieldEntranceFieldIndicationTags,
    [TagType.File]: null,
    [TagType.Geology]: FeatureKey.EnabledFieldCaveGeologyTags,
    [TagType.GeologicAge]: FeatureKey.EnabledFieldCaveGeologicAgeTags,
    [TagType.PhysiographicProvince]:
      FeatureKey.EnabledFieldCavePhysiographicProvinceTags,
    [TagType.Biology]: FeatureKey.EnabledFieldCaveBiologyTags,
    [TagType.Archeology]: FeatureKey.EnabledFieldCaveArcheologyTags,
    [TagType.MapStatus]: FeatureKey.EnabledFieldCaveMapStatusTags,
    [TagType.CaveOther]: FeatureKey.EnabledFieldCaveOtherTags,
  };

  const includedTagTypes = Object.keys(tagTypeFeatureMap).sort() as TagType[];

  const [filteredTagTypes, setFilteredTagTypes] = useState<TagType[]>([]);

  const fetchFeatureSettings = async () => {
    const enabledFeatures = await AccountService.GetFeatureSettings(true);
    const enabledTagTypes = includedTagTypes.filter((tagType) => {
      const featureKey = tagTypeFeatureMap[tagType];
      if (!featureKey) return true;
      return enabledFeatures.some((f) => f.key === featureKey && f.isEnabled);
    });

    setFeatureSettings(enabledFeatures);

    setFilteredTagTypes(enabledTagTypes);
  };

  useEffect(() => {
    fetchFeatureSettings();
  }, []);

  const [states, setStates] = useState<SelectListItem<string>[]>([]);

  const [featureSettings, setFeatureSettings] = useState<FeatureSettingVm[]>(
    []
  );

  const navigate = useNavigate();
  const location = useLocation();
  const [tagsActiveKey, setTagsActiveKey] = useState("");
  const [countyActiveKey, setCountyActiveKey] = useState("");

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const tagType = toEnum(TagType, params.get("tagType"));
    if (tagType && filteredTagTypes.includes(tagType)) {
      setTagsActiveKey(tagType);
    } else if (filteredTagTypes.length > 0) {
      setTagsActiveKey(filteredTagTypes[0]);
    }

    const countyId = params.get("countyId");
    if (countyId && states.length > 0) {
      setCountyActiveKey(countyId);
    } else if (states.length > 0) {
      setCountyActiveKey(states[0].value);
    }
  }, [location.search, filteredTagTypes, states]);

  const onTagTypeTabChange = (key: string) => {
    setTagsActiveKey(key);
    navigate(`?tagType=${key}`, { replace: true });
  };

  const onCountyTabChange = (key: string) => {
    setCountyActiveKey(key);
    navigate(`?countyId=${key}`, { replace: true });
  };

  useEffect(() => {
    getStates();
  }, []);

  const getStates = async () => {
    const cavesResponse = await SettingsService.GetStates();
    setStates(cavesResponse);
  };

  const onStatesChange = (value: string[]) => {
    getStates();
  };

  return (
    <>
      <Space direction="vertical">
        <MiscAccountSettings onStatesChange={onStatesChange} />
        <Card title="Manage Tags">
          <Tabs
            type="card"
            tabPosition="left"
            activeKey={tagsActiveKey}
            onChange={onTagTypeTabChange}
          >
            {filteredTagTypes.map((tagType) => (
              <Tabs.TabPane tab={splitCamelCase(tagType)} key={tagType}>
                <TagTypeEditComponent tagType={tagType} />
              </Tabs.TabPane>
            ))}
          </Tabs>
        </Card>

        <Card title="Fields to Collect">
          <Tabs type="card">
            <Tabs.TabPane tab="Cave" key="1">
              <EnabledFieldsComponent
                featureSettings={featureSettings}
                filterType={"cave"}
                onChange={fetchFeatureSettings}
              ></EnabledFieldsComponent>
            </Tabs.TabPane>
            <Tabs.TabPane tab="Entrance" key="2">
              <EnabledFieldsComponent
                featureSettings={featureSettings}
                onChange={fetchFeatureSettings}
                filterType={"entrance"}
              ></EnabledFieldsComponent>{" "}
            </Tabs.TabPane>
          </Tabs>
        </Card>

        <Card title="Manage Counties">
          <Tabs
            type="card"
            activeKey={countyActiveKey}
            onChange={onCountyTabChange}
          >
            {states.map((state) => (
              <Tabs.TabPane tab={state.display} key={state.value}>
                <CreateEditCountiesComponent stateId={state.value} />
              </Tabs.TabPane>
            ))}
          </Tabs>
        </Card>

        <ResetAccountComponent />
      </Space>
    </>
  );
};

export { AccountSettingsComponent };
