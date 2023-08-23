import { Form, Select, SelectProps, Spin } from "antd";
import { useEffect, useState } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { ProjectService } from "../../Project/Services/ProjectService";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { TagType } from "../Models/TagType";

export interface TagSelectComponentProps extends SelectProps<string> {
  tagType: TagType;
  projectId?: string;
}
const TagSelectComponent: React.FC<TagSelectComponentProps> = ({
  tagType,
  projectId,
  onChange,
  ...rest
}) => {
  const [tags, setTags] = useState<SelectListItem<string>[]>();
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const getTags = async () => {
      switch (tagType) {
        case TagType.Trip:
          const tripTags = await SettingsService.GetTripTags();
          setTags(tripTags);
          break;
        case TagType.ProjectMember:
          if (!projectId) {
            throw new Error("Project id is required");
          }
          const projectMembers = await ProjectService.GetProjectMembers(
            projectId
          );
          setTags(projectMembers);

          break;
        case TagType.Geology:
          const geologyTags = await SettingsService.GetGeology();
          setTags(geologyTags);
          break;
        case TagType.LocationQuality:
          const locationQualityTagss =
            await SettingsService.GetLocationQualityTags();
          setTags(locationQualityTagss);
          break;
        case TagType.FieldIndication:
          const fieldIndicationTags =
            await SettingsService.GetFieldIndicationTags();
          setTags(fieldIndicationTags);
          break;

        case TagType.EntranceHydrology:
          const entranceHydrologyTags =
            await SettingsService.GetEntranceHydrology();
          setTags(entranceHydrologyTags);

          break;
        case TagType.EntranceHydrologyFrequency:
          const entranceHydrologyFrequencyTags =
            await SettingsService.GetEntranceHydrologyFrequencyTags();
          setTags(entranceHydrologyFrequencyTags);
          break;
        case TagType.EntranceStatus:
          const entranceStatusTags =
            await SettingsService.GetEntranceStatusTags();
          setTags(entranceStatusTags);
          break;

        case TagType.File:
          const fileTags = await SettingsService.GetFileTags();
          setTags(fileTags);
          break;

        default:
          throw new Error("Invalid tag type");
      }
      setIsLoading(false);
    };
    getTags();
  }, []);

  return (
    <Spin spinning={isLoading}>
      {!isLoading && (
        <Select
          loading={isLoading}
          notFoundContent={isLoading ? <Spin size="small" /> : null}
          placeholder={`Select ${tagType ?? "tag"}`}
          allowClear
          onChange={(value, option) => {
            if (onChange) {
              onChange(value, option);
            }
          }}
          {...rest}
        >
          {tags?.map((tag) => (
            <Select.Option key={tag.value} value={tag.value}>
              {tag.display}
            </Select.Option>
          ))}
        </Select>
      )}
    </Spin>
  );
};

export { TagSelectComponent };
