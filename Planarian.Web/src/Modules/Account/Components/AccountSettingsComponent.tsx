import { Card, Space, Tabs } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import TagTypeEditComponent, { TagTypeTableVm } from "./TagTypeEditComponent";
import { TagType } from "../../Tag/Models/TagType";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";
import { useLocation, useNavigate } from "react-router-dom";
import { toEnum } from "../../../Shared/Helpers/EnumHelpers";

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

  const navigate = useNavigate();
  const location = useLocation();
  const [activeKey, setActiveKey] = useState("");

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const tagType = toEnum(TagType, params.get("tagType"));
    if (tagType && includedTagTypes.includes(tagType)) {
      setActiveKey(tagType);
    } else if (includedTagTypes.length > 0) {
      setActiveKey(includedTagTypes[0]); // Default to the first tag type if none is specified
    }
  }, [location.search, includedTagTypes]);

  const onTabChange = (key: string) => {
    setActiveKey(key);
    navigate(`?tagType=${key}`, { replace: true });
  };

  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    const tagType = toEnum(TagType, searchParams.get("tagType"));
    if (tagType && includedTagTypes.includes(tagType)) {
      setActiveKey(tagType);
    } else if (includedTagTypes.length > 0) {
      setActiveKey(includedTagTypes[0]);
      navigate(`?tagType=${includedTagTypes[0]}`, { replace: true });
    }
  }, [location.search]);

  return (
    <>
      <Space direction="vertical">
        <Card title="Manage Tags">
          <Tabs
            type="card"
            tabPosition="left"
            activeKey={activeKey}
            onChange={onTabChange}
          >
            {includedTagTypes.map((tagType) => (
              <Tabs.TabPane tab={splitCamelCase(tagType)} key={tagType}>
                <TagTypeEditComponent tagType={tagType} />
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
