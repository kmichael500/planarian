using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Account : EntityBase
{
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.Delimiter)] public string? CountyIdDelimiter { get; set; } = null!;

    public ICollection<AccountUser> AccountUsers { get; set; } = new HashSet<AccountUser>();
    public ICollection<Cave> Caves { get; set; } = new HashSet<Cave>();
    public ICollection<AccountState> AccountStates { get; set; } = new HashSet<AccountState>();
    public ICollection<County> Counties { get; set; } = new HashSet<County>();
    public ICollection<TagType> Tags { get; set; } = new HashSet<TagType>();
    public ICollection<FeatureSetting> FeatureSettings { get; set; } = new HashSet<FeatureSetting>();
    public ICollection<CavePermission> CavePermissions { get; set; } = new HashSet<CavePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new HashSet<UserPermission>();
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasMany(e => e.AccountUsers)
            .WithOne(au => au.Account)
            .HasForeignKey(au => au.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.AccountStates)
            .WithOne(e => e.Account)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

    }
}