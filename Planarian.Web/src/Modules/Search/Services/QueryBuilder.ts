// interface IFieldValue<T> {
//   field: keyof T;
//   value: T[keyof T];
// }

// class FieldValue<T> implements IFieldValue<T> {
//   constructor(public field: keyof T, public value: T[keyof T]) {}
// }

// interface IQueryOperator<T> {
//   operator: string;
//   fieldValue: FieldValue<T>;
// }

// class QueryOperator<T> implements IQueryOperator<T> {
//   constructor(public operator: string, public fieldValue: FieldValue<T>) {}
// }

// class QueryBuilder<T> {
//   private conditions: QueryOperator<T>[] = [];

//   equal(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(QueryOperatorEnum.Equal, new FieldValue(field, value))
//     );
//     return this;
//   }

//   notEqual(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.NotEqual,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   lessThan(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.LessThan,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   greaterThan(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.GreaterThan,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   greaterThanOrEqual(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.GreaterThanOrEqual,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   lessThanOrEqual(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.LessThanOrEqual,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   contains(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.Contains,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   notContains(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.NotStartsWith,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   startsWith(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.StartsWith,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   notStartsWith(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.NotStartsWith,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   endsWith(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.EndsWith,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   notEndsWith(field: keyof T, value: T[keyof T]) {
//     this.push(
//       new QueryOperator(
//         QueryOperatorEnum.NotEndsWith,
//         new FieldValue(field, value)
//       )
//     );
//     return this;
//   }

//   private push(operator: QueryOperator<T>) {
//     if (
//       typeof operator.fieldValue.value === "string" ||
//       operator.fieldValue.value instanceof String
//     ) {
//       const escapedValue = operator.fieldValue.value.replace(
//         /([(),|]|\/i)/g,
//         "\\$1"
//       );
//       operator.fieldValue.value = escapedValue as any;
//     }
//     this.conditions.push(operator);
//     return this;
//   }

//   build(by: QueryLogicalOperatorEnum = QueryLogicalOperatorEnum.And) {
//     return this.conditions;
//   }
// }

// enum QueryOperatorEnum {
//   Equal = "=",
//   NotEqual = "!=",
//   LessThan = "<",
//   GreaterThan = ">",
//   GreaterThanOrEqual = ">=",
//   LessThanOrEqual = "<=",
//   Contains = "=*",
//   NotContains = "!*",
//   StartsWith = "^",
//   NotStartsWith = "!^",
//   EndsWith = "$",
//   NotEndsWith = "!$",
// }

// enum QueryLogicalOperatorEnum {
//   And = ",",
//   Or = "|",
// }

// export { QueryBuilder };
export {};
