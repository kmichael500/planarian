using System.Text.RegularExpressions;
using Planarian.Library.Exceptions;

namespace Planarian.Modules.Account.Import.Models;

public class CountyCaveInfo
{
    public string CountyCode { get; set; }
    public int CountyCaveNumber { get; set; }
    public static CountyCaveInfo Parse(string input, string idRegex, string delimiter)
    {
        var regex = new Regex(idRegex);
        var match = regex.Match(input);

        if (!match.Success)
        {
            throw ApiExceptionDictionary.BadRequest("The filename does not match the provided regex pattern.");
        }

        var id = match.Value;
        string countyCode;
        int countyCaveNumber;

        if (string.IsNullOrWhiteSpace(delimiter))
        {
            // If no delimiter is provided, assume the format is CountyCodeCountyCaveNumber
            var splitIndex = id.IndexOfAny("0123456789".ToCharArray());
            if (splitIndex == -1)
            {
                throw new ArgumentException("The ID does not contain a valid county cave number.");
            }
            countyCode = id.Substring(0, splitIndex);
            if (!int.TryParse(id.Substring(splitIndex), out countyCaveNumber))
            {
                throw new ArgumentException("The ID does not contain a valid county cave number.");
            }
        }
        else
        {
            var parts = id.Split(new[] { delimiter }, StringSplitOptions.None);

            if (parts.Length != 2 || !int.TryParse(parts[1], out var caveNumber))
            {
                throw ApiExceptionDictionary.BadRequest(
                    $"'{input}' does not contain a valid county cave number.");

            }

            countyCaveNumber = caveNumber;
            countyCode = parts[0];
        }

        return new CountyCaveInfo
        {
            CountyCode = countyCode,
            CountyCaveNumber = countyCaveNumber
        };
    }
}