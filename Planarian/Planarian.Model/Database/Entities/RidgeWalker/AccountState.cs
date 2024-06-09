using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class AccountState : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;

    public virtual State? State { get; set; } = null!;
    public virtual Account? Account { get; set; } = null!;
}

public class AccountStateConfiguration : IEntityTypeConfiguration<AccountState>
{
    public void Configure(EntityTypeBuilder<AccountState> builder)
    {
        builder.HasKey(e => new { e.AccountId, e.StateId });

        builder.HasOne(e => e.Account)
            .WithMany(a => a.AccountStates)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.State)
            .WithMany(e => e.AccountStates)
            .HasForeignKey(e => e.StateId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}