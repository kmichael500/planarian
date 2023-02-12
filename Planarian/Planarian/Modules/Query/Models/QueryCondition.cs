namespace Planarian.Modules.Leads.Controllers;

public class QueryCondition
{
    public QueryCondition(string field, string @operator, dynamic value)
    {
        Field = field;
        Operator = @operator;
        Value = value;
    }

    public QueryCondition()
    {
    }

    public string Field { get; set; }
    public string Operator { get; set; }
    public string Value { get; set; }
}

public class FilterQuery
{
    public IEnumerable<QueryCondition> Conditions { get; set; } = new List<QueryCondition>()!;
    public int PageSize { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
}