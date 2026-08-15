namespace Planarian.Shared.Routing;

public static class UserInvitationRoutes
{
    public const string Segment = "invitations";
    public const string CodeParameter = "code";
    public const string CodeConstraint = "invitationCode";

    private const string CodeToken =
        "{" + CodeParameter + ":" + CodeConstraint + "}";

    public static class Api
    {
        public const string Pending = UserInvitationRoutes.Segment;
        public const string ByCode = UserInvitationRoutes.Segment + "/" + CodeToken;
        public const string Accept = ByCode + "/accept";
        public const string Decline = ByCode + "/decline";

        public static class Names
        {
            public const string Pending = "UserInvitation.Pending";
            public const string Get = "UserInvitation.Get";
            public const string Accept = "UserInvitation.Accept";
            public const string Decline = "UserInvitation.Decline";
        }
    }
}
