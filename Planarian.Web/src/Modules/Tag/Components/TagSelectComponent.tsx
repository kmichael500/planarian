import { Form, Select, SelectProps, Spin } from "antd";
import { useEffect, useState } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { ProjectService } from "../../Project/Services/ProjectService";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { TagType } from "../Models/TagType";
import { sortSelectListItems } from "../../../Shared/Helpers/ArrayHelpers";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";

export interface TagSelectComponentProps extends SelectProps<string> {
  tagType: TagType;
  projectId?: string;
  allowCustomTags?: boolean;
}
const TagSelectComponent: React.FC<TagSelectComponentProps> = ({
  tagType,
  projectId,
  allowCustomTags = false,

  onChange,
  ...rest
}) => {
  const [tags, setTags] = useState<SelectListItem<string>[]>();
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState<string>("");

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
        case TagType.EntranceStatus:
          const entranceStatusTags =
            await SettingsService.GetEntranceStatusTags();
          setTags(entranceStatusTags);
          break;

        case TagType.File:
          const fileTags = await SettingsService.GetFileTags();
          setTags(fileTags);
          break;
        case TagType.People:
          const peopleTags = await SettingsService.GetPeopleTags();
          setTags(peopleTags);
          break;
        case TagType.Biology:
          const biologyTags = await SettingsService.GetBiologyTags();
          setTags(biologyTags);
          break;
        case TagType.Archeology:
          const archeologyTags = await SettingsService.GetArcheologyTags();
          setTags(archeologyTags);
          break;
        case TagType.MapStatus:
          const mapStatusTags = await SettingsService.GetMapStatusTags();
          setTags(mapStatusTags);
          break;
        case TagType.CaveOther:
          const otherTags = await SettingsService.GetOtherTags();
          setTags(otherTags);
          break;
        case TagType.GeologicAge:
          const geologicAgeTags = await SettingsService.GetGeologicAgeTags();
          setTags(geologicAgeTags);
          break;
        case TagType.PhysiographicProvince:
          const physiographicProvinceTags =
            await SettingsService.GetPhysiographicProvinceTags();
          setTags(physiographicProvinceTags);
          break;

        default:
          throw new Error("Invalid tag type");
      }
      setIsLoading(false);
    };
    getTags();
  }, []);

  const filteredTags = sortSelectListItems<string>(tags ?? []);

  var test = true;

  return (
    <Spin spinning={isLoading}>
      {!isLoading && (
        <Select
          showSearch
          loading={isLoading}
          notFoundContent={isLoading ? <Spin size="small" /> : null}
          placeholder={`Select ${splitCamelCase(tagType) ?? "tag"}`}
          allowClear
          onChange={(value, option) => {
            if (onChange) {
              onChange(value, option);
            }
          }}
          filterOption={(input, option) => {
            return option?.props.children
              .toLowerCase()
              .includes(input.toLowerCase());
          }}
          {...rest}
        >
          {filteredTags.map((tag) => (
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
