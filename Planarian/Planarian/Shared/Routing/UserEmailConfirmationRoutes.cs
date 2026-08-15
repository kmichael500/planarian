namespace Planarian.Shared.Routing;

public static class UserEmailConfirmationRoutes
{
    public const string Segment = "confirm-email";
    public const string CodeParameter = "code";

    public static class Api
    {
        public const string Confirm = UserEmailConfirmationRoutes.Segment;
        public const string Resend = UserEmailConfirmationRoutes.Segment + "/resend";

        public static class Names
        {
            public const string Confirm = "UserEmailConfirmation.Confirm";
            public const string Resend = "UserEmailConfirmation.Resend";
        }
    }

    public static class Client
    {
        public const string Path = "/" + UserEmailConfirmationRoutes.Segment;

        public static string Get(string code) =>
            Path + "?" + CodeParameter + "=" + Uri.EscapeDataString(code);
    }
}
