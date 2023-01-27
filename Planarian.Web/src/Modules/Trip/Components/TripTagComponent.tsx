import React, { useEffect, useState } from "react";
import { Select, Spin, Tag, Tooltip } from "antd";
import { TripService } from "../Services/TripService";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";

export interface TripTagComponentProps {
  tripId: string;
  getTags: () => Promise<SelectListItem<string>[]>;
  getTagTypes: () => Promise<SelectListItem<string>[]>;
}

const TripTagComponent = (props: TripTagComponentProps) => {
  const [tags, setTags] = useState<SelectListItem<string>[]>([]);
  const [tagTypes, setTagTypes] = useState<SelectListItem<string>[]>([]);
  const [selected, setSelected] = useState<string | undefined | null>(
    undefined
  );

  const [inputVisible, setInputVisible] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function fetchTags() {
      try {
        const tags = await props.getTags();
        setTags(tags);
        const tagTypes = await props.getTagTypes();
        setTagTypes(
          tagTypes.filter(
            (tag) => !tags.some((type) => type.value === tag.value)
          )
        );

        setIsLoading(false);
      } catch (error) {
        console.error(error);
      }
    }

    fetchTags();
  }, []);

  const handleClose = async (removedTag: SelectListItem<string>) => {
    try {
      await TripService.DeleteTripTag(removedTag.value, props.tripId);
      setTags(tags.filter((tag) => tag !== removedTag));
      setTagTypes([...tagTypes, removedTag]);
    } catch (error) {
      console.error(error);
    }
  };

  const showInput = () => {
    setInputVisible(true);
  };

  const handleSelectChange = async (value: string) => {
    var tagAlreadyAdded = tags.find((tag) => tag.value === value);
    if (value && tagAlreadyAdded === undefined) {
      var selectedTag = tagTypes.find((tag) => tag.value === value);

      try {
        await TripService.AddTripTag(value, props.tripId);
        var selectedTag = tagTypes.find((tag) => tag.value === value);
        if (!selectedTag) {
          return;
        }
        setTags([...tags, selectedTag]);
        setTagTypes(tagTypes.filter((tag) => tag !== selectedTag));
        setSelected("Yooo");
        setSelected(null);

        setInputVisible(false);
      } catch (error) {
        console.error(error);
      }
    }
  };
  return (
    <Spin spinning={isLoading}>
      {tags.map((tag, index) => {
        const isLongTag = tag.display.length > 20;
        const tagElem = (
          <Tag key={tag.value} closable={true} onClose={() => handleClose(tag)}>
            {isLongTag ? `${tag.display.slice(0, 20)}...` : tag.display}
          </Tag>
        );
        return isLongTag ? (
          <Tooltip title={tag.display} key={tag.value}>
            {tagElem}
          </Tooltip>
        ) : (
          tagElem
        );
      })}
      {tagTypes.length > 0 && (
        <Select
          loading={isLoading}
          size="small"
          value={selected}
          placeholder="Add tag"
          onChange={handleSelectChange}
        >
          {tagTypes.map((tag) => {
            return (
              <Select.Option key={tag.value} value={tag.value}>
                {tag.display}
              </Select.Option>
            );
          })}
        </Select>
      )}
    </Spin>
  );
};

export { TripTagComponent };
