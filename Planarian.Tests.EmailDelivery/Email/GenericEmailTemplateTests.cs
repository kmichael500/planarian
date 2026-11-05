using System.Text;
using HandlebarsDotNet;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Email.Substitutions;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class GenericEmailTemplateTests
{
    private static readonly string Template = ReadEmbeddedTemplate();

    [Fact]
    public void ActionEmailRendersWithFallbackUrlAndEscapedContent()
    {
        var replacements = new GenericEmailSubstitutions(
            new[] { "Welcome to Planarian!", "You have been invited by <Example & Co>." },
            "Welcome!", "Create Account", "https://example.com/invite?code=a&source=email");
        replacements.Substitutions["websiteUrl"] = "https://example.com";

        var html = Handlebars.Compile(Template)(replacements.Substitutions);

        Assert.Contains("<h1", html);
        Assert.Contains("Welcome!", html);
        Assert.Contains("You have been invited by &lt;Example &amp; Co&gt;.", html);
        Assert.Contains("Create Account", html);
        Assert.Contains("https://example.com/invite?code=a&amp;source=email", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void NotificationWithoutButtonHasNoActionOrFallback()
    {
        var replacements = new GenericEmailSubstitutions(
            "Your account access has been disabled.", "Account access disabled");
        replacements.Substitutions["websiteUrl"] = "https://example.com";

        var html = Handlebars.Compile(Template)(replacements.Substitutions);

        Assert.Contains("Account access disabled", html);
        Assert.Contains("Your account access has been disabled.", html);
        Assert.DoesNotContain("If the button doesn't work", html);
        Assert.DoesNotContain("mailto:", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void PasswordNotificationRendersItsSingleContactParagraph()
    {
        var message = "Your password was changed. If you did not make this change, please contact Example Support & <Assistance> immediately at security@example.org.";
        var replacements = new GenericEmailSubstitutions(message, "Password changed");
        replacements.Substitutions["websiteUrl"] = "https://example.com";

        var html = Handlebars.Compile(Template)(replacements.Substitutions);

        Assert.Contains("Example Support &amp; &lt;Assistance&gt; immediately at security@example.org.", html);
        Assert.DoesNotContain("mailto:", html);
        Assert.DoesNotContain("{{", html);
        Assert.Contains(message, replacements.ToPlainText("https://example.com"));
    }

    [Fact]
    public void PlainTextIncludesMessageAndOnlyPresentAction()
    {
        var action = new GenericEmailSubstitutions(
            new[] { "First paragraph.", "Second paragraph." },
            "Confirm email", "Confirm Email", "https://example.com/confirm");
        var notification = new GenericEmailSubstitutions("Notice body.", "Notice");

        var actionText = action.ToPlainText("https://example.com");
        var notificationText = notification.ToPlainText("https://example.com");

        Assert.Contains("First paragraph.\n\nSecond paragraph.", actionText);
        Assert.Contains("Confirm Email: https://example.com/confirm", actionText);
        Assert.Contains("Visit Planarian: https://example.com", actionText);
        Assert.DoesNotContain("Confirm Email:", notificationText);
        Assert.Contains("Notice body.", notificationText);
    }

    [Fact]
    public void PreheaderIsBoundedAndUsesFirstNonemptyParagraph()
    {
        var replacements = new GenericEmailSubstitutions(
            new[] { "", new string('A', 180) }, "Header");

        var preview = Assert.IsType<string>(replacements.Substitutions["previewText"]);
        Assert.Equal(140, preview.Length);
        Assert.EndsWith("...", preview);
    }

    private static string ReadEmbeddedTemplate()
    {
        using var stream = typeof(EmailService).Assembly.GetManifestResourceStream(
            "Planarian.Emails.GenericEmailTemplate.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
