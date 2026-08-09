using System.Globalization;
using System.Text;

namespace Planarian.Tests;

internal static class ImportScaleDataFactory
{
    public static string BuildCaves(IEnumerable<int> numbers, Func<int, bool> changed, bool churn = false)
    {
        var csv = new StringBuilder(ImportDryRunIntegrationTests.CaveHeader).Append('\n');
        foreach (var number in numbers)
        {
            var name = changed(number) ? $"Benchmark Cave {number} updated" : $"Benchmark Cave {number}";
            var geology = churn && number % 25 == 0 ? "Dolomite" : "Limestone";
            csv.Append(Row(name, "Benchmark County", "BEN", number, "TN", $"Alt {number}", "Mapped",
                "Mapper A,Mapper B", 1000 + number % 500, 100 + number % 200, 20 + number % 100,
                2 + number % 5, geology, "Mississippian", "Cumberland Plateau", "Artifact", "Bats",
                "2026-08-01", "Reporter A,Reporter B,Reporter C", false, "Interesting",
                $"Benchmark narrative {number} " + new string((char)('a' + number % 26), 180 + number % 160)))
                .Append('\n');
        }
        return csv.ToString();
    }

    public static string BuildEntrances(IEnumerable<int> numbers, bool replacement)
    {
        var csv = new StringBuilder(ImportDryRunIntegrationTests.EntranceHeader).Append('\n');
        foreach (var number in numbers)
        {
            var count = replacement ? 1 : EntranceCount(number);
            for (var index = 0; index < count; index++)
            {
                csv.Append(Row(
                    replacement ? $"Replacement {number}" : $"Entrance {number}-{index + 1}",
                    "BEN", number, index == 0,
                    35 + number / 100000d + index / 1000000d,
                    -86 - number / 100000d - index / 1000000d,
                    500 + number % 1000 + index,
                    replacement && number % 10 == 0 ? "Estimated" : "Survey Grade",
                    index * 5,
                    replacement && number % 10 == 0 ? "Restricted" : "Open",
                    "Wet", "Sink", "2026-08-02", "Reporter A,Reporter B",
                    replacement ? "Replacement entrance" : "Benchmark entrance")).Append('\n');
            }
        }
        return csv.ToString();
    }

    private static int EntranceCount(int number) => number switch
    {
        <= 8000 => 1,
        <= 9500 => 2,
        <= 9900 => 3,
        _ => 10
    };

    private static string Row(params object?[] values) => string.Join(',', values.Select(value =>
    {
        var text = value switch
        {
            null => "",
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        return text.Contains(',') || text.Contains('"') ? '"' + text.Replace("\"", "\"\"") + '"' : text;
    }));
}
