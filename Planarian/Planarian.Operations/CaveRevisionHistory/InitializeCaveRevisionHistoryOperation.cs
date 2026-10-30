namespace Planarian.Operations.CaveRevisionHistory;

internal sealed class InitializeCaveRevisionHistoryOperation : IPlanarianOperation
{
    public const string Name = "initialize-cave-revision-history";
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__default";

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} is required to run this operation.");

        var result = await CaveRevisionRolloutRepository.InitializeAsync(connectionString, cancellationToken);

        Console.WriteLine(result.AlreadyComplete
            ? $"Cave revision history is already initialized. Boundary: {result.RevisionHistoryStartedOn:O}"
            : $"Initialized revision history for {result.InitializedCaveCount} Cave(s). Boundary: {result.RevisionHistoryStartedOn:O}");

        return 0;
    }
}
