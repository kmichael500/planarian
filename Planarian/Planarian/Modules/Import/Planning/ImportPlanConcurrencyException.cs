namespace Planarian.Modules.Import.Planning;

/// <summary>Raised when persisted state no longer matches an import plan and the caller must re-plan.</summary>
public sealed class ImportPlanConcurrencyException : InvalidOperationException
{
    public ImportPlanConcurrencyException(string message) : base(message)
    {
    }

    public ImportPlanConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
