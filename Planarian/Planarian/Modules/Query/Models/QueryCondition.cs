using Planarian.Library.Extensions.String;

namespace Planarian.Modules.Query.Models;

public class QueryCondition
{
    private string _field;

    public QueryCondition(string field, string @operator, dynamic value)
    {
        Field = field;
        Operator = @operator;
        Value = value;
    }

    public QueryCondition()
    {
    }

    public string Field
    {
        get => _field.FirstCharToUpper();
        set => _field = value;
    }

    public string Operator { get; set; }
    public string Value { get; set; }
}