import { NestedKeyOf } from "../Helpers/StringHelpers";

export interface SelectListItem<TValue> {
  display: string;
  value: TValue;
}

export interface SelectListItemWithData<TValue, TData>
  extends SelectListItem<TValue> {
  data: TData;
}

export interface SelectListItemKey<TValue extends object> {
  display: string;
  value: NestedKeyOf<TValue>;
  data?: any;
}

export interface SelectListItemDescriptionData<TValue, TData>
  extends SelectListItemWithData<TValue, TData> {
  description: string;
}
