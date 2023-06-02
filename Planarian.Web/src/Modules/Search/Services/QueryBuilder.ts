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
    public operator: QueryOperator,
    public value: T[keyof T]
  ) {}
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

  reverseOperators(field: keyof T, operator: QueryOperator) {
    this.conditions.forEach((element) => {
      if (element.field === field) {
        const reversedOperator = this.reversedOperator(operator);
        if (this.isSameOperatorType(element.operator, operator)) {
          element.operator = operator;
        } else if (
          this.isSameOperatorType(element.operator, reversedOperator)
        ) {
          console.log("HERER");
          element.operator = reversedOperator;
        }

        // else if (
        //   this.isSameOperatorType(
        //     element.operator,
        //     this.reversedOperator(operator)
        //   )
        // ) {
        //   element.operator = this.reversedOperator(operator);
        // }
      }
    });
  }

  private isSameOperatorType(
    operator: QueryOperator,
    operator2: QueryOperator
  ) {
    console.log("here");
    if (
      (operator === QueryOperator.GreaterThan ||
        operator === QueryOperator.GreaterThanOrEqual) &&
      (operator2 === QueryOperator.GreaterThan ||
        operator2 === QueryOperator.GreaterThanOrEqual)
    ) {
      return true;
    }

    if (
      (operator === QueryOperator.LessThan ||
        operator === QueryOperator.LessThanOrEqual) &&
      (operator2 === QueryOperator.LessThan ||
        operator2 === QueryOperator.LessThanOrEqual)
    ) {
      return true;
    }

    return false;
  }

  public getFieldValue(field: keyof T): T[keyof T] | undefined {
    return this.conditions.find((x) => x.field === field)?.value;
  }

  equal(field: keyof T, value: T[keyof T], keepDuplicates = false) {
    this.addToDictionary(
      new QueryCondition(field, QueryOperator.Equal, value),
      keepDuplicates
    );
    return this;
  }

  filterBy(
    field: keyof T,
    operator: QueryOperator,
    value: T[keyof T],
    keepDuplicates = false
  ) {
    this.addToDictionary(
      new QueryCondition(field, operator, value),
      keepDuplicates
    );
    return this;
  }

  private addToDictionary(
    condition: QueryCondition<T>,
    keepDuplicates = false
  ) {
    const existingCondition = this.conditions.find(
      (x) =>
        (x.field === condition.field && x.operator === condition.operator) ||
        (x.field === condition.field &&
          x.operator === this.reversedOperator(condition.operator))
    );
    if (existingCondition && !keepDuplicates) {
      // this is either genius or it will cause a bug that someone will find in a year...
      if (
        existingCondition.operator !== QueryOperator.GreaterThan &&
        existingCondition.operator !== QueryOperator.GreaterThanOrEqual &&
        existingCondition.operator !== QueryOperator.LessThan &&
        existingCondition.operator !== QueryOperator.LessThanOrEqual
      ) {
        existingCondition.operator = condition.operator;
      }
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
