import { QueryBuilder } from "../Services/QueryBuilder";

export interface FilterFormItemProps<T> {
  queryBuilder: QueryBuilder<T>;
  field: keyof T;
  label: string;
}
