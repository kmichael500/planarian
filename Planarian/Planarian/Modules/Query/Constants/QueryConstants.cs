using Planarian.Modules.Caves.Models;

namespace Planarian.Modules.Query.Constants;

public static class QueryConstants
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public static string DefaultSortBy = nameof(CaveSearchVm.LengthFeet);
}