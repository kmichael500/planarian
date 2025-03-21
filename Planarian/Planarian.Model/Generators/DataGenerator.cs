using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

namespace Planarian.Model.Generators;

public class DataGenerator
{
    private readonly PlanarianDbContext _dbContext;
    private static readonly DateTime Now = DateTime.UtcNow;

    public DataGenerator(PlanarianDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddOrUpdateDefaultData()
    {
        var allGeneratedTags = new List<IEnumerable<TagType>>()
        {
            GenerateArcheologyTags(),
            GenerateEntranceHydrologyTags(),
            GenerateBiologyTags(),
            GenerateEntranceStatusTags(),
            GenerateFieldIndicationTags(),
            GenerateMapStatusTags(),
            GenerateLocationQualityTags(),
            GenerateFileTags(),
        };

        foreach (var tagGroup in allGeneratedTags)
        {
            foreach (var tag in tagGroup)
            {
                if (tag.Id.Length != PropertyLength.Id)
                {
                    throw new ArgumentException("Tag Id is not the correct length");
                }
            }
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var permissions = GeneratePermissions();
            foreach (var permission in permissions)
            {
                var existingPermission = await _dbContext.Permissions.FindAsync(permission.Id);

                if (existingPermission != null)
                {
                    existingPermission.Name = permission.Name;
                    existingPermission.Description = permission.Description;
                    existingPermission.IsHidden = permission.IsHidden;
                    existingPermission.Key = permission.Key;
                    existingPermission.PermissionType = permission.PermissionType;
                    existingPermission.SortOrder = permission.SortOrder;
                }
                else
                {
                    await _dbContext.Permissions.AddAsync(permission);
                }
            }

            var defaultFeatureSettings = GenerateDefaultFeatureSettings();
            foreach (var setting in defaultFeatureSettings)
            {
                var existingSetting = await _dbContext.FeatureSettings.FindAsync(setting.Id);
                if (existingSetting != null)
                {
                    existingSetting.IsEnabled = setting.IsEnabled;
                    existingSetting.IsDefault = setting.IsDefault;
                }
                else
                {
                    await _dbContext.FeatureSettings.AddAsync(setting);
                }
            }

            foreach (var tagGroup in allGeneratedTags)
            {
                foreach (var tag in tagGroup)
                {
                    var existingTag = await _dbContext.TagTypes.FindAsync(tag.Id);

                    if (existingTag != null)
                    {
                        existingTag.Name = tag.Name;
                        existingTag.Key = tag.Key;
                        existingTag.IsDefault = tag.IsDefault;
                    }
                    else
                    {
                        await _dbContext.TagTypes.AddAsync(tag);
                    }
                }

                await _dbContext.SaveChangesAsync();
            }

            var generatedStates = GenerateStates();

            foreach (var state in generatedStates)
            {
                var existingState = await _dbContext.States.FindAsync(state.Id);

                if (existingState != null)
                {
                    existingState.Name = state.Name;
                    existingState.Abbreviation = state.Abbreviation;
                }
                else
                {
                    await _dbContext.States.AddAsync(state);
                }
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private static IEnumerable<TagType> GenerateArcheologyTags()
    {
        return new List<TagType>
        {
            new() { Id = "hM5WqDNpUZ", Name = "Yes" },
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.Archeology,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    private static IEnumerable<TagType> GenerateEntranceHydrologyTags()
    {
        return new List<TagType>
        {
            new() { Id = "1QO16Io0vY", Name = "Insurgence" },
            new() { Id = "M7z2e9mXMA", Name = "Resurgence" },
            new() { Id = "AWLutt7ycP", Name = "Dry" },
            new() { Id = "YNHO4oxf4e", Name = "Ephemeral" },
            new() { Id = "A5SbUzxlAW", Name = "Intermittent" },
            new() { Id = "LGK50JYmoa", Name = "Perennial" }
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.EntranceHydrology,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    private static IEnumerable<TagType> GenerateBiologyTags()
    {
        return new List<TagType>
        {
            new() { Id = "ovlQqdGz9X", Name = "Bat" },
            new() { Id = "NKuxTWWOc9", Name = "Salamander" },
            new() { Id = "watBrybCEj", Name = "Cricket" },
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.Biology,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    private static IEnumerable<TagType> GenerateEntranceStatusTags()
    {
        return new List<TagType>
        {
            new() { Id = "tEf6BwMc9v", Name = "Private Property" },
            new() { Id = "4zXHk4YGoz", Name = "Government Owned" },
            new() { Id = "YKc4DrWPH5", Name = "Destroyed or Blocked" },
            new() { Id = "eVICXhX8Az", Name = "Commercial Cave" },
            new() { Id = "Ea2qbFPAmL", Name = "Entry Forbidden" },
            new() { Id = "Yzm8fuURGp", Name = "Locked/Gated" },
            new() { Id = "MF0pzvBHuX", Name = "NSS Owned or Leased" },
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.EntranceStatus,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    private static IEnumerable<TagType> GenerateFieldIndicationTags()
    {
        return new List<TagType>
        {
            new() { Id = "W088gfk2DU", Name = "Hillside" },
            new() { Id = "WTYBjdOIKU", Name = "Sink" },
            new() { Id = "gue4UZgRtt", Name = "Bluff or Outcrop" },
            new() { Id = "wzzlSdsdGT", Name = "Quarry" },
            new() { Id = "MQrCQVS1oP", Name = "Inflowing Stream" },
            new() { Id = "jPXyqg2J3m", Name = "Level Ground" },
            new() { Id = "9rL9M2fa80", Name = "Roadcut" },
            new() { Id = "0vbTCSUzOq", Name = "Wet-Weather Stream-bed" },
            new() { Id = "cMipvC2vib", Name = "Underwater" },
            new() { Id = "4Nj73dFHgb", Name = "Spring" },
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.FieldIndication,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    public static IEnumerable<TagType> GenerateMapStatusTags()
    {
        return new List<TagType>
            {
                new() { Id = "gP2IXRuTQF", Name = "Mapped" },
                new() { Id = "LIG2sdzx5R", Name = "Sketch" },
                new() { Id = "CJV2rD3SH2", Name = "Unmapped" },
                new() { Id = "n38D45I7SO", Name = "Pace & Compass" },
                new() { Id = "W3A9hSoYs7", Name = "Tape & Compass" },
            }
            .Select(e => new TagType
            {
                Id = e.Id,
                Name = e.Name,
                Key = TagTypeKeyConstant.MapStatus,
                IsDefault = true,
                CreatedOn = Now
            });
    }

    private static IEnumerable<TagType> GenerateLocationQualityTags()
    {
        var locationQualityTags = new List<TagType>
        {
            new() { Id = "psv2C6Mh2k", Name = "Unverified" },
            new() { Id = "oF8MuzX7OI", Name = "Approximate" },
            new() { Id = "Tndm7SgjVg", Name = "GPS Point" },
            new() { Id = "XY80k3iZ8a", Name = "Lidar / GIS Assisted" },
            new() { Id = "RaQvx9jOB8", Name = "Historic Topo Point" }
        };

        return locationQualityTags.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.LocationQuality,
            IsDefault = true,
            CreatedOn = Now
        }).ToList();
    }

    private static IEnumerable<TagType> GenerateFileTags()
    {
        return new List<TagType>
        {
            new() { Id = "Y0BrXbVmkW", Name = "Map" },
            new() { Id = "6CeJboo99I", Name = "Trip Report" },
            new() { Id = "aaCmvn2MMG", Name = "Article" },
            new() { Id = "LONruyiR4F", Name = "Notes" },
            new() { Id = "jeGhtON7Gk", Name = "Data" },
            new() { Id = "1fhE7lSCI1", Name = "Shapefile/GIS" },
            new() { Id = "FxaiqMZaYH", Name = "Photos" },
            new() { Id = "OthEUrY4eF", Name = "Other" },
        }.Select(e => new TagType
        {
            Id = e.Id,
            Name = e.Name,
            Key = TagTypeKeyConstant.File,
            IsDefault = true,
            CreatedOn = Now
        });
    }

    private static IEnumerable<State> GenerateStates()
    {
        var states = new List<State>
        {
            new() { Id = "A1b2C3d4E5", Name = "Alabama", Abbreviation = "AL" },
            new() { Id = "F6g7H8i9J0", Name = "Alaska", Abbreviation = "AK" },
            new() { Id = "K1l2M3n4O5", Name = "Arizona", Abbreviation = "AZ" },
            new() { Id = "P6q7R8s9T0", Name = "Arkansas", Abbreviation = "AR" },
            new() { Id = "U1v2W3x4Y5", Name = "California", Abbreviation = "CA" },
            new() { Id = "Z6A7B8C9D0", Name = "Colorado", Abbreviation = "CO" },
            new() { Id = "E1F2G3H4I5", Name = "Connecticut", Abbreviation = "CT" },
            new() { Id = "J6K7L8M9N0", Name = "Delaware", Abbreviation = "DE" },
            new() { Id = "O1P2Q3R4S5", Name = "Florida", Abbreviation = "FL" },
            new() { Id = "T6U7V8W9X0", Name = "Georgia", Abbreviation = "GA" },
            new() { Id = "Y1Z2a3B4c5", Name = "Hawaii", Abbreviation = "HI" },
            new() { Id = "D6E7F8G9H0", Name = "Idaho", Abbreviation = "ID" },
            new() { Id = "I1J2K3L4M5", Name = "Illinois", Abbreviation = "IL" },
            new() { Id = "N6O7P8Q9R0", Name = "Indiana", Abbreviation = "IN" },
            new() { Id = "S1T2U3V4W5", Name = "Iowa", Abbreviation = "IA" },
            new() { Id = "X6Y7Z8a9B0", Name = "Kansas", Abbreviation = "KS" },
            new() { Id = "c1d2e3f4g5", Name = "Kentucky", Abbreviation = "KY" },
            new() { Id = "h6i7j8k9l0", Name = "Louisiana", Abbreviation = "LA" },
            new() { Id = "m1n2o3p4q5", Name = "Maine", Abbreviation = "ME" },
            new() { Id = "r6s7t8u9v0", Name = "Maryland", Abbreviation = "MD" },
            new() { Id = "w1x2y3z4A5", Name = "Massachusetts", Abbreviation = "MA" },
            new() { Id = "B6C7D8E9F1", Name = "Michigan", Abbreviation = "MI" },
            new() { Id = "F7G8H9I0J1", Name = "Minnesota", Abbreviation = "MN" },
            new() { Id = "L6M7N8O9P1", Name = "Mississippi", Abbreviation = "MS" },
            new() { Id = "Q1R2S3T4U6", Name = "Missouri", Abbreviation = "MO" },
            new() { Id = "V6W7X8Y9Z1", Name = "Montana", Abbreviation = "MT" },
            new() { Id = "a1b2c3d4e6", Name = "Nebraska", Abbreviation = "NE" },
            new() { Id = "f6g7h8i9j1", Name = "Nevada", Abbreviation = "NV" },
            new() { Id = "k1l2m3n4o6", Name = "New Hampshire", Abbreviation = "NH" },
            new() { Id = "p6q7r8s9t1", Name = "New Jersey", Abbreviation = "NJ" },
            new() { Id = "u1v2w3x4y6", Name = "New Mexico", Abbreviation = "NM" },
            new() { Id = "z6A7B8C9D1", Name = "New York", Abbreviation = "NY" },
            new() { Id = "E1F2G3H4I6", Name = "North Carolina", Abbreviation = "NC" },
            new() { Id = "J6K7L8M9N1", Name = "North Dakota", Abbreviation = "ND" },
            new() { Id = "O1P2Q3R4S6", Name = "Ohio", Abbreviation = "OH" },
            new() { Id = "T6U7V8W9X1", Name = "Oklahoma", Abbreviation = "OK" },
            new() { Id = "Y1Z2a3B4C6", Name = "Oregon", Abbreviation = "OR" },
            new() { Id = "D6E7F8G9H1", Name = "Pennsylvania", Abbreviation = "PA" },
            new() { Id = "I1J2K3L4M6", Name = "Rhode Island", Abbreviation = "RI" },
            new() { Id = "N6O7P8Q9R1", Name = "South Carolina", Abbreviation = "SC" },
            new() { Id = "S1T2U3V4W6", Name = "South Dakota", Abbreviation = "SD" },
            new() { Id = "X6Y7Z8a9B1", Name = "Tennessee", Abbreviation = "TN" },
            new() { Id = "c1d2e3f4g6", Name = "Texas", Abbreviation = "TX" },
            new() { Id = "h6i7j8k9l1", Name = "Utah", Abbreviation = "UT" },
            new() { Id = "m1n2o3p4q6", Name = "Vermont", Abbreviation = "VT" },
            new() { Id = "r6s7t8u9v1", Name = "Virginia", Abbreviation = "VA" },
            new() { Id = "w1x2y3z4A6", Name = "Washington", Abbreviation = "WA" },
            new() { Id = "B6C7D8E9F2", Name = "West Virginia", Abbreviation = "WV" },
            new() { Id = "G1H2I3J4K6", Name = "Wisconsin", Abbreviation = "WI" },
            new() { Id = "L6M7N8O9P2", Name = "Wyoming", Abbreviation = "WY" }
        };

        return states.Select(e => new State
        {
            Id = e.Id,
            Name = e.Name,
            Abbreviation = e.Abbreviation,
            CreatedOn = Now,
        }).ToList();
    }

    private List<FeatureSetting> GenerateDefaultFeatureSettings()
    {
        return new List<FeatureSetting>()
        {
            new()
            {
                Id = "pQokhzzUrp", Key = FeatureKey.EnabledFieldCaveId, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "XmyhgWyC1M", Key = FeatureKey.EnabledFieldCaveName, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "KFPVtOjitA", Key = FeatureKey.EnabledFieldCaveState, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "q5yTZXnt4v", Key = FeatureKey.EnabledFieldCaveCounty, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "GKq8oaxF8e", Key = FeatureKey.EnabledFieldEntranceCoordinates, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "uxvdNjxj27", Key = FeatureKey.EnabledFieldEntranceElevation, IsEnabled = true, IsDefault = true,
                CreatedOn = Now
            },
            new()
            {
                Id = "Z5z5z5z5z5", Key = FeatureKey.EnabledFieldEntranceLocationQuality, IsEnabled = true,
                IsDefault = true, CreatedOn = Now
            },
        };
    }

    private IEnumerable<Permission> GeneratePermissions()
    {
        var viewPermission = new Permission
        {
            Id = "vIeWPz9a00",
            Name = "View",
            Description =
                "Users with view access can view the cave data and suggest updates. Their changes will need a to be approved by a county coordinator or an admin before any changes are applied.",
            IsHidden = false,
            Key = PermissionKey.View,
            PermissionType = PermissionType.Cave,
            SortOrder = 0
        };

        var managePermission = new Permission
        {
            Id = "MaNagEz9a0",
            Name = "Manager",
            Description =
                "Managers are responsible for reviewing and approving changes. They can also invite other members within the state survey to view the data that they manage.",
            IsHidden = false,
            Key = PermissionKey.Manager,
            PermissionType = PermissionType.Cave,
            SortOrder = 1
        };

        var adminPermission = new Permission
        {
            Id = "AdMiNz9a00",
            Name = "Admin",
            Description = "Admins have full access to the system and can manage all data, permissions, users, and account settings.",
            IsHidden = false,
            Key = PermissionKey.Admin,
            PermissionType = PermissionType.User,
            SortOrder = 2
        };

        var planarianAdmin = new Permission
        {
            Id = "PlAnArIaNz",
            Name = "Planarian Admin",
            Description = "Planarian Admins have access to super secret settings.",
            IsHidden = true,
            Key = PermissionKey.PlanarianAdmin,
            PermissionType = PermissionType.User,
            SortOrder = 3
        };
        

        return [managePermission, viewPermission, adminPermission, planarianAdmin];
    }
}