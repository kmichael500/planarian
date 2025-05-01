using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;
public static class ChangeValueType
{
    public const string String   = "String";
    public const string Int      = "Int";
    public const string Double   = "Double";
    public const string Bool     = "Bool";
    public const string DateTime = "DateTime";
    
    public const string Entrance = "Entrance";
    public const string Cave     = "Cave";
}

public static class ChangeType 
{
    public const string Add    = "Add";
    public const string Update = "Update";
    public const string Delete = "Delete";
}

public class CaveChangeHistory : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string CaveChangeRequestId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? EntranceId { get; set; }
    [MaxLength(PropertyLength.Id)] public string ChangedByUserId { get; set; }

    [MaxLength(PropertyLength.Id)] public string? ApprovedByUserId { get; set; }


    [MaxLength(PropertyLength.Key)] public string PropertyName { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? PropertyId { get; set; } = null!;

    [MaxLength(PropertyLength.Key)] public string ChangeType { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string ChangeValueType { get; set; } = null!;

    public string? ValueString { get; set; }

    public int? ValueInt { get; set; }

    public double? ValueDouble { get; set; }

    public bool? ValueBool { get; set; }

    public DateTime? ValueDateTime { get; set; }


    public Account Account { get; set; } = null!;
    public CaveChangeRequest CaveChangeRequest { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public Cave Cave { get; set; } = null!;
    public User ApprovedByUser { get; set; } = null!;
}

public class CaveChangeLogConfiguration : BaseEntityTypeConfiguration<CaveChangeHistory>
{
    public override void Configure(EntityTypeBuilder<CaveChangeHistory> builder)
    {
        builder.HasOne(e => e.Cave)
            .WithMany(e => e.CaveChangeLogs)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(e=>e.CaveChangeRequest)
            .WithMany(e => e.CaveChangeHistory)
            .HasForeignKey(e => e.CaveChangeRequestId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(e => e.Account)
            .WithMany(e => e.CaveChangeHistory)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .Property(e => e.EntranceId)
            .ValueGeneratedNever()
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        
        builder.HasOne(e => e.ChangedByUser)
            .WithMany(e => e.CaveChangeLogs)
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ApprovedByUser)
            .WithMany()
            .HasForeignKey(e => e.ApprovedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}