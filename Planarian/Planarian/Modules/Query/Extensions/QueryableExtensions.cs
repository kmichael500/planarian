using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Query.Models;

namespace Planarian.Modules.Query.Extensions;

public static class  QueryableExtensions
{
    public static async Task<PagedResult<T>> ApplyPagingAsync<T>(this IQueryable<T> query, int pageNumber, int pageSize,
        Expression<Func<T, object?>>? orderingExpression = null, bool sortDescending = true)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        if (pageNumber < 1) pageNumber = 1;

        if (pageSize < 1) pageSize = QueryConstants.DefaultPageSize;

        if (pageSize > QueryConstants.MaxPageSize) pageSize = QueryConstants.MaxPageSize;

        if (orderingExpression != null)
        {
            query = sortDescending
                ? query.OrderByDescendingNullLast(orderingExpression)
                : query.OrderBy(orderingExpression);
        }

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
    
    public static IOrderedQueryable<T> OrderByDescendingNullLast<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        var param = keySelector.Parameters[0];
        var body = keySelector.Body;

        var isNull = Expression.Equal(body, Expression.Constant(null, typeof(TKey)));
        var notNull = Expression.NotEqual(body, Expression.Constant(null, typeof(TKey)));

        var nullValuesLast = Expression.Condition(
            isNull,
            Expression.Constant(int.MaxValue, typeof(int)),
            Expression.Constant(0, typeof(int))
        );

        var selector = Expression.Lambda<Func<T, int>>(nullValuesLast, param);

        return source
            .OrderBy(selector)
            .ThenByDescending(keySelector);
    }
}
