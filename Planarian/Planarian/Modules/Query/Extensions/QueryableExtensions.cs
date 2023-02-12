using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Planarian.Modules.Leads.Controllers;

public static class QueryableExtensions
{
    public static IQueryable<T> QueryFilter<T>(this IQueryable<T> source, IEnumerable<QueryCondition> conditions)
    {
        conditions = conditions.ToList();
        if (!conditions.Any()) return source;
        
        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? expression = null;

        foreach (var condition in conditions)
        {
            var property = Expression.Property(parameter, condition.Field);
            var constant = Expression.Constant(Convert.ChangeType(condition.Value, property.Type));

            Expression? comparison = null;
            switch (condition.Operator)
            {
                case QueryOperator.Equal:
                    comparison = Expression.Equal(property, constant);
                    break;
                case QueryOperator.NotEqual:
                    comparison = Expression.NotEqual(property, constant);
                    break;
                case QueryOperator.GreaterThan:
                    comparison = Expression.GreaterThan(property, constant);
                    break;
                case QueryOperator.GreaterThanOrEqual:
                    comparison = Expression.GreaterThanOrEqual(property, constant);
                    break;
                case QueryOperator.LessThan:
                    comparison = Expression.LessThan(property, constant);
                    break;
                case QueryOperator.LessThanOrEqual:
                    comparison = Expression.LessThanOrEqual(property, constant);
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
                    comparison = Expression.Call(
                        null, 
                        method, 
                        Expression.Constant(EF.Functions), 
                        property, 
                        constant
                    );
                    break;

                default:
                    throw new ArgumentException("Invalid operator specified: " + condition.Operator);
            }

            expression = expression == null ? comparison : Expression.AndAlso(expression, comparison);
        }

        if (expression == null) return source;

        var methodName = expression.ToString();
        var lambda = Expression.Lambda<Func<T, bool>>(expression, parameter);

        source = source.Where(lambda);

        return source;
    }
}