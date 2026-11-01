using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Parsing;
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService : AccountServiceBase
{
    private readonly FileService _fileService;
    private readonly ImportSourceService _importSourceService;
    private readonly StorageCleanupService _storageCleanup;
    private readonly TagRepository _tagRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly NotificationService _notificationService;
    private readonly AccountRepository _accountRepository;
    private readonly CaveRepository _repository;
    private readonly FileRepository _fileRepository;
    private readonly ChunkedUploadService _chunkedUploadService;
    private readonly CaveImportCsvParser _caveParser;
    private readonly EntranceImportCsvParser _entranceParser;
    private readonly CaveImportPlanningRepository _cavePlanningRepository;
    private readonly EntranceImportPlanningRepository _entrancePlanningRepository;
    private readonly CaveImportPlanner _cavePlanner;
    private readonly EntranceImportPlanner _entrancePlanner;
    private readonly CaveImportExecutionRepository _caveExecutor;
    private readonly EntranceImportExecutionRepository _entranceExecutor;

    public ImportService(RequestUser requestUser, FileService fileService,
        ImportSourceService importSourceService, StorageCleanupService storageCleanup,
        TagRepository tagRepository,
        SettingsRepository settingsRepository,
        NotificationService notificationService,
        AccountRepository accountRepository,
        CaveRepository caveRepository,
        FileRepository fileRepository,
        ChunkedUploadService chunkedUploadService, CaveImportCsvParser caveParser,
        EntranceImportCsvParser entranceParser, CaveImportPlanningRepository cavePlanningRepository,
        EntranceImportPlanningRepository entrancePlanningRepository, CaveImportPlanner cavePlanner,
        EntranceImportPlanner entrancePlanner, CaveImportExecutionRepository caveExecutor,
        EntranceImportExecutionRepository entranceExecutor) : base(requestUser)
    {
        _fileService = fileService;
        _importSourceService = importSourceService;
        _storageCleanup = storageCleanup;
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
        _notificationService = notificationService;
        _accountRepository = accountRepository;
        _repository = caveRepository;
        _fileRepository = fileRepository;
        _chunkedUploadService = chunkedUploadService;
        _caveParser = caveParser;
        _entranceParser = entranceParser;
        _cavePlanningRepository = cavePlanningRepository;
        _entrancePlanningRepository = entrancePlanningRepository;
        _cavePlanner = cavePlanner;
        _entrancePlanner = entrancePlanner;
        _caveExecutor = caveExecutor;
        _entranceExecutor = entranceExecutor;
    }

    public Task<(Stream Stream, string FileName)> OpenCsvImportSourceDownloadAsync(
        string sourceId, CancellationToken cancellationToken) =>
        _importSourceService.OpenDownloadAsync(sourceId, cancellationToken);

    public Task<ImportSourceUploadVm> UploadCsvImportSourceAsync(
        Stream stream, string fileName, string? uuid, CancellationToken cancellationToken) =>
        _importSourceService.UploadAsync(stream, fileName, uuid, cancellationToken);

}
