namespace Planarian.Modules.Account.Model;

/// <summary>
/// The database purge is authoritative even when post-commit object storage cleanup fails.
/// </summary>
public sealed record AccountResetResult(bool RelationalResetCompleted, bool StorageCleanupCompleted);
