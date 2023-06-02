import { Form, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";

export interface TagFilterFormItemProps<T> extends FilterFormItemProps<T> {
  projectId: string;
  tagType: TagType;
}

const TagFilterFormItem = <T,>({
  queryBuilder,
  field,
  projectId,
  tagType,
  label,
}: TagFilterFormItemProps<T>) => {
  const [form] = Form.useForm(); // Create a form instance

  return (
    <TagSelectComponent
      projectId={projectId}
      tagType={tagType}
      field={field.toString()}
      label={label}
      onChange={(e) => {
        queryBuilder.filterBy(field, QueryOperator.In, e as any);
      }}
    />
  );
};

export { TagFilterFormItem };
