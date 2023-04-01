import { Select, Spin } from "antd";
import { useEffect, useState } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { ProjectService } from "../../Project/Services/ProjectService";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { TagType } from "../Models/TagType";

export interface TagSelectComponentProps {
  onChange?: (value: string[]) => void;
  defaultValue?: string[];
  tagType: TagType;
  projectId: string;
}
const TagSelectComponent: React.FC<TagSelectComponentProps> = (props) => {
  const [tags, setTags] = useState<SelectListItem<string>[]>();
  const [isLoading, setIsLoading] = useState(true);

  const [defaultValue, setDefaultValue] = useState<string[]>([]);

  useEffect(() => {
    const getTags = async () => {
      switch (props.tagType) {
        case TagType.Trip:
          const tripTags = await SettingsService.GetTripTags();
          setTags(tripTags);
          break;
        case TagType.ProjectMember:
          const projectMembers = await ProjectService.GetProjectMembers(
            props.projectId
          );
          setTags(projectMembers);

          break;
        default:
          throw new Error("Invalid tag type");
      }
      setIsLoading(false);
      setDefaultValue(props.defaultValue ?? []);
    };
    getTags();
  }, []);

  return (
    <Spin spinning={isLoading}>
      <Select
        loading={isLoading}
        notFoundContent={isLoading ? <Spin size="small" /> : null}
        mode="multiple"
        value={defaultValue}
        allowClear
        onChange={(e) => {
          setDefaultValue(e);
          if (props.onChange) {
            props?.onChange(e);
          }
        }}
      >
        {tags?.map((tag) => (
          <Select.Option key={tag.value} value={tag.value}>
            {tag.display}
          </Select.Option>
        ))}
      </Select>
    </Spin>
  );
};

export { TagSelectComponent };
