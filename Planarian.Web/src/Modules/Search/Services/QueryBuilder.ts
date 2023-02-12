import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";

interface IQueryCondition<T> {
  field: keyof T;
  value: T[keyof T];
  operator: QueryOperator;
}

export enum QueryOperator {
  Equal = "=",
  NotEqual = "!=",
  LessThan = "<",
  GreaterThan = ">",
  GreaterThanOrEqual = ">=",
  LessThanOrEqual = "<=",
  Contains = "=*",
  NotContains = "!*",
  StartsWith = "^",
  NotStartsWith = "!^",
  EndsWith = "$",
  NotEndsWith = "!$",
  FreeText = "*=",
}

class QueryCondition<T> implements IQueryCondition<T> {
  constructor(
    public field: keyof T,
    public operator: QueryOperator,
    public value: T[keyof T]
  ) {}
}

class QueryBuilder<T> {
  private conditions: { [key: string]: QueryCondition<T> } = {};
  private currentPage: number = 1;
  private pageSize: number = 10;

  equal(field: keyof T, value: T[keyof T]) {
    this.addToDictionary(new QueryCondition(field, QueryOperator.Equal, value));

    return this;
  }

  filterBy(field: keyof T, operator: QueryOperator, value: T[keyof T]) {
    this.addToDictionary(new QueryCondition(field, operator, value));
    return this;
  }

  private addToDictionary(condition: QueryCondition<T>) {
    this.conditions[condition.field as string] = condition;

    return this;
  }

  public changePage(pageNumber: number, pageSize: number) {
    this.currentPage = pageNumber;
    this.pageSize = pageSize;
    return this;
  }

  public buildAsQueryString() {
    const conditions = [] as QueryCondition<T>[];
    Object.values(this.conditions).forEach((condition) => {
      if (condition.value !== null || condition.value !== undefined) {
        if (
          typeof condition.value === "string" ||
          condition.value instanceof String
        ) {
          if (!isNullOrWhiteSpace(condition.value as string)) {
            conditions.push(condition);
          }
        } else {
          conditions.push(condition);
        }
      }
    });

    const pageQueryString = this.convertToQueryParams({
      pageNumber: this.currentPage,
      pageSize: this.pageSize,
    });

    const conditionsQueryString = this.convertArrayToQueryParams(conditions);

    const queryString = this.combineQueryParams(
      pageQueryString,
      conditionsQueryString
    );

    window.history.pushState({}, "", `?${queryString}`);

    return queryString;
  }

  private convertArrayToQueryParams(array: { [key: string]: any }[]) {
    let queryParams = "";
    for (let i = 0; i < array.length; i++) {
      const object = array[i];
      for (const key in object) {
        if (object.hasOwnProperty(key)) {
          const value = object[key];
          queryParams += `conditions[${i}].${key}=${encodeURIComponent(
            value
          )}&`;
        }
      }
    }
    return queryParams.slice(0, -1);
  }

  private combineQueryParams(
    queryParams1: string,
    queryParams2: string
  ): string {
    const values = [queryParams1, queryParams2];

    return values.filter((value) => value !== "").join("&");
  }

  private convertToQueryParams(object: { [key: string]: any }) {
    let queryParams = "";

    for (const key in object) {
      if (object.hasOwnProperty(key)) {
        const value = object[key];
        queryParams += `${key}=${encodeURIComponent(value)}&`;
      }
    }
    return queryParams.slice(0, -1);
  }
}

export { QueryBuilder };
