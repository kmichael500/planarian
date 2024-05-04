import { Card, Space, Tabs } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import TagTypeEditComponent from "./TagTypeEditComponent";
import { TagTypeTableVm } from "../Models/TagTypeTableVm";
import { TagType } from "../../Tag/Models/TagType";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";
import { useLocation, useNavigate } from "react-router-dom";
import { toEnum } from "../../../Shared/Helpers/EnumHelpers";
import CreateEditCountiesComponent from "./CreateEditCountiesComponent";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";

const AccountSettingsComponent = () => {
  const includedTagTypes = [
    TagType.EntranceHydrology,
    TagType.People,
    TagType.EntranceStatus,
    TagType.LocationQuality,
    TagType.FieldIndication,
    TagType.File,
    TagType.Geology,
    TagType.GeologicAge,
    TagType.PhysiographicProvince,
    TagType.Biology,
    TagType.Archeology,
    TagType.MapStatus,
  ].sort();
  includedTagTypes.push(TagType.CaveOther);

  const [states, setStates] = useState<SelectListItem<string>[]>([]);

  const navigate = useNavigate();
  const location = useLocation();
  const [tagsActiveKey, setTagsActiveKey] = useState("");
  const [countyActiveKey, setCountyActiveKey] = useState("");

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const tagType = toEnum(TagType, params.get("tagType"));
    if (tagType && includedTagTypes.includes(tagType)) {
      setTagsActiveKey(tagType);
    } else if (includedTagTypes.length > 0) {
      setTagsActiveKey(includedTagTypes[0]); // Default to the first tag type if none is specified
    }

    const countyId = params.get("countyId");
    if (countyId) {
      setCountyActiveKey(countyId);
    } else if (states.length > 0) {
      setCountyActiveKey(states[0].value);
    }
  }, [location.search, includedTagTypes]);

  const onTagTypeTabChange = (key: string) => {
    setTagsActiveKey(key);
    navigate(`?tagType=${key}`, { replace: true });
  };

  const onCountyTabChange = (key: string) => {
    setCountyActiveKey(key);
    navigate(`?countyId=${key}`, { replace: true });
  };

  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    const tagType = toEnum(TagType, searchParams.get("tagType"));
    if (tagType && includedTagTypes.includes(tagType)) {
      setTagsActiveKey(tagType);
    } else if (includedTagTypes.length > 0) {
      setTagsActiveKey(includedTagTypes[0]);
      navigate(`?tagType=${includedTagTypes[0]}`, { replace: true });
    }
  }, [location.search]);

  useEffect(() => {
    getStates();
  }, []);

  const getStates = async () => {
    const cavesResponse = await SettingsService.GetStates();
    setStates(cavesResponse);
  };

  return (
    <>
      <Space direction="vertical">
        <Card title="Manage Tags">
          <Tabs
            type="card"
            tabPosition="left"
            activeKey={tagsActiveKey}
            onChange={onTagTypeTabChange}
          >
            {includedTagTypes.map((tagType) => (
              <Tabs.TabPane tab={splitCamelCase(tagType)} key={tagType}>
                <TagTypeEditComponent tagType={tagType} />
              </Tabs.TabPane>
            ))}
          </Tabs>
        </Card>

        {/* <Card title="Manage Counties">
          <CreateEditCountiesComponent stateId={"c1d2e3f4g5"} />
        </Card> */}

        <Card title="Manage Counties">
          <Tabs
            type="card"
            // tabPosition="left"
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
