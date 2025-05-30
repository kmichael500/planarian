import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { Form, SelectProps } from "antd";

export interface TagFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T>,
    SelectProps<string> {
  projectId?: string;
  tagType: TagType;
}

const TagFilterFormItem = <T extends object>({
  queryBuilder,
  field,
  projectId,
  tagType,
  label,
}: TagFilterFormItemProps<T>) => {
  return (
    <Form.Item name={field.toString()} label={label}>
      <TagSelectComponent
        projectId={projectId}
        tagType={tagType}
        onChange={(value) => {
          queryBuilder.filterBy(field, QueryOperator.In, value as any);
        }}
        mode="multiple"
      />
    </Form.Item>
  );
};

export { TagFilterFormItem };
