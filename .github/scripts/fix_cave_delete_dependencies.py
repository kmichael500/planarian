from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)

repo_path = Path("Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs")
repo = repo_path.read_text()
repo = replace_once(
    repo,
    "            .Include(e => e.Favorites)\n            .Include(e => e.Files)",
    "            .Include(e => e.Favorites)\n            .Include(e => e.CavePermissions)\n            .Include(e => e.Files)",
    "CaveRepository GetAsync permissions include")
repo_path.write_text(repo)

service_path = Path("Planarian/Planarian/Modules/Caves/Services/CaveService.cs")
service = service_path.read_text()
service = replace_once(
    service,
    "            files = entity.Files.ToList();\n\n            Repository.Delete(entity);\n            await Repository.SaveChangesAsync(cancellationToken);",
    "            files = entity.Files.ToList();\n\n            foreach (var permission in entity.CavePermissions)\n            {\n                cancellationToken.ThrowIfCancellationRequested();\n                Repository.Delete(permission);\n            }\n\n            foreach (var file in files)\n            {\n                cancellationToken.ThrowIfCancellationRequested();\n                Repository.Delete(file);\n            }\n\n            await Repository.SaveChangesAsync(cancellationToken);\n\n            Repository.Delete(entity);\n            await Repository.SaveChangesAsync(cancellationToken);",
    "CaveService delete dependent rows")
service_path.write_text(service)
