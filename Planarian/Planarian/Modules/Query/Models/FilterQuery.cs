using Planarian.Modules.Query.Constants;

namespace Planarian.Modules.Query.Models;

public class FilterQuery
{
    public IEnumerable<QueryCondition> Conditions { get; set; } = new List<QueryCondition>()!;
    public int PageSize { get; set; } = QueryConstants.DefaultPageSize;
    public int PageNumber { get; set; } = 1;
    public string SortBy { get; set; } = QueryConstants.DefaultSortBy;
    public bool SortDescending { get; set; } = true;
}