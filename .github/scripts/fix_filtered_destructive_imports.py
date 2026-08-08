from pathlib import Path


def exact(text: str, old: str, new: str, expected: int, label: str) -> str:
    count = text.count(old)
    if count != expected:
        raise SystemExit(f"{label}: expected {expected} matches, found {count}")
    return text.replace(old, new)

cave_path = Path("Planarian/Planarian/Modules/Import/Planning/CaveImportExecutor.cs")
cave = cave_path.read_text()

for set_name in [
    "GeologyTags", "GeologicAgeTags", "MapStatusTags", "PhysiographicProvinceTags",
    "ArcheologyTags", "BiologyTags", "CaveOtherTags", "CartographerNameTags",
    "CaveReportedByNameTags"
]:
    cave = exact(cave, f"_db.{set_name}.Where(", f"_db.{set_name}.IgnoreQueryFilters().Where(", 2,
                 f"{set_name} destructive queries")

for set_name in [
    "EntranceStatusTags", "EntranceHydrologyTags", "FieldIndicationTags",
    "EntranceReportedByNameTags", "EntranceOtherTag"
]:
    cave = exact(cave, f"_db.{set_name}.Where(", f"_db.{set_name}.IgnoreQueryFilters().Where(", 1,
                 f"{set_name} cave-sync query")

cave = exact(cave,
    "var files = await _db.Files.Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId)\n                .AsNoTracking().Select(f => new { f.BlobKey, f.BlobContainer }).ToListAsync(cancellationToken);",
    "var files = await _db.Files.IgnoreQueryFilters().Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId)\n                .AsNoTracking().Select(f => new { f.Id, f.BlobKey, f.BlobContainer }).ToListAsync(cancellationToken);",
    1, "Cave sync file lookup")

cave = exact(cave,
    "            await _db.Favorites.Where(f => f.AccountId == _scope.AccountId && scopedIds.Contains(f.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.CavePermissions.Where(p => p.AccountId == _scope.AccountId && p.CaveId != null && scopedIds.Contains(p.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.Files.Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);",
    "            await _db.Favorites.IgnoreQueryFilters().Where(f => f.AccountId == _scope.AccountId && scopedIds.Contains(f.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.CavePermissions.IgnoreQueryFilters().Where(p => p.AccountId == _scope.AccountId && p.CaveId != null && scopedIds.Contains(p.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            var fileIds = files.Select(f => f.Id).ToList();\n            if (fileIds.Count > 0)\n            {\n                await _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()\n                    .Where(sf => sf.AccountId == _scope.AccountId && fileIds.Contains(sf.FileId))\n                    .ExecuteDeleteAsync(cancellationToken);\n            }\n            await _db.Files.IgnoreQueryFilters().Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);",
    1, "Cave sync dependent cleanup")

cave = exact(cave, "_db.CaveGeoJsons.Where(", "_db.CaveGeoJsons.IgnoreQueryFilters().Where(", 1,
             "CaveGeoJson destructive query")
cave_path.write_text(cave)

entrance_path = Path("Planarian/Planarian/Modules/Import/Planning/EntranceImportExecutor.cs")
entrance = entrance_path.read_text()
for set_name in [
    "EntranceStatusTags", "EntranceHydrologyTags", "FieldIndicationTags",
    "EntranceReportedByNameTags", "EntranceOtherTag"
]:
    entrance = exact(entrance, f"_db.{set_name}.Where(", f"_db.{set_name}.IgnoreQueryFilters().Where(", 1,
                     f"{set_name} entrance-sync query")
entrance_path.write_text(entrance)
