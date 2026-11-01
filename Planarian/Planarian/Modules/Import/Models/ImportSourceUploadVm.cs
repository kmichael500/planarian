using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Import.Models;

public sealed class ImportSourceUploadVm
{
    [MaxLength(PropertyLength.Id)] public string Id { get; init; } = null!;
    [MaxLength(PropertyLength.FileName)] public string FileName { get; init; } = null!;
    public string? Uuid { get; init; }
}
