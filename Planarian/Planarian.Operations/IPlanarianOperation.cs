namespace Planarian.Operations;

internal interface IPlanarianOperation
{
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}
