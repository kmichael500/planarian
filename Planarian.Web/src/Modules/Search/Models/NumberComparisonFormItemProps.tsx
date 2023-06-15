import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { QueryBuilder } from "../Services/QueryBuilder";

export interface FilterFormProps<T extends object> {
  queryBuilder: QueryBuilder<T>;
}

export interface FilterFormItemProps<T extends object>
  extends FilterFormProps<T> {
  queryBuilder: QueryBuilder<T>;
  field: NestedKeyOf<T>;
  label: string;
}
