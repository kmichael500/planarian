using CsvHelper;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Repositories;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService : ServiceBase
{
    private readonly PlanarianDbContext _dbContext;
    private readonly FileService _fileService;
    private readonly TagRepository<PlanarianDbContextBase> _tagRepository;
    private readonly SettingsRepository<PlanarianDbContextBase> _settingsRepository;
    private readonly EntranceImportPlanStore _entranceImportPlanStore;
    private readonly NotificationService _notificationService;
    private readonly AccountRepository<PlanarianDbContextBase> _accountRepository;
    private readonly CaveRepository<PlanarianDbContextBase> _repository;
    private readonly FileRepository<PlanarianDbContextBase> _fileRepository;
    private readonly ChunkedUploadService _chunkedUploadService;

    public ImportService(RequestUser requestUser, PlanarianDbContext dbContext, FileService fileService,
        TagRepository<PlanarianDbContextBase> tagRepository,
        SettingsRepository<PlanarianDbContextBase> settingsRepository,
        EntranceImportPlanStore entranceImportPlanStore,
        NotificationService notificationService,
        AccountRepository<PlanarianDbContextBase> accountRepository,
        CaveRepository<PlanarianDbContextBase> caveRepository,
        FileRepository<PlanarianDbContextBase> fileRepository,
        ChunkedUploadService chunkedUploadService) : base(requestUser)
    {
        _dbContext = dbContext;
        _fileService = fileService;
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
        _entranceImportPlanStore = entranceImportPlanStore;
        _notificationService = notificationService;
        _accountRepository = accountRepository;
        _repository = caveRepository;
        _fileRepository = fileRepository;
        _chunkedUploadService = chunkedUploadService;
    }

    public async Task<FileVm> AddTemporaryFileForImport(Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.AddTemporaryAccountFile(stream, fileName,
            FileTypeTagName.Other, cancellationToken, uuid);

        return result;
    }

    private bool TryGetFieldValue<T>(IReaderRow csv, string fieldName, bool isRequired, List<string> errors,
        out T? fieldValue)
    {
        var hasValue = csv.TryGetField(fieldName, out fieldValue);

        if (!hasValue || (typeof(T) == typeof(string) && string.IsNullOrWhiteSpace(fieldValue?.ToString())))
        {
            if (isRequired) errors.Add($"{fieldName} is required.");

            return false;
        }

        if (typeof(T) == typeof(string) && fieldValue != null) fieldValue = (T)(object)fieldValue.ToString()?.Trim()!;

        return true;
    }
}
