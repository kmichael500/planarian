import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { QueryStringParser } from "./QueryStringParser";

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
  In = "=]",
}

class QueryCondition<T> implements IQueryCondition<T> {
  constructor(
    public field: keyof T,
    public key: string | undefined,
    public operator: QueryOperator,
    public value: T[keyof T]
  ) {
    if (!key) {
      this.key = field.toString();
    }
  }
}

class QueryBuilder<T> {
  private conditions: QueryCondition<T>[];
  private currentPage: number;
  private pageSize: number;

  constructor(queryString: string) {
    const filerQuery =
      QueryStringParser.parseQueryString<FilterQuery>(queryString);

    filerQuery.conditions?.forEach((element) => {
      if (element.operator === QueryOperator.In) {
        element.value = element.value.split(",");
      }
    });
    this.conditions = filerQuery.conditions ?? [];

    this.currentPage = filerQuery.pageNumber ?? 1;
    this.pageSize = filerQuery.pageSize ?? 8;
  }

  public reversedOperator(operator: QueryOperator) {
    switch (operator) {
      case QueryOperator.GreaterThan:
        return QueryOperator.LessThan;
      case QueryOperator.GreaterThanOrEqual:
        return QueryOperator.LessThanOrEqual;
      case QueryOperator.LessThanOrEqual:
        return QueryOperator.GreaterThanOrEqual;
      case QueryOperator.LessThan:
        return QueryOperator.GreaterThan;
      default:
        throw new Error("Invalid operator");
    }
  }

  changeOperators(key: string, operator: QueryOperator) {
    const existingCondition = this.conditions.find((x) => x.key === key);
    if (existingCondition) {
      existingCondition.operator = operator;
    }
  }

  public getFieldValue(key: keyof T): T[keyof T] | undefined {
    return this.conditions.find((x) => x.key === key)?.value;
  }

  equal(
    field: keyof T,
    value: T[keyof T],
    keepDuplicates = false,
    key?: string
  ) {
    this.addToDictionary(
      new QueryCondition(field, key, QueryOperator.Equal, value)
    );
    return this;
  }

  filterBy(
    field: keyof T,
    operator: QueryOperator,
    value: T[keyof T],
    key?: string
  ) {
    this.addToDictionary(new QueryCondition(field, key, operator, value));
    return this;
  }

  private addToDictionary(condition: QueryCondition<T>) {
    const existingCondition = this.conditions.find(
      (x) => x.key === condition.key
    );
    if (existingCondition) {
      existingCondition.operator = condition.operator;
      existingCondition.value = condition.value;
    } else {
      this.conditions.push(condition);
    }
    return this;
  }

  public changePage(pageNumber: number, pageSize: number) {
    this.currentPage = pageNumber;
    this.pageSize = pageSize;
    return this;
  }

  public buildAsQueryString() {
    const conditions = [] as QueryCondition<T>[];
    this.conditions.forEach((condition) => {
      if (condition.value !== null || condition.value !== undefined) {
        if (
          Array.isArray(condition.value) &&
          condition.value.every((item) => typeof item === "string")
        ) {
          if (condition.value.length > 0) {
            var shallowCopy = Object.assign({}, condition);
            shallowCopy.value = condition.value.join(",") as T[keyof T];
            conditions.push(shallowCopy);
          }
        } else if (
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

export interface FilterQuery {
  conditions?: QueryCondition<any>[];
  pageSize?: number;
  pageNumber?: number;
}

export { QueryBuilder };
