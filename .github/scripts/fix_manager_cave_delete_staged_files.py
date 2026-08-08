from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)

repo_path = Path("Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs")
repo = repo_path.read_text()
needle = '''            .ThenInclude(entrance => entrance.EntranceReportedByNameTags)
            .FirstOrDefaultAsync();
    }

    public async Task<Cave?> GetCaveWithLinePlots(string caveId)'''
replacement = '''            .ThenInclude(entrance => entrance.EntranceReportedByNameTags)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteStagedFileReferencesAsync(IEnumerable<string> fileIds,
        CancellationToken cancellationToken = default)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return;
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId))
            throw new InvalidOperationException("An active account is required to delete staged file references.");

        await DbContext.Set<CaveChangeRequestStagedFile>()
            .IgnoreQueryFilters()
            .Where(staged => staged.AccountId == RequestUser.AccountId && ids.Contains(staged.FileId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Cave?> GetCaveWithLinePlots(string caveId)'''
repo = replace_once(repo, needle, replacement, "repository staged-file cleanup")
repo_path.write_text(repo)

service_path = Path("Planarian/Planarian/Modules/Caves/Services/CaveService.cs")
service = service_path.read_text()
needle = '''            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(file);
            }

            await Repository.SaveChangesAsync(cancellationToken);'''
replacement = '''            await Repository.DeleteStagedFileReferencesAsync(files.Select(file => file.Id), cancellationToken);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(file);
            }

            await Repository.SaveChangesAsync(cancellationToken);'''
service = replace_once(service, needle, replacement, "service staged-file cleanup")
service_path.write_text(service)
