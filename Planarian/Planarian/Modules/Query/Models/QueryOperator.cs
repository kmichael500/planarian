namespace Planarian.Modules.Leads.Controllers;

public static class QueryOperator
{
    public const string Equal = "=";
    public const string NotEqual = "!=";
    public const string LessThan = "<";
    public const string GreaterThan = ">";
    public const string GreaterThanOrEqual = ">=";
    public const string LessThanOrEqual = "<=";
    public const string Contains = "=*";
    public const string NotContains = "!*";
    public const string StartsWith = "^";
    public const string NotStartsWith = "!^";
    public const string EndsWith = "$";
    public const string NotEndsWith = "!$";
    public const string FreeText = "*=";
}