using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class County : EntityBaseNameId
{
    public string DispayId { get; set; }
    
    public ICollection<Cave> Caves { get; set; } = null!;
}

public class CountyConfiguration : BaseEntityTypeConfiguration<County>
{
    public override void Configure(EntityTypeBuilder<County> builder)
    {
    }
}