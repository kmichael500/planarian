using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Planarian.Library.Constants;

namespace Planarian.Library.Extensions.String;

public static class StringExtensions
{
    private static readonly Regex IdRegex = new(@"^[A-Za-z0-9]{10}$", RegexOptions.Compiled);

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? input)
    {
        return string.IsNullOrWhiteSpace(input);
    }

    public static string[] SplitAndTrim(this string? input, char delimiter = ',')
    {
        return input == null
            ? Array.Empty<string>()
            : input.Split(delimiter).Where(e => !string.IsNullOrWhiteSpace(e)).Select(s => s.Trim()).ToArray();
    }

    public static string Quote(this string input)
    {
        return $"\"{input}\"";
    }

    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = char.ToUpper(input[0]) + input[1..];

        return result;
    }

    public static bool IsValidId(this string? input)
    {
        return !string.IsNullOrWhiteSpace(input) && IdRegex.IsMatch(input);
    }
    
    public static bool IsValidEmail(this string email)
    {
        if (string.IsNullOrEmpty(email)) return false;

        try
        {
            // Normalize the domain
            email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                RegexOptions.None, TimeSpan.FromMilliseconds(200));

            // Examines the domain part of the email and normalizes it.
            string DomainMapper(Match match)
            {
                // Use IdnMapping class to convert Unicode domain names.
                var idn = new IdnMapping();

                // Pull out and process domain name (throws ArgumentException on invalid)
                var domainName = idn.GetAscii(match.Groups[2].Value);

                return match.Groups[1].Value + domainName;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(email,
                @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                RegexOptions.IgnoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool IsValidPhoneNumber(this string phoneNumber)
    {
        // Check if the string is null or empty
        if (string.IsNullOrEmpty(phoneNumber)) return false;

        // Check if the string is in the correct format
        var regex = new Regex(@"^\+1\d{10}$");
        return regex.IsMatch(phoneNumber);
    }

    public static string ExtractPhoneNumber(this string phoneNumber)
    {
        // Remove all non-numeric characters
        var rawNumber = Regex.Replace(phoneNumber, @"\D", "");

        // Check if the number is a US number
        if (rawNumber.Length == 11 && rawNumber[0] == '1')
            return "+" + rawNumber;
        if (rawNumber.Length == 10)
            return "+1" + rawNumber;
        // Return an empty string if the number is not a valid US number
        throw new ArgumentException("The phone number is not a valid US number.", nameof(phoneNumber));
    }

    public static bool IsValidPassword(this string password)
    {
        return Regex.IsMatch(password, RegularExpressions.PasswordValidation);
    }

    public static string ToCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    /// <summary>
    /// Converts an enumerable of strings into a comma-separated string with "and" before the last element.
    /// For example, { "apple", "banana", "cherry" } becomes "apple, banana, and cherry".
    /// </summary>
    public static string ToCommaSeparatedString(this IEnumerable<string>? items)
    {
        if (items == null)
            return string.Empty;

        var list = items.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        switch (list.Count)
        {
            case 0:
                return string.Empty;
            case 1:
                return list[0];
            case 2:
                return $"{list[0]} and {list[1]}";
        }

        var allButLast = string.Join(", ", list.Take(list.Count - 1));
        var last = list.Last();
        return $"{allButLast}, and {last}";
    }

    /// <summary>
    /// Returns the specified default value if the string is null or consists only of whitespace;
    /// otherwise returns the original string.
    /// Default value is "-" unless specified otherwise.
    /// </summary>
    public static string DefaultIfNullOrWhiteSpace(this string? input, string defaultValue = "-")
    {
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }
}