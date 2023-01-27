using Planarian.Shared.Email.Models;

namespace Planarian.Shared.Email.Substitutions;

public class GenericEmailSubstitutions : ISubstitution
{
    public GenericEmailSubstitutions(string message, string header, string? buttonText = null, string? buttonUrl = null)
        : this(new List<string> { message }, header, buttonText, buttonUrl)
    {
    }

    public GenericEmailSubstitutions(IEnumerable<string> messages, string header, string? buttonText = null,
        string? buttonUrl = null)
    {
        Substitutions = new Dictionary<string, object>
        {
            ["messages"] = messages.ToList(),
            ["header"] = header
        };

        if (!string.IsNullOrWhiteSpace(buttonText)) Substitutions.Add("buttonText", buttonText);

        if (!string.IsNullOrWhiteSpace(buttonUrl)) Substitutions.Add("buttonUrl", buttonUrl);
    }

    public Dictionary<string, object> Substitutions { get; set; }
}