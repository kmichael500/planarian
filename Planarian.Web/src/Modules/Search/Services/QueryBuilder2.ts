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
  private conditions: QueryCondition<T>[] = [];

  equal(field: keyof T, value: T[keyof T]) {
    this.push(new QueryCondition(field, QueryOperator.Equal, value));
    return this;
  }

  filterBy(field: keyof T, operator: QueryOperator, value: T[keyof T]) {
    this.push(new QueryCondition(field, operator, value));
    return this;
  }

  private push(condition: QueryCondition<T>) {
    if (
      typeof condition.value === "string" ||
      condition.value instanceof String
    ) {
      const escapedValue = condition.value.replace(/([(),|]|\/i)/g, "\\$1");
      condition.value = escapedValue as any;
    }
    this.conditions.push(condition);
    return this;
  }

  public buildAsQueryString() {
    const queryString = convertArrayToQueryParams(this.conditions);
    return queryString;
  }

  public buildGridifyQueryString() {
    const page = 1;
    const pageSize = 10;
    const filter = this.conditions
      .map((condition) => {
        return `${String(condition.field)} ${condition.operator} ${
          condition.value
        }`;
      })
      .join(",");

    const queryObject = {
      page,
      pageSize,
      filter,
    };
    return convertToQueryParams(queryObject);
  }
}

export { QueryBuilder };

function convertArrayToQueryParams(array: { [key: string]: any }[]) {
  let queryParams = "";
  for (let i = 0; i < array.length; i++) {
    const object = array[i];
    for (const key in object) {
      if (object.hasOwnProperty(key)) {
        const value = object[key];
        queryParams += `[${i}].${key}=${encodeURIComponent(value)}&`;
      }
    }
  }
  return queryParams.slice(0, -1);
}

function convertToQueryParams(object: { [key: string]: any }) {
  let queryParams = "";

  for (const key in object) {
    if (object.hasOwnProperty(key)) {
      const value = object[key];
      queryParams += `${key}=${encodeURIComponent(value)}&`;
    }
  }
  return queryParams.slice(0, -1);
}
