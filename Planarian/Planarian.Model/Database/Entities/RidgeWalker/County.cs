using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class County : EntityBaseNameId
{
    [MaxLength(PropertyLength.SmallText)] public string DisplayId { get; set; } = null!;
    
    public ICollection<Cave> Caves { get; set; } = null!;
}

public class CountyConfiguration : BaseEntityTypeConfiguration<County>
{
    public override void Configure(EntityTypeBuilder<County> builder)
    {
    }
}