namespace Planarian.Model.Shared.Helpers;

public class IdGenerator
{
    public static string Generate()
    {
        var base64Guid = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        // Replace URL unfriendly characters with better ones
        base64Guid = base64Guid.Replace('+', RandomChar()).Replace('/', RandomChar());

        // Remove the trailing ==
        base64Guid = base64Guid[..^2];

        return base64Guid[..10];
    }

    private static char RandomChar()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();

        var index = random.Next(0, chars.Length - 1);

        return chars[index];
    }
}