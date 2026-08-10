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
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService : ServiceBase
{
    private readonly FileService _fileService;
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

    public async Task<FileVm> AddTemporaryFileForImport(Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.AddTemporaryAccountFile(stream, fileName,
            FileTypeTagName.Other, cancellationToken, uuid);

        return result;
    }

}
