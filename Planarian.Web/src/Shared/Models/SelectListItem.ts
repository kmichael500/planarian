export interface SelectListItem<TValue> {
  display: string;
  value: TValue;
}

export interface SelectListItemWithData<TValue, TData>
  extends SelectListItem<TValue> {
  data: TData;
}
