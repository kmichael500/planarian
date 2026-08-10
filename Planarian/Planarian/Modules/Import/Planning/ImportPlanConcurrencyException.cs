namespace Planarian.Modules.Import.Planning;

/// <summary>
/// Raised when persisted data no longer matches the immutable state captured
/// by an import plan and the caller must re-plan before committing.
/// </summary>
public sealed class ImportPlanConcurrencyException : InvalidOperationException
{
    public ImportPlanConcurrencyException(string message) : base(message)
    {
    }

    public ImportPlanConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
