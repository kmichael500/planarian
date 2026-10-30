using Planarian.Operations;

if (args.Length != 1 || !OperationsCatalog.TryCreate(args[0], out var operation))
{
    Console.Error.WriteLine(
        $"Usage: dotnet Planarian.Operations.dll <operation>{Environment.NewLine}" +
        $"Available operations: {string.Join(", ", OperationsCatalog.Names)}");
    return 2;
}

return await operation.ExecuteAsync();
