using Planarian.Operations.CaveRevisionHistory;

namespace Planarian.Operations;

internal static class OperationsCatalog
{
    private static readonly IReadOnlyDictionary<string, Func<IPlanarianOperation>> Factories =
        new Dictionary<string, Func<IPlanarianOperation>>(StringComparer.Ordinal)
        {
            [InitializeCaveRevisionHistoryOperation.Name] = static () => new InitializeCaveRevisionHistoryOperation()
        };

    public static IEnumerable<string> Names => Factories.Keys.Order(StringComparer.Ordinal);

    public static bool TryCreate(string name, out IPlanarianOperation operation)
    {
        if (Factories.TryGetValue(name, out var factory))
        {
            operation = factory();
            return true;
        }

        operation = null!;
        return false;
    }
}
