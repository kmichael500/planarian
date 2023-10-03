using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Query.Models;

namespace Planarian.Modules.Query.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> QueryFilter<T>(this IQueryable<T> source, IEnumerable<QueryCondition> conditions)
    {
        conditions = conditions.ToList();
        if (!conditions.Any()) return source;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression expression = null;

        foreach (var condition in conditions)
        {
            var properties = condition.Field.Split('.');
            Expression property = parameter;

            foreach (var propName in properties) property = Expression.Property(property, propName);

            var propertyType = property.Type;

            object value;

            if (propertyType == typeof(IEnumerable<string>))
            {
                var values = condition.Value.Split(',');
                var listExpression = typeof(List<string>).GetConstructor(new[] { typeof(IEnumerable<string>) })!;
                value = Expression.New(listExpression, Expression.Constant(values));
            }
            else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            {
                var dateTimeValue = DateTime.Parse(condition.Value);
                var underlyingType = Nullable.GetUnderlyingType(propertyType);

                if (underlyingType != null)
                {
                    // Property type is nullable
                    value = Convert.ChangeType(dateTimeValue, underlyingType);
                    value = Activator.CreateInstance(propertyType, value) ?? throw new InvalidOperationException();
                }
                else
                {
                    // Property type is non-nullable
                    value = Convert.ChangeType(dateTimeValue, propertyType);
                }
            }
            else
            {
                value = Convert.ChangeType(condition.Value, propertyType);
            }

            var constant = Expression.Constant(value);

            Expression comparison;

            switch (condition.Operator)
            {
                case QueryOperator.In:
                    var ids = condition.Value.Split(',',
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    if (property is not MemberExpression { Member: PropertyInfo })
                        throw new ArgumentException("Selector must be a property selector.");
                    var containsMethod = typeof(Enumerable).GetMethods()
                        .Single(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                        .MakeGenericMethod(typeof(string));

                    foreach (var id in ids)
                    {
                        constant = Expression.Constant(id, typeof(string));
                        var expressionBody = Expression.Call(containsMethod, property, constant);
                        var expressionLambda = Expression.Lambda<Func<T, bool>>(expressionBody, parameter);
                        source = source.Where(expressionLambda);
                    }

                    continue;
                case QueryOperator.Equal:
                    comparison = Expression.Equal(property, constant);
                    break;

                case QueryOperator.NotEqual:
                    comparison = Expression.NotEqual(property, constant);
                    break;

                case QueryOperator.GreaterThan:
                    if (IsNullableType(property.Type))
                    {
                        // Retrieve the underlying non-nullable type
                        var underlyingType = Nullable.GetUnderlyingType(property.Type);

                        // Create an expression to convert the nullable property to the non-nullable type
                        var convertedProperty = Expression.Convert(property,
                            underlyingType ?? throw new InvalidOperationException());
                        comparison = Expression.GreaterThan(convertedProperty, constant);
                    }
                    else
                    {
                        comparison = Expression.GreaterThan(property, constant);
                    }

                    break;

                case QueryOperator.GreaterThanOrEqual:
                    if (IsNullableType(property.Type))
                    {
                        // Retrieve the underlying non-nullable type
                        var underlyingType = Nullable.GetUnderlyingType(property.Type);

                        // Create an expression to convert the nullable property to the non-nullable type
                        var convertedProperty = Expression.Convert(property,
                            underlyingType ?? throw new InvalidOperationException());
                        comparison = Expression.GreaterThanOrEqual(convertedProperty, constant);
                    }
                    else
                    {
                        comparison = Expression.GreaterThanOrEqual(property, constant);
                    }

                    break;
                case QueryOperator.LessThan:
                    if (IsNullableType(property.Type))
                    {
                        // Retrieve the underlying non-nullable type
                        var underlyingType = Nullable.GetUnderlyingType(property.Type);

                        // Create an expression to convert the nullable property to the non-nullable type
                        var convertedProperty = Expression.Convert(property,
                            underlyingType ?? throw new InvalidOperationException());
                        comparison = Expression.LessThan(convertedProperty, constant);
                    }
                    else
                    {
                        comparison = Expression.LessThan(property, constant);
                    }

                    break;

                case QueryOperator.LessThanOrEqual:
                    if (IsNullableType(property.Type))
                    {
                        // Retrieve the underlying non-nullable type
                        var underlyingType = Nullable.GetUnderlyingType(property.Type);

                        // Create an expression to convert the nullable property to the non-nullable type
                        var convertedProperty = Expression.Convert(property,
                            underlyingType ?? throw new InvalidOperationException());
                        comparison = Expression.LessThanOrEqual(convertedProperty, constant);
                    }
                    else
                    {
                        comparison = Expression.LessThanOrEqual(property, constant);
                    }

                    break;

                case QueryOperator.Contains:
                    comparison = Expression.Call(property,
                        typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!, constant);
                    break;

                case QueryOperator.StartsWith:
                    comparison = Expression.Call(property,
                        typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!, constant);
                    break;

                case QueryOperator.EndsWith:
                    comparison = Expression.Call(property,
                        typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!, constant);
                    break;

                case QueryOperator.FreeText:
                    var method = typeof(SqlServerDbFunctionsExtensions).GetMethod("FreeText",
                        new[] { typeof(DbFunctions), typeof(string), typeof(string) });
                    comparison = Expression.Call(null, method, Expression.Constant(EF.Functions), property, constant);
                    break;

                default:
                    throw new ArgumentException("Invalid operator specified: " + condition.Operator);
            }

            expression = expression == null ? comparison : Expression.AndAlso(expression, comparison);
        }

        if (expression == null) return source;

        var lambda = Expression.Lambda<Func<T, bool>>(expression, parameter);
        source = source.Where(lambda);

        return source;
    }

    private static bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    public static IQueryable<T> IsInList<T>(this IQueryable<T> source,
        Expression<Func<T, IEnumerable<string>>> stringListSelector, List<string> ids)
    {
        var parameter = stringListSelector.Parameters.Single();
        var memberExpression = stringListSelector.Body as MemberExpression;
        if (memberExpression == null || !(memberExpression.Member is PropertyInfo))
            throw new ArgumentException("Selector must be a property selector.");
        var containsMethod = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == "Contains" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(string));

        foreach (var id in ids)
        {
            var constant = Expression.Constant(id, typeof(string));
            var body = Expression.Call(containsMethod, stringListSelector.Body, constant);
            var lambda = Expression.Lambda<Func<T, bool>>(body, parameter);
            source = source.Where(lambda);
        }

        return source;
    }


    private static IEnumerable<TItem> ParseItems<TItem>(IEnumerable<string> items)
    {
        return items.Select(i => (TItem)Convert.ChangeType(i, typeof(TItem)));
    }

    public static async Task<PagedResult<T>> ApplyPagingAsync<T>(this IQueryable<T> query, int pageNumber, int pageSize,
        Expression<Func<T, object?>> orderingExpression)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        if (pageNumber < 1) pageNumber = 1;

        if (pageSize < 1) pageSize = QueryConstants.DefaultPageSize;

        if (pageSize > QueryConstants.MaxPageSize) pageSize = QueryConstants.MaxPageSize;


        query = query.OrderByDescending(orderingExpression);


        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        if (pageNumber > totalPages) pageNumber = totalPages;

        if (pageNumber < 1) pageNumber = 1;

        var results = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>(pageNumber, pageSize, totalCount, results);
    }
}