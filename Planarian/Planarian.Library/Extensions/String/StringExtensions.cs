using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Planarian.Library.Constants;
using Planarian.Model.Shared;

namespace Planarian.Library.Extensions.String;

public static class StringExtensions
{
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

    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = char.ToUpper(input[0]) + input[1..];

        return result;
    }

    public static bool IsValidEmail(this string email) =>
        UserContactPolicy.IsValidEmail(email);

    public static bool IsValidPhoneNumber(this string phoneNumber) =>
        UserContactPolicy.IsValidPhoneNumber(phoneNumber);

    public static string ExtractPhoneNumber(this string phoneNumber) =>
        UserContactPolicy.NormalizePhoneNumber(phoneNumber);

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