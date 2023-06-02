import { Form, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "./NumberComparisonFormItemProps";
import { TagSelectComponent } from "../../Tag/Components/TagSelectComponent";
import { TagType } from "../../Tag/Models/TagType";
import { useEffect } from "react";

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

  // Set the initial value using setFieldsValue when the component mounts
  useEffect(() => {
    form.setFieldsValue({
      [field]: queryBuilder.getFieldValue(field),
    });
  }, [field, form, queryBuilder]);

  return (
    <TagSelectComponent
      projectId={projectId}
      tagType={tagType}
      field={field.toString()}
      label={label}
      // defaultValue={queryBuilder.getFieldValue(field) as string[]}
      onChange={(e) => {
        queryBuilder.filterBy(field, QueryOperator.In, e as any);
      }}
    />
  );
};

export { TagFilterFormItem };
