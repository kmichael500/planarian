using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Migrations.Generators;

public static class Generator
{

    public static List<TagType> GenerateLocationQualityTags()
    {
        var locationQualityTags = new List<TagType>
        {
            new() { Name = "Confirmed", Key = TagTypeKeyConstant.LocationQuality, CreatedOn = DateTime.UtcNow },
            new() { Name = "Estimated", Key = TagTypeKeyConstant.LocationQuality, CreatedOn = DateTime.UtcNow },
            new() { Name = "Unconfirmed", Key = TagTypeKeyConstant.LocationQuality, CreatedOn = DateTime.UtcNow },
        };


        return locationQualityTags;
    }
    public static List<State> GenerateStates()
    {
        var states = new List<State>
        {
            new() { Name = "Alabama", Abbreviation = "AL" },
            new() { Name = "Alaska", Abbreviation = "AK" },
            new() { Name = "Arizona", Abbreviation = "AZ" },
            new() { Name = "Arkansas", Abbreviation = "AR" },
            new() { Name = "California", Abbreviation = "CA" },
            new() { Name = "Colorado", Abbreviation = "CO" },
            new() { Name = "Connecticut", Abbreviation = "CT" },
            new() { Name = "Delaware", Abbreviation = "DE" },
            new() { Name = "Florida", Abbreviation = "FL" },
            new() { Name = "Georgia", Abbreviation = "GA" },
            new() { Name = "Hawaii", Abbreviation = "HI" },
            new() { Name = "Idaho", Abbreviation = "ID" },
            new() { Name = "Illinois", Abbreviation = "IL" },
            new() { Name = "Indiana", Abbreviation = "IN" },
            new() { Name = "Iowa", Abbreviation = "IA" },
            new() { Name = "Kansas", Abbreviation = "KS" },
            new() { Name = "Kentucky", Abbreviation = "KY" },
            new() { Name = "Louisiana", Abbreviation = "LA" },
            new() { Name = "Maine", Abbreviation = "ME" },
            new() { Name = "Maryland", Abbreviation = "MD" },
            new() { Name = "Massachusetts", Abbreviation = "MA" },
            new() { Name = "Michigan", Abbreviation = "MI" },
            new() { Name = "Minnesota", Abbreviation = "MN" },
            new() { Name = "Mississippi", Abbreviation = "MS" },
            new() { Name = "Missouri", Abbreviation = "MO" },
            new() { Name = "Montana", Abbreviation = "MT" },
            new() { Name = "Nebraska", Abbreviation = "NE" },
            new() { Name = "Nevada", Abbreviation = "NV" },
            new() { Name = "New Hampshire", Abbreviation = "NH" },
            new() { Name = "New Jersey", Abbreviation = "NJ" },
            new() { Name = "New Mexico", Abbreviation = "NM" },
            new() { Name = "New York", Abbreviation = "NY" },
            new() { Name = "North Carolina", Abbreviation = "NC" },
            new() { Name = "North Dakota", Abbreviation = "ND" },
            new() { Name = "Ohio", Abbreviation = "OH" },
            new() { Name = "Oklahoma", Abbreviation = "OK" },
            new() { Name = "Oregon", Abbreviation = "OR" },
            new() { Name = "Pennsylvania", Abbreviation = "PA" },
            new() { Name = "Rhode Island", Abbreviation = "RI" },
            new() { Name = "South Carolina", Abbreviation = "SC" },
            new() { Name = "South Dakota", Abbreviation = "SD" },
            new() { Name = "Tennessee", Abbreviation = "TN" },
            new() { Name = "Texas", Abbreviation = "TX" },
            new() { Name = "Utah", Abbreviation = "UT" },
            new() { Name = "Vermont", Abbreviation = "VT" },
            new() { Name = "Virginia", Abbreviation = "VA" },
            new() { Name = "Washington", Abbreviation = "WA" },
            new() { Name = "West Virginia", Abbreviation = "WV" },
            new() { Name = "Wisconsin", Abbreviation = "WI" },
            new() { Name = "Wyoming", Abbreviation = "WY" }
        };

        return states.Select(e=> new State
        {
            Name = e.Name,
            Abbreviation = e.Abbreviation,
            Id = IdGenerator.Generate(),
            CreatedOn = DateTime.UtcNow
        }).ToList();
    }
}