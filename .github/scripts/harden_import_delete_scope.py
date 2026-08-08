from pathlib import Path


def replace_all_exact(text: str, old: str, new: str, expected: int, label: str) -> str:
    count = text.count(old)
    if count != expected:
        raise SystemExit(f"{label}: expected {expected} matches, found {count}")
    return text.replace(old, new)

cave_path = Path("Planarian/Planarian/Modules/Import/Planning/CaveImportExecutor.cs")
cave = cave_path.read_text()

# Cave association replacement deletes (9 roles) and sync-deletion Cave tag deletes
# must each restate tenant ownership through the Cave relationship.
for set_name in [
    "GeologyTags", "GeologicAgeTags", "MapStatusTags", "PhysiographicProvinceTags",
    "ArcheologyTags", "BiologyTags", "CaveOtherTags", "CartographerNameTags",
    "CaveReportedByNameTags"
]:
    old = f"_db.{set_name}.Where(t => chunk.Contains(t.CaveId)).ExecuteDeleteAsync(cancellationToken)"
    new = f"_db.{set_name}.Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken)"
    cave = replace_all_exact(cave, old, new, 1, f"{set_name} replacement delete")

for set_name in [
    "GeologyTags", "GeologicAgeTags", "MapStatusTags", "PhysiographicProvinceTags",
    "ArcheologyTags", "BiologyTags", "CaveOtherTags", "CartographerNameTags",
    "CaveReportedByNameTags"
]:
    old = f"_db.{set_name}.Where(t => scopedIds.Contains(t.CaveId)).ExecuteDeleteAsync(cancellationToken)"
    new = f"_db.{set_name}.Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken)"
    cave = replace_all_exact(cave, old, new, 1, f"{set_name} sync delete")

for set_name in [
    "EntranceStatusTags", "EntranceHydrologyTags", "FieldIndicationTags",
    "EntranceReportedByNameTags", "EntranceOtherTag"
]:
    old = f"_db.{set_name}.Where(t => entranceChunk.Contains(t.EntranceId)).ExecuteDeleteAsync(cancellationToken)"
    new = f"_db.{set_name}.Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken)"
    cave = replace_all_exact(cave, old, new, 1, f"{set_name} sync delete")

cave = replace_all_exact(
    cave,
    "_db.CaveGeoJsons.Where(g => scopedIds.Contains(g.CaveId)).ExecuteDeleteAsync(cancellationToken)",
    "_db.CaveGeoJsons.Where(g => scopedIds.Contains(g.CaveId) && g.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken)",
    1,
    "CaveGeoJson sync delete")

needle = "            await _db.Favorites.Where(f => f.AccountId == _scope.AccountId && scopedIds.Contains(f.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.Files.Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);"
replacement = "            await _db.Favorites.Where(f => f.AccountId == _scope.AccountId && scopedIds.Contains(f.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.CavePermissions.Where(p => p.AccountId == _scope.AccountId && p.CaveId != null && scopedIds.Contains(p.CaveId)).ExecuteDeleteAsync(cancellationToken);\n            await _db.Files.Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);"
cave = replace_all_exact(cave, needle, replacement, 1, "CavePermission sync delete")
cave_path.write_text(cave)

entrance_path = Path("Planarian/Planarian/Modules/Import/Planning/EntranceImportExecutor.cs")
entrance = entrance_path.read_text()
for set_name in [
    "EntranceStatusTags", "EntranceHydrologyTags", "FieldIndicationTags",
    "EntranceReportedByNameTags", "EntranceOtherTag"
]:
    old = f"_db.{set_name}.Where(t => entranceChunk.Contains(t.EntranceId)).ExecuteDeleteAsync(cancellationToken)"
    new = f"_db.{set_name}.Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken)"
    entrance = replace_all_exact(entrance, old, new, 1, f"{set_name} entrance import delete")
entrance_path.write_text(entrance)
