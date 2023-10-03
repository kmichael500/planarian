using System.Globalization;
using System.Text.RegularExpressions;
using Planarian.Library.Constants;

namespace Planarian.Library.Extensions.String;

public static class StringExtensions
{
    public static string[] SplitAndTrim(this string? input, char delimiter = ',')
    {
        return input == null ? Array.Empty<string>() : input.Split(delimiter).Select(s => s.Trim()).ToArray();
    }

    public static string FirstCharToUpper(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = char.ToUpper(input[0]) + input[1..];

        return result;
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
}