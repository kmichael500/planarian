import { Form } from "antd";
import { FilterFormProps } from "./NumberComparisonFormItemProps";

export interface SearchFormComponentProps<T> extends FilterFormProps<T> {
  onSearch: () => void;
  children?: React.ReactNode;
}

const SearchFormComponent = <T,>({
  queryBuilder,
  onSearch,
  children,
}: SearchFormComponentProps<T>) => {
  return (
    <Form
      onKeyDown={(e) => {
        if (e.key === "Enter") {
          onSearch();
        }
      }}
      layout="vertical"
      initialValues={queryBuilder.getDefaultValues()}
    >
      {children}
    </Form>
  );
};

export { SearchFormComponent };
