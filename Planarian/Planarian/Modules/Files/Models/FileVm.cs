using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Files.Models;

public sealed class FileVm
{
    [MaxLength(PropertyLength.FileName)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.FileName)] public string Extension { get; set; } = string.Empty;
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileTypeTagId { get; set; } = null!;

    // Historical API name: this contains the File TagType display name, not TagType.Key.
    [MaxLength(PropertyLength.Name)] public string FileTypeKey { get; set; } = null!;
    public string? Uuid { get; set; }
}
