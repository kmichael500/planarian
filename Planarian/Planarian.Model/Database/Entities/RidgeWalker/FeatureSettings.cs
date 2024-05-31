using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class FeatureSetting : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? UserId { get; set; } = null!;

    [MaxLength(PropertyLength.Key)] public FeatureKey Key { get; set; } = default;
    public bool IsEnabled { get; set; } = false;
    
    public virtual Account? Account { get; set; }
    public virtual User? User { get; set; }

}

public class FeatureSettingConfiguration : BaseEntityTypeConfiguration<FeatureSetting>
{
    public override void Configure(EntityTypeBuilder<FeatureSetting> builder)
    {
        base.Configure(builder);

        builder.HasOne(e => e.Account)
            .WithMany(e=>e.FeatureSettings)
            .HasForeignKey(e => e.AccountId);

        builder.HasOne(e => e.User)
            .WithMany(e=>e.FeatureSettings)
            .HasForeignKey(e => e.UserId);

        builder.Property(e => e.Key)
            .HasConversion<string>()
            .HasMaxLength(PropertyLength.Key);
        
        ConfigureEnum(builder, e=>e.Key);
    }

}



public enum FeatureKey
{
    #region Cave Features

    EnabledFieldCaveAlternateNames,
    EnabledFieldCaveLengthFeet,
    EnabledFieldCaveDepthFeet,
    EnabledFieldCaveMaxPitDepthFeet,
    EnabledFieldCaveNumberOfPits,
    EnabledFieldCaveNarrative,
    EnabledFieldCaveGeologyTags,
    EnabledFieldCaveMapStatusTags,
    EnabledFieldCaveGeologicAgeTags,
    EnabledFieldCavePhysiographicProvinceTags,
    EnabledFieldCaveBiologyTags,
    EnabledFieldCaveArcheologyTags,
    EnabledFieldCaveCartographerNameTags,
    EnabledFieldCaveReportedByNameTags,
    EnabledFieldCaveOtherTags,

    #endregion

    #region Entrance Features

    EnabledFieldEntranceDescription,
    EnabledFieldEntranceLocation,
    EnabledFieldEntranceStatusTags,
    EnabledFieldEntranceHydrologyTags,
    EnabledFieldEntranceFieldIndicationTags,
    EnabledFieldEntranceReportedByNameTags,
    EnabledFieldEntranceOtherTags,
    EnabledFieldEntrancePrimary,

    #endregion
}
