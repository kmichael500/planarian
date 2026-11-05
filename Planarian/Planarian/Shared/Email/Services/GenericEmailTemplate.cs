namespace Planarian.Shared.Email.Services;

internal static class GenericEmailTemplate
{
    private const string ResourceName = "Planarian.Emails.GenericEmailTemplate.html";

    internal static readonly string Html = Load();

    private static string Load()
    {
        using var stream = typeof(GenericEmailTemplate).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded email template {ResourceName} was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
