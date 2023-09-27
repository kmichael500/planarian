import { Card, Space, Tabs } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import TagTypeEditComponent, { TagTypeTableVm } from "./TagTypeEditComponent";
import { TagType } from "../../Tag/Models/TagType";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";

const AccountSettingsComponent = () => {
  const includedTagTypes = [
    TagType.EntranceHydrology,
    TagType.EntranceHydrologyFrequency,
    TagType.EntranceStatus,
    TagType.LocationQuality,
    TagType.FieldIndication,
    TagType.File,
    TagType.Geology,
  ];
  return (
    <>
      <Space direction="vertical">
        <Card title="Manage Tags">
          <Tabs type="card">
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
