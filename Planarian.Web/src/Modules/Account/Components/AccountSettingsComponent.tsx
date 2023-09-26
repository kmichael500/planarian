import { Card, TagType } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import TagTypeEditComponent, { TagTypeEditVm } from "./TagTypeEditComponent";

const AccountSettingsComponent = () => {
  const [geologyTags, setGeologyTags] = useState<TagTypeEditVm[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  useEffect(() => {
    const getTags = async () => {
      var tags = await SettingsService.GetGeology();
      var tagVms = tags.map((tag) => {
        return {
          tagTypeId: tag.value,
          name: tag.display,
          isDeletable: true,
        } as TagTypeEditVm;
      });
      setGeologyTags(tagVms);
      setIsLoading(false);
    };
    getTags();
  }, []);
  return (
    <>
      <ResetAccountComponent />
      <Card title="Geology Tags">
        <TagTypeEditComponent
          tagTypes={geologyTags}
          handleSave={async (tagType: TagTypeEditVm) => {
            console.log(tagType);
          }}
          handleDelete={async (tagTypeId: string) => {
            console.log(tagTypeId);
          }}
          handleAdd={async (name: string) => {
            console.log(name);
            return {
              tagTypeId: "1",
              name: name,
              isDeletable: true,
            } as TagTypeEditVm;
          }}
        />
      </Card>
    </>
  );
};

export { AccountSettingsComponent };
