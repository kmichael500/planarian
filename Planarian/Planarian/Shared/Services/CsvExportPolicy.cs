using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Planarian.Shared.Services;

internal static class CsvExportPolicy
{
    private static readonly HashSet<char> FormulaPrefixes = new()
    {
        '=', '@', '+', '-', '\t', '\r'
    };

    public static CsvWriter CreateWriter(TextWriter writer)
    {
        var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.TypeConverterCache.AddConverter<string>(new SpreadsheetSafeStringConverter());
        return csv;
    }

    private sealed class SpreadsheetSafeStringConverter : StringConverter
    {
        public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
        {
            var text = base.ConvertToString(value, row, memberMapData);
            return !string.IsNullOrEmpty(text) && FormulaPrefixes.Contains(text[0])
                ? $"'{text}"
                : text;
        }
    }
}
