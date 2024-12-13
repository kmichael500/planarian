using CsvHelper;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper.Configuration;
using Tcs;

public partial class Program
{
    public static void Main()
    {
        const string filePath = "/Users/michaelketzner/Downloads/TCSnarr.csv";
        var extractedRecords = ReadCavesFromCsv(filePath);
        
        var (caveRecords, entranceRecords) = SplitCaveRecords(extractedRecords);
        
        // cave records contain the primary entrance, the entranceRecords contains the other entrances
        var allEntranceRecords = caveRecords.Concat(entranceRecords);
        
        var planarianCaveRecords = ExtractCaveRecords(caveRecords);
        
        var planarianEntranceRecords = ExtractEntranceRecords(allEntranceRecords);
        
        
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
            var geology = ExtractTags((null, cave.CaveGeology));
            var geologyAge = ExtractTags((null, cave.GeologicalAge));

            var physiographicProvinces = cave.PhysiographicProvince;
            var reportedOnDate = ExtractReportedOnDate(cave.Narrative);
            var reportedBy = ExtractReportedBy(cave.Narrative);

            #region TCS Specific Tags

            var otherTags =
                ExtractTags(("Gear", cave.RequiredGear), ("Entrance Type", cave.EntranceType));

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
                Narrative = cave.Narrative,
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
                ExtractTags((null, entrance.Ownership));

            var fieldIndicationTags = ExtractTags((null, entrance.FieldIndication));


            #endregion

            var planarianRecord = new EntranceCsvModel
            {
                CountyCode = countyCode,
                CountyCaveNumber = countyNumber.ToString(),
                EntranceName = entrance.CaveName,
                DecimalLatitude = entrance.Latitude,
                DecimalLongitude = entrance.Longitude,
                EntranceElevationFt = entrance.ElevationFt,
                LocationQuality = "TODO",
                EntranceDescription = entrance.Narrative,
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

    private static string ExtractTags(params (string? prefix, string? input)[] inputs)
    {
        var tagsList = new List<string>();
        string[] separators = { ",", "and", "&", "AND" };

        foreach (var (prefix, input) in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Split the input using the separators
            var tags = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var tag in tags)
            {
                var trimmedTag = tag.Trim();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    trimmedTag = $"{prefix}: {trimmedTag}";
                }
                if (!string.IsNullOrWhiteSpace(trimmedTag))
                {
                    tagsList.Add(trimmedTag);
                }
            }
        }

        return string.Join(", ", tagsList);
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
        return [..csv.GetRecords<CaveRecord>()];
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
}