namespace Planarian.Shared.Routing;

public static class UserPasswordResetRoutes
{
    public const string Segment = "reset-password";
    public const string CodeParameter = "code";
    public const string EmailParameter = "email";

    private const string EmailToken = "{" + EmailParameter + "}";

    public static class Api
    {
        public const string Reset = UserPasswordResetRoutes.Segment;
        public const string SendEmail = UserPasswordResetRoutes.Segment + "/email/" + EmailToken;

        public static class Names
        {
            public const string Reset = "UserPasswordReset.Reset";
            public const string SendEmail = "UserPasswordReset.SendEmail";
        }
    }

    public static class Client
    {
        public const string Path = "/" + UserPasswordResetRoutes.Segment;

        public static string Get(string code) =>
            Path + "?" + CodeParameter + "=" + Uri.EscapeDataString(code);
    }
}
