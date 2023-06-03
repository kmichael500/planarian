import { QueryBuilder } from "../Services/QueryBuilder";

export interface FilterFormProps<T> {
  queryBuilder: QueryBuilder<T>;
}

export interface FilterFormItemProps<T> extends FilterFormProps<T> {
  queryBuilder: QueryBuilder<T>;
  field: keyof T;
  label: string;
}
