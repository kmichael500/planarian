using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Account : EntityBase
{
    public string Name { get; set; }

    public ICollection<AccountUser> AccountUsers { get; set; } = new HashSet<AccountUser>();
    public ICollection<Cave> Caves { get; set; } = new HashSet<Cave>();
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder
            .HasMany(a => a.AccountUsers)
            .WithOne(au => au.Account)
            .HasForeignKey(au => au.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}