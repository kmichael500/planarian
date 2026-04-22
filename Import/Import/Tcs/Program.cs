using CsvHelper;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper.Configuration;
using Tcs;

public partial class Program
{
    private static readonly Dictionary<string, string> GeologyCanonicalNames =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Monteagle Ls"] = "Monteagle Limestone",
            ["Monteagle"] = "Monteagle Limestone",
            ["Hartselle"] = "Hartselle Formation",
            ["Bangor"] = "Bangor Limestone",
            ["Warsaw Ls"] = "Warsaw Limestone",
            ["St Louis Ls"] = "St Louis Limestone",
            ["St Louis"] = "St Louis Limestone",
            ["Pennington"] = "Pennington Formation",
            ["Bigby-Canon Limestone"] = "Bigby-Cannon Limestone",
            ["Bigby-Cannon Ls"] = "Bigby-Cannon Limestone",
            ["Leipers-Catheys Fm"] = "Leipers-Catheys Formation",
            ["Leipers-Catheys"] = "Leipers-Catheys Formation",
            ["Chepultepec Do"] = "Chepultepec Dolomite",
            ["Chepultepec"] = "Chepultepec Dolomite",
            ["Blackford Fm"] = "Blackford Formation",
            ["Eidson Member of Lincolnshire Fm"] = "Eidson Member of Lincolnshire Formation",
            ["Hardin SS"] = "Hardin Sandstone",
            ["Lincoinshire Formation"] = "Lincolnshire Formation",
            ["Brassfield Formation"] = "Brassfield Limestone",
            ["Leipers"] = "Leipers Formation",
            ["Leipers Limestone"] = "Leipers Formation",
            ["Longview"] = "Longview Dolomite",
            ["Kingsport"] = "Kingsport Dolomite",
            ["Fort Payne"] = "Fort Payne Formation",
            ["Fernvale"] = "Fernvale Limestone",
            ["Fervale"] = "Fernvale Limestone",
            ["Arnheim"] = "Arnheim Formation",
            ["Laurel"] = "Laurel Limestone",
            ["Raccoon Mountain"] = "Raccoon Mountain Formation"
        };

    private static readonly Dictionary<string, string> GroupedGeologyCanonicalNames =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Hurricance Bridge and Woodway Limestones Undivid"] =
                "Hurricane Bridge & Woodway Limestones Undivided",
            ["Chepultepec, Longview and Newala Dols. Undiff."] =
                "Chepultepec & Longview & Newala Dolomites Undifferentiated",
            ["Kingsport, Longview, Chepultepec Undifferentiated"] =
                "Kingsport & Longview & Chepultepec Undifferentiated",
            ["Jonesboro, Mosheim, Lenoir Ls Undivided"] =
                "Jonesboro & Mosheim & Lenoir Limestones Undivided",
            ["Crooked Fork Group and Rockcastle Undiff."] =
                "Crooked Fork Group & Rockcastle Undifferentiated",
            ["Vandever, Newton, and Whitwell Formation, Undiff"] =
                "Vandever & Newton & Whitwell Formation Undifferentiated",
            ["Fort Payne Formation and Hardin SS and Wayne Group"] =
                "Fort Payne Formation & Hardin Sandstone & Wayne Group",
            ["Devonian and Silurian Undifferentiated"] =
                "Devonian & Silurian Undifferentiated"
        };

    private static readonly Dictionary<string, string> GeologicAgeCanonicalNames =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Miss"] = "Mississippian",
            ["Dev"] = "Devonian",
            ["Devon"] = "Devonian",
            ["Ord"] = "Ordovician",
            ["Ordov"] = "Ordovician",
            ["Penn"] = "Pennsylvanian",
            ["Sil"] = "Silurian"
        };

    private static readonly Dictionary<string, string> GroupedGeologicAgeCanonicalNames =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Dev & Sil Und"] = "Devonian & Silurian Undifferentiated"
        };

    private static readonly Dictionary<string, string> PhysiographicProvinceCanonicalNames =
        new(StringComparer.InvariantCultureIgnoreCase)
        {
            ["Sequatchie  Valley"] = "Sequatchie Valley",
            ["Unaka Mtns & Blue Ridge"] = "Unaka Mountains & Blue Ridge",
            ["Gulf & Atlantic Costal Plains Undiff."] = "Gulf & Atlantic Coastal Plains Undifferentiated"
        };

    internal enum TagSeparatorPolicy
    {
        CommaOnly,
        Geology,
        GeologicAge
    }

    public static void Main()
    {
        const string filePath = "/Users/michaelketzner/Downloads/TCSnarr.csv";
        var extractedRecords = ReadCavesFromCsv(filePath);

        var (caveRecords, entranceRecords) = SplitCaveRecords(extractedRecords);

        // cave records contain the primary entrance, the entranceRecords contains the other entrances
        var allEntranceRecords = caveRecords.Concat(entranceRecords);

        var planarianCaveRecords = ExtractCaveRecords(caveRecords);

        var planarianEntranceRecords = ExtractEntranceRecords(allEntranceRecords);

        WriteCsvFile("/Users/michaelketzner/Downloads/planarianCaveRecords.csv", planarianCaveRecords);
        WriteCsvFile("/Users/michaelketzner/Downloads/planarianEntranceRecords.csv", planarianEntranceRecords);

    }

    private static void WriteCsvFile<T>(string filePath, IEnumerable<T> records)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(records);
    }

    private static IEnumerable<CaveCsvModel> ExtractCaveRecords(IEnumerable<CaveRecord> caveRecords)
    {
        var planarianCaveRecords = new List<CaveCsvModel>();
        foreach (var cave in caveRecords)
        {
            var countyCode = new string(cave.TcsNumber.TakeWhile(char.IsLetter).ToArray());
            var countyNumberStr = new string(cave.TcsNumber.SkipWhile(char.IsLetter).ToArray());

            int.TryParse(countyNumberStr, out var countyNumber);

            if (string.IsNullOrWhiteSpace(cave.CaveName) ||
                string.IsNullOrWhiteSpace(countyCode) || string.IsNullOrWhiteSpace(countyNumberStr) ||
                string.IsNullOrWhiteSpace(cave.CountyName))
            {
                throw new Exception();
            }

            var mapStatuses = cave.MapStatus;
            var geology = ExtractGeologyTags(cave.CaveGeology);
            var geologyAge = ExtractGeologicAgeTags(cave.GeologicalAge);

            var physiographicProvinces = NormalizePhysiographicProvince(cave.PhysiographicProvince);
            var narrative = TrimBoundaryWhitespace(cave.Narrative);
            var reportedOnDate = ExtractReportedOnDate(narrative);
            var reportedBy = ExtractReportedBy(narrative);

            #region TCS Specific Tags

            var otherTags =
                ExtractCommaSeparatedTags(("Gear", cave.RequiredGear));

            #endregion

            var planarianRecord = new CaveCsvModel
            {
                CaveName = cave.CaveName,
                AlternateNames = null,
                State = "TN",
                CountyCode = countyCode,
                CountyCaveNumber = countyNumber,
                CountyName = cave.CountyName,
                MapStatuses = mapStatuses,
                CartographerNames = null,
                CaveLengthFt = cave.LengthFt,
                CaveDepthFt = cave.DepthFt,
                MaxPitDepthFt = cave.PitDepthFt,
                NumberOfPits = cave.NumberOfPits,
                Narrative = narrative,
                Geology = geology,
                GeologicAges = geologyAge,
                PhysiographicProvinces = physiographicProvinces,
                Archeology = null,
                Biology = null,
                ReportedOnDate = reportedOnDate,
                ReportedByNames = reportedBy,
                IsArchived = false,
                OtherTags = otherTags
            };

            planarianCaveRecords.Add(planarianRecord);

        }

        return planarianCaveRecords;
    }

    private static List<EntranceCsvModel> ExtractEntranceRecords(IEnumerable<CaveRecord> entranceRecord)
    {
        var planarianEntranceRecords = new List<EntranceCsvModel>();
        foreach (var entrance in entranceRecord)
        {
            var match = CaveIdRegexWithinString().Match(entrance.TcsNumber);
            string? tcsNumber = null;
            string? entranceNumberPart = null;
            if (match.Success)
            {
                tcsNumber = match.Value;
                entranceNumberPart = entrance.TcsNumber[match.Length..];
            }

            // entrance number will be empty/null if it is the cave record, which is the primary entrance
            var isPrimaryEntrance = string.IsNullOrWhiteSpace(entranceNumberPart);


            var countyCode = new string(tcsNumber?.TakeWhile(char.IsLetter).ToArray());
            var countyNumberStr = new string(tcsNumber?.SkipWhile(char.IsLetter).ToArray());

            int.TryParse(countyNumberStr, out var countyNumber);

            if (string.IsNullOrWhiteSpace(countyCode) || string.IsNullOrWhiteSpace(countyNumberStr))
            {
                throw new Exception();
            }


            #region TCS Specific Tags

            var ownershipTags =
                ExtractCommaSeparatedTags((null, entrance.Ownership));

            var fieldIndicationTags = ExtractCommaSeparatedTags(
                (null, entrance.FieldIndication),
                (null, entrance.EntranceType));


            #endregion

            var planarianRecord = new EntranceCsvModel
            {
                CountyCode = countyCode,
                CountyCaveNumber = countyNumber.ToString(),
                EntranceName = entrance.CaveName,
                DecimalLatitude = entrance.Latitude,
                DecimalLongitude = entrance.Longitude,
                EntranceElevationFt = entrance.ElevationFt,
                LocationQuality = "Unverified",
                EntranceDescription = null,
                EntrancePitDepth = !isPrimaryEntrance ? entrance.PitDepthFt : null,
                EntranceStatuses = ownershipTags,
                EntranceHydrology = null,
                FieldIndication = fieldIndicationTags,
                ReportedOnDate = null,
                ReportedByNames = null,
                IsPrimaryEntrance = isPrimaryEntrance,
                EntranceId = null
            };

            planarianEntranceRecords.Add(planarianRecord);

        }

        return planarianEntranceRecords;
    }

    private static string? ExtractReportedOnDate(string? narrative)
    {
        return null;
    }

    private static string? ExtractReportedBy(string? narrative)
    {
        return null;
    }

    private static string? TrimBoundaryWhitespace(string? value)
    {
        return value?.Trim();
    }

    internal static string ExtractGeologyTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalizedInput = NormalizeGeologyInput(input);

        if (IsGroupedGeologyUnit(normalizedInput))
        {
            return NormalizeGroupedGeologyTag(normalizedInput);
        }

        return ExtractTags(TagSeparatorPolicy.Geology, (null, normalizedInput));
    }

    internal static string ExtractGeologicAgeTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalizedInput = NormalizeGeologicAgeInput(input);

        if (IsGroupedGeologicAgeUnit(normalizedInput))
        {
            return NormalizeGroupedGeologicAgeTag(normalizedInput);
        }

        return ExtractTags(TagSeparatorPolicy.GeologicAge, (null, normalizedInput));
    }

    internal static string ExtractCommaSeparatedTags(params (string? prefix, string? input)[] inputs)
    {
        return ExtractTags(TagSeparatorPolicy.CommaOnly, inputs);
    }

    internal static string? NormalizePhysiographicProvince(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var normalizedInput = Regex.Replace(input.Trim(), @"\s+", " ");

        if (PhysiographicProvinceCanonicalNames.TryGetValue(input.Trim(), out var canonicalName))
        {
            return canonicalName;
        }

        if (PhysiographicProvinceCanonicalNames.TryGetValue(normalizedInput, out canonicalName))
        {
            return canonicalName;
        }

        return normalizedInput;
    }

    private static string ExtractTags(TagSeparatorPolicy separatorPolicy, params (string? prefix, string? input)[] inputs)
    {
        var tagsList = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        foreach (var (prefix, input) in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
                continue;

            foreach (var tag in SplitTags(input, separatorPolicy))
            {
                var trimmedTag = NormalizeTag(tag, separatorPolicy);
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    trimmedTag = $"{prefix}: {trimmedTag}";
                }
                if (!string.IsNullOrWhiteSpace(trimmedTag) && seenTags.Add(trimmedTag))
                {
                    tagsList.Add(trimmedTag);
                }
            }
        }

        return string.Join(", ", tagsList);
    }

    private static string NormalizeTag(string input, TagSeparatorPolicy separatorPolicy)
    {
        var normalizedTag = Regex.Replace(input.Trim(), @"\s+", " ");

        if (separatorPolicy == TagSeparatorPolicy.Geology)
        {
            normalizedTag = NormalizeGeologyTag(normalizedTag);
        }
        else if (separatorPolicy == TagSeparatorPolicy.GeologicAge)
        {
            normalizedTag = NormalizeGeologicAgeTag(normalizedTag);
        }
        else if (normalizedTag.Equals("Artifical Tunnel", StringComparison.InvariantCultureIgnoreCase))
        {
            normalizedTag = "Artificial Tunnel";
        }
        else if (normalizedTag.Equals("Large, walk-in", StringComparison.InvariantCultureIgnoreCase))
        {
            normalizedTag = "Large/Walk-in";
        }

        return normalizedTag;
    }

    private static string NormalizeGeologyTag(string input)
    {
        var normalized = input.Trim();

        if (GeologyCanonicalNames.TryGetValue(normalized, out var canonicalName))
        {
            return canonicalName;
        }

        return normalized;
    }

    private static string NormalizeGeologicAgeTag(string input)
    {
        if (GeologicAgeCanonicalNames.TryGetValue(input.Trim(), out var canonicalName))
        {
            return canonicalName;
        }

        return input.Trim();
    }

    private static string NormalizeGroupedGeologyTag(string input)
    {
        var lookupKey = Regex.Replace(input, @"\s*&\s*", " and ");

        if (GroupedGeologyCanonicalNames.TryGetValue(lookupKey, out var canonicalName))
        {
            return canonicalName;
        }

        return input;
    }

    private static string NormalizeGroupedGeologicAgeTag(string input)
    {
        if (GroupedGeologicAgeCanonicalNames.TryGetValue(input, out var canonicalName))
        {
            return canonicalName;
        }

        return input;
    }

    private static string NormalizeGeologyInput(string input)
    {
        var normalized = Regex.Replace(input.Trim(), @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        normalized = Regex.Replace(normalized, @"\s*&\s*", " & ");
        normalized = Regex.Replace(normalized, @"\s+and\s+", " and ", RegexOptions.IgnoreCase);

        return normalized;
    }

    private static string NormalizeGeologicAgeInput(string input)
    {
        var normalized = Regex.Replace(input.Trim(), @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        normalized = Regex.Replace(normalized, @"\s*&\s*", " & ");
        normalized = Regex.Replace(normalized, @"\s+and\s+", " & ", RegexOptions.IgnoreCase);

        return normalized;
    }

    private static bool IsGroupedGeologyUnit(string input)
    {
        return GroupedGeologySuffixRegex().IsMatch(input) &&
               (input.Contains(',') || input.Contains('&') || GeologyAndSeparatorRegex().IsMatch(input));
    }

    private static bool IsGroupedGeologicAgeUnit(string input)
    {
        return GroupedGeologicAgeCanonicalNames.ContainsKey(input);
    }

    private static IEnumerable<string> SplitTags(string input, TagSeparatorPolicy separatorPolicy)
    {
        var tags = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .AsEnumerable();

        if (separatorPolicy == TagSeparatorPolicy.Geology)
        {
            tags = tags
                .SelectMany(tag => tag.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .SelectMany(tag => GeologyAndSeparatorRegex()
                    .Split(tag)
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Select(part => part.Trim()));
        }
        else if (separatorPolicy == TagSeparatorPolicy.GeologicAge)
        {
            tags = tags
                .SelectMany(tag => tag.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return tags;
    }

    private static List<CaveRecord> ReadCavesFromCsv(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null
        };
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<CaveRecordMap>();
        return [.. csv.GetRecords<CaveRecord>()];
    }

    public static (List<CaveRecord> caveRecords, List<CaveRecord> entranceRecords) SplitCaveRecords(List<CaveRecord> caveRecords)
    {
        var regex = CaveIdRegex();

        var caves = new List<CaveRecord>();
        var secondaryEntrances = new List<CaveRecord>();

        foreach (var record in caveRecords)
        {
            record.TcsNumber = record.TcsNumber.Trim();
            record.CaveName = record.CaveName?.Trim();
            record.CountyName = record.CountyName?.Trim();
            record.TopographicName = record.TopographicName?.Trim();
            record.Ownership = record.Ownership?.Trim();
            record.RequiredGear = record.RequiredGear?.Trim();
            record.EntranceType = record.EntranceType?.Trim();
            record.FieldIndication = record.FieldIndication?.Trim();
            record.MapStatus = record.MapStatus?.Trim();
            record.CaveGeology = record.CaveGeology?.Trim();
            record.GeologicalAge = record.GeologicalAge?.Trim();
            record.PhysiographicProvince = record.PhysiographicProvince?.Trim();
            record.Narrative = record.Narrative?.Trim();

            if (regex.IsMatch(record.TcsNumber))
            {
                caves.Add(record);
            }
            else
            {
                secondaryEntrances.Add(record);
            }
        }

        return (caves, secondaryEntrances);
    }

    [GeneratedRegex(@"^[A-Z]{2}\d+$")]
    private static partial Regex CaveIdRegex();

    [GeneratedRegex(@"^[A-Z]{2}\d+")]
    private static partial Regex CaveIdRegexWithinString();

    [GeneratedRegex(@"\s+and\s+", RegexOptions.IgnoreCase)]
    private static partial Regex GeologyAndSeparatorRegex();

    [GeneratedRegex(@"Undivid(?:ed)?|Undiff(?:erentiated)?", RegexOptions.IgnoreCase)]
    private static partial Regex GroupedGeologySuffixRegex();
}
