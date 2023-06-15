using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Migrations.Generators;

public static class DataGenerator
{

    public static IEnumerable<TagType> GenerateLocationQualityTags()
    {
        var locationQualityTags = new List<TagType>
        {
            new() { Name = "Field Confirmed", Key = TagTypeKeyConstant.LocationQuality},
            new() { Name = "Estimated", Key = TagTypeKeyConstant.LocationQuality},
            new() { Name = "Unconfirmed", Key = TagTypeKeyConstant.LocationQuality},
        };


        return locationQualityTags.Select(e=> new TagType
        {
            Name = e.Name,
            Key = e.Key,
            CreatedOn = e.CreatedOn,
            Id = IdGenerator.Generate()
        });
    }
    public static IEnumerable<State> GenerateStates()
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

        var now = DateTime.UtcNow;

        return states.Select(e=> new State
        {
            Name = e.Name,
            Abbreviation = e.Abbreviation,
            CreatedOn = now
        }).ToList();
    }
}