namespace Planarian.Shared.Routing;

public static class UserEmailConfirmationRoutes
{
    public const string Segment = "confirm-email";

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
}
