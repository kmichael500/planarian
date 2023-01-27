namespace Planarian.Shared.Email.Models;

public class MessageTypeVm
{
    public MessageTypeVm(string subject, string html, string fromEmail, string fromName)
    {
        Subject = subject;
        Html = html;
        FromEmail = fromEmail;
        FromName = fromName;
    }

    public string Subject { get; }
    public string Html { get; }
    public string FromEmail { get; }
    public string FromName { get; }
}