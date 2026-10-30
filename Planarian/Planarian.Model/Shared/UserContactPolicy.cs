using System.Globalization;
using System.Text.RegularExpressions;

namespace Planarian.Model.Shared;

public static class UserContactPolicy
{
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return false;

        try
        {
            email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                RegexOptions.None, TimeSpan.FromMilliseconds(200));

            string DomainMapper(Match match)
            {
                var idn = new IdnMapping();
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

    public static bool IsValidPhoneNumber(string? phoneNumber) =>
        !string.IsNullOrEmpty(phoneNumber) && Regex.IsMatch(phoneNumber, @"^\+1\d{10}$");

    public static string NormalizePhoneNumber(string phoneNumber)
    {
        var rawNumber = Regex.Replace(phoneNumber, @"\D", "");
        if (rawNumber.Length == 11 && rawNumber[0] == '1')
            return "+" + rawNumber;
        if (rawNumber.Length == 10)
            return "+1" + rawNumber;

        throw new ArgumentException("The phone number is not a valid US number.", nameof(phoneNumber));
    }
}
