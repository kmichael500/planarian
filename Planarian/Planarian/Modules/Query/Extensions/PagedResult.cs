namespace Planarian.Modules.Query.Extensions;

public class PagedResult<T>
{
    public PagedResult(int pageNumber, int pageSize, int totalCount, IList<T> results)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        Results = results;
    }

    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<T> Results { get; set; }
}