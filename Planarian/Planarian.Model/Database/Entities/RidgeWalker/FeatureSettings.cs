using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class FeatureSetting : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public FeatureKey Key { get; set; } = default;
    public bool IsEnabled { get; set; } = false;
    
    public bool IsDefault { get; set; } = false;
    
    public virtual Account? Account { get; set; }

}

public class FeatureSettingConfiguration : BaseEntityTypeConfiguration<FeatureSetting>
{
    public override void Configure(EntityTypeBuilder<FeatureSetting> builder)
    {
        base.Configure(builder);

        builder.HasOne(e => e.Account)
            .WithMany(e=>e.FeatureSettings)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(e => e.Key)
            .HasConversion<string>()
            .HasMaxLength(PropertyLength.Key);
        
        ConfigureEnum(builder, e=>e.Key);
    }

}



public enum FeatureKey
{
    #region Cave Features

    EnabledFieldCaveId,
    EnabledFieldCaveName,
    EnabledFieldCaveAlternateNames,
    EnabledFieldCaveState,
    EnabledFieldCaveCounty,
    EnabledFieldCaveLengthFeet,
    EnabledFieldCaveDepthFeet,
    EnabledFieldCaveMaxPitDepthFeet,
    EnabledFieldCaveNumberOfPits,
    EnabledFieldCaveReportedOn,
    EnabledFieldCaveReportedByNameTags,
    EnabledFieldCaveGeologyTags,
    EnabledFieldCaveGeologicAgeTags,
    EnabledFieldCavePhysiographicProvinceTags,
    EnabledFieldCaveBiologyTags,
    EnabledFieldCaveArcheologyTags,
    EnabledFieldCaveMapStatusTags,
    EnabledFieldCaveCartographerNameTags,
    EnabledFieldCaveOtherTags,
    EnabledFieldCaveNarrative,


    #endregion

    #region Entrance Features

    EnabledFieldEntranceCoordinates,
    EnabledFieldEntranceElevation,
    EnabledFieldEntranceLocationQuality,
    EnabledFieldEntranceName,
    EnabledFieldEntranceReportedOn,
    EnabledFieldEntranceReportedByNameTags,
    EnabledFieldEntrancePitDepth,
    EnabledFieldEntranceStatusTags,
    EnabledFieldEntranceFieldIndicationTags,
    EnabledFieldEntranceHydrologyTags,
    EnabledFieldEntranceDescription

    #endregion
}
