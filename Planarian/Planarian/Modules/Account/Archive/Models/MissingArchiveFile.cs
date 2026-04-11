namespace Planarian.Modules.Account.Archive.Models;

public sealed record MissingArchiveFile(
    string CaveDisplayId,
    string CaveName,
    string EntryPath,
    string BlobKey,
    string Reason);
