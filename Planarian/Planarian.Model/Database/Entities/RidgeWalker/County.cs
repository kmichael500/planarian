using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.RidgeWalker.Views;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class County : EntityBaseNameId
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;
    [MaxLength(PropertyLength.SmallText)] public string DisplayId { get; set; } = null!;

    public virtual Account? Account { get; set; } = null!;
    public virtual State? State { get; set; } = null!;
    public ICollection<Cave> Caves { get; set; } = new HashSet<Cave>();
    public ICollection<CavePermission> CavePermissions { get; set; } = new HashSet<CavePermission>();
}

public class CountyConfiguration : BaseEntityTypeConfiguration<County>
{
    public override void Configure(EntityTypeBuilder<County> builder)
    {
        builder.HasOne(e => e.Account)
            .WithMany(e => e.Counties)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.State)
            .WithMany(e => e.Counties)
            .HasForeignKey(e => e.StateId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => new { e.AccountId, e.StateId, e.DisplayId }).IsUnique();
    }
}