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
    this.conditions = this.conditions.filter(
      (condition) => condition.field !== condition.field
    );

    this.conditions.push(condition);
    return this;
  }

  public buildAsQueryString() {
    const conditionsQueryString = this.convertArrayToQueryParams(
      this.conditions
    );
    const filterQueryString = this.convertToQueryParams({
      pageNumber: 1,
      pageSize: 10,
    });

    const queryString = `${conditionsQueryString}&${filterQueryString}`;

    window.history.pushState({}, "", `?${queryString}`);

    return filterQueryString;
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
