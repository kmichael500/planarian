using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Favorite : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string UserId { get; set; } = null!;

    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }

    [MaxLength(PropertyLength.Max)] public string? Notes { get; set; }

    public List<string> Tags { get; set; } = new();

    public virtual Account Account { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Cave Cave { get; set; } = null!;
}

public class FavoriteConfiguration : BaseEntityTypeConfiguration<Favorite>
{
    public override void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasOne(e => e.Account)
            .WithMany(e => e.Favorites)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.Favorites)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(e => e.User)
            .WithMany(e => e.Favorites)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.OwnsOne(favorite => favorite.Tags, buildAction =>
        {
            buildAction.ToJson();
        });
    }
}