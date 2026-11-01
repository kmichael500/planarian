using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Files.Models;

public sealed class EditFileMetadataVm
{
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [Required, MaxLength(PropertyLength.FileName)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileTypeTagId { get; set; } = null!;
}
