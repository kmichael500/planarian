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
        var paragraphs = messages.ToList();
        var preview = paragraphs.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))?.Trim() ?? header;
        if (preview.Length > 140) preview = preview[..137] + "...";

        Substitutions = new Dictionary<string, object>
        {
            ["messages"] = paragraphs,
            ["header"] = header,
            ["previewText"] = preview
        };

        if (!string.IsNullOrWhiteSpace(buttonText)) Substitutions.Add("buttonText", buttonText);

        if (!string.IsNullOrWhiteSpace(buttonUrl)) Substitutions.Add("buttonUrl", buttonUrl);
    }

    public Dictionary<string, object> Substitutions { get; set; }

    public string ToPlainText(string websiteUrl)
    {
        var paragraphs = new List<string> { "Planarian", (string)Substitutions["header"] };
        paragraphs.AddRange(((IEnumerable<string>)Substitutions["messages"])
            .Where(message => !string.IsNullOrWhiteSpace(message)));

        if (Substitutions.TryGetValue("buttonText", out var buttonText) &&
            Substitutions.TryGetValue("buttonUrl", out var buttonUrl))
            paragraphs.Add($"{buttonText}: {buttonUrl}");

        paragraphs.Add($"Visit Planarian: {websiteUrl}");
        return string.Join("\n\n", paragraphs);
    }
}
