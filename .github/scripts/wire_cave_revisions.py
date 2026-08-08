from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


cave_path = Path("Planarian/Planarian/Modules/Caves/Services/CaveService.cs")
cave = cave_path.read_text()
cave = replace_once(
    cave,
    "using Planarian.Model.Database.Entities.RidgeWalker;\nusing Planarian.Model.Shared;",
    "using Planarian.Model.Database.Entities.RidgeWalker;\nusing Planarian.Model.Database.Revisions;\nusing Planarian.Model.Shared;",
    "CaveService revision using")
cave = replace_once(
    cave,
    "using Planarian.Modules.Caves.Repositories;\nusing Planarian.Modules.Files.Repositories;",
    "using Planarian.Modules.Caves.Repositories;\nusing Planarian.Modules.Caves.Revisions;\nusing Planarian.Modules.Files.Repositories;",
    "CaveService coordinator using")
cave = replace_once(
    cave,
    "    private readonly ClientUrlBuilder _clientUrlBuilder;\n",
    "    private readonly ClientUrlBuilder _clientUrlBuilder;\n    private readonly CaveMutationCoordinator _caveMutationCoordinator;\n",
    "CaveService field")
cave = replace_once(
    cave,
    "        TagRepository tagRepository,\n        FeatureSettingRepository featureSettingRepository, ClientUrlBuilder clientUrlBuilder) : base(\n        repository, requestUser)",
    "        TagRepository tagRepository,\n        FeatureSettingRepository featureSettingRepository, ClientUrlBuilder clientUrlBuilder,\n        CaveMutationCoordinator caveMutationCoordinator) : base(\n        repository, requestUser)",
    "CaveService constructor signature")
cave = replace_once(
    cave,
    "        _clientUrlBuilder = clientUrlBuilder;\n",
    "        _clientUrlBuilder = clientUrlBuilder;\n        _caveMutationCoordinator = caveMutationCoordinator;\n",
    "CaveService constructor assignment")
cave = replace_once(
    cave,
    "            var entity = isNew ? new Cave() : await Repository.GetAsync(values.Id);\n\n            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));\n\n            var isNewCounty = entity.CountyId != values.CountyId;",
    "            var entity = isNew ? new Cave() : await Repository.GetAsync(values.Id);\n\n            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));\n\n            CaveMutationPreparation? revisionPreparation = null;\n            if (!isNew)\n            {\n                revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(\n                    entity.Id, entity.CurrentRevisionId, cancellationToken);\n            }\n\n            var isNewCounty = entity.CountyId != values.CountyId;",
    "CaveService edit preparation")
cave = replace_once(
    cave,
    "            await Repository.SaveChangesAsync(cancellationToken);\n\n            await transaction.CommitAsync(cancellationToken);\n\n            foreach (var blobProperties in blobsToDelete)",
    "            await Repository.SaveChangesAsync(cancellationToken);\n\n            if (isNew)\n            {\n                await _caveMutationCoordinator.PublishPersistedNewAsync(\n                    entity.Id, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Create,\n                    cancellationToken: cancellationToken);\n            }\n            else\n            {\n                await _caveMutationCoordinator.PublishPreparedAsync(\n                    revisionPreparation!, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,\n                    cancellationToken: cancellationToken);\n            }\n\n            await transaction.CommitAsync(cancellationToken);\n\n            foreach (var blobProperties in blobsToDelete)",
    "CaveService edit publication")
cave = replace_once(
    cave,
    "            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId, entity.StateId);\n\n            var geoJsons = await Repository.GetCaveGeoJsonsAsync(caveId);",
    "            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId, entity.StateId);\n\n            var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(\n                entity.Id, entity.CurrentRevisionId, cancellationToken);\n\n            var geoJsons = await Repository.GetCaveGeoJsonsAsync(caveId);",
    "CaveService delete preparation")
cave = replace_once(
    cave,
    "            Repository.Delete(entity);\n            await Repository.SaveChangesAsync(cancellationToken);\n\n            if (!outsideTransaction)",
    "            Repository.Delete(entity);\n            await Repository.SaveChangesAsync(cancellationToken);\n            await _caveMutationCoordinator.PublishPreparedDeleteAsync(\n                revisionPreparation, CaveRevisionSource.ManagerEdit, cancellationToken: cancellationToken);\n\n            if (!outsideTransaction)",
    "CaveService delete publication")
cave = replace_once(
    cave,
    "        entity.IsArchived = true;\n        await Repository.SaveChangesAsync();",
    "        await _caveMutationCoordinator.PublishExistingAsync(\n            caveId, entity.CurrentRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Archive,\n            cave => cave.IsArchived = true);",
    "CaveService archive")
cave = replace_once(
    cave,
    "        entity.IsArchived = false;\n        await Repository.SaveChangesAsync();",
    "        await _caveMutationCoordinator.PublishExistingAsync(\n            caveId, entity.CurrentRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Unarchive,\n            cave => cave.IsArchived = false);",
    "CaveService unarchive")
cave_path.write_text(cave)

file_path = Path("Planarian/Planarian/Modules/Files/Services/FileService.cs")
files = file_path.read_text()
files = replace_once(
    files,
    "using Planarian.Modules.Caves.Repositories;\nusing Planarian.Modules.Files.Controllers;",
    "using Planarian.Modules.Caves.Repositories;\nusing Planarian.Modules.Caves.Revisions;\nusing Planarian.Modules.Files.Controllers;",
    "FileService coordinator using")
files = replace_once(
    files,
    "    private readonly CaveRepository _caveRepository;\n",
    "    private readonly CaveRepository _caveRepository;\n    private readonly CaveMutationCoordinator _caveMutationCoordinator;\n",
    "FileService field")
files = replace_once(
    files,
    "        FileOptions fileOptions, SettingsRepository settingsRepository, CaveRepository caveRepository,\n        RequestThrottleService requestThrottleService) : base(\n        repository, requestUser)",
    "        FileOptions fileOptions, SettingsRepository settingsRepository, CaveRepository caveRepository,\n        RequestThrottleService requestThrottleService, CaveMutationCoordinator caveMutationCoordinator) : base(\n        repository, requestUser)",
    "FileService constructor signature")
files = replace_once(
    files,
    "        _caveRepository = caveRepository;\n        _requestThrottleService = requestThrottleService;",
    "        _caveRepository = caveRepository;\n        _requestThrottleService = requestThrottleService;\n        _caveMutationCoordinator = caveMutationCoordinator;",
    "FileService constructor assignment")

permission = "        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);\n        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);"
permission_with_prepare = "        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);\n        var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(\n            caveId, cancellationToken: cancellationToken);\n        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);"
files = replace_once(files, permission, permission_with_prepare, "FileService upload preparation")

publish = "        entity.BlobKey = blobKey;\n        entity.BlobContainer = RequestUser.AccountContainerName;\n        await Repository.SaveChangesAsync(cancellationToken);\n        await transaction.CommitAsync(cancellationToken);"
publish_new = "        entity.BlobKey = blobKey;\n        entity.BlobContainer = RequestUser.AccountContainerName;\n        await Repository.SaveChangesAsync(cancellationToken);\n        await _caveMutationCoordinator.PublishPreparedAsync(\n            revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,\n            cancellationToken: cancellationToken);\n        await transaction.CommitAsync(cancellationToken);"
if files.count(publish) != 2:
    raise SystemExit(f"FileService upload/staged publication: expected two matches, found {files.count(publish)}")
files = files.replace(publish, publish_new, 1)

staged_permission = "        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId,  caveEntity.StateId);\n        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);"
staged_permission_new = "        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId,  caveEntity.StateId);\n        var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(\n            caveId, cancellationToken: cancellationToken);\n        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);"
files = replace_once(files, staged_permission, staged_permission_new, "FileService staged preparation")
files = replace_once(files, publish, publish_new, "FileService staged publication")

old_metadata = '''    public async Task UpdateFilesMetadata(IEnumerable<EditFileMetadataVm> values, CancellationToken cancellationToken)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        foreach (var value in values)
        {
            await EnsureFileManagerAccess(value.Id);

            var file = await Repository.GetFileById(value.Id);
            if (file == null) throw ApiExceptionDictionary.NotFound("File");

            if (!string.IsNullOrWhiteSpace(value.DisplayName))
            {
                file.DisplayName = value.DisplayName;
                file.FileName = $"{value.DisplayName}{Path.GetExtension(file.FileName)}";
            }

            file.FileTypeTagId = value.FileTypeTagId;

            await Repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }'''
new_metadata = '''    public async Task UpdateFilesMetadata(IEnumerable<EditFileMetadataVm> values, CancellationToken cancellationToken)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        var revisionPreparations = new Dictionary<string, CaveMutationPreparation>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            await EnsureFileManagerAccess(value.Id);

            var file = await Repository.GetFileById(value.Id);
            if (file == null) throw ApiExceptionDictionary.NotFound("File");

            if (!string.IsNullOrWhiteSpace(file.CaveId) && !revisionPreparations.ContainsKey(file.CaveId))
            {
                revisionPreparations[file.CaveId] = await _caveMutationCoordinator.PrepareExistingAsync(
                    file.CaveId, cancellationToken: cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(value.DisplayName))
            {
                file.DisplayName = value.DisplayName;
                file.FileName = $"{value.DisplayName}{Path.GetExtension(file.FileName)}";
            }

            file.FileTypeTagId = value.FileTypeTagId;

            await Repository.SaveChangesAsync(cancellationToken);
        }

        foreach (var preparation in revisionPreparations.Values)
        {
            await _caveMutationCoordinator.PublishPreparedAsync(
                preparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cancellationToken: cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }'''
files = replace_once(files, old_metadata, new_metadata, "FileService metadata publication")
file_path.write_text(files)
