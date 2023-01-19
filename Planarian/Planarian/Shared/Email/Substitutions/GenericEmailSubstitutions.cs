using Planarian.Shared.Services.Substitutions;

namespace Planarian.Shared.Email.Substitutions;

public class GenericEmailSubstitutions : ISubstitution
{
    public Dictionary<string, object> Substitutions { get; set; }

    public GenericEmailSubstitutions(string header, string message, string? buttonText = null, string? buttonUrl = null)
    {

        Substitutions = new Dictionary<string, object>
        {
            ["message"] = message,
            ["header"] = header,
        };
        
        if (!string.IsNullOrWhiteSpace(buttonText))
        {
            Substitutions.Add("buttonText", buttonText);
        }

        if (!string.IsNullOrWhiteSpace(buttonUrl))
        {
            Substitutions.Add("buttonUrl", buttonUrl);
        }
    }
}