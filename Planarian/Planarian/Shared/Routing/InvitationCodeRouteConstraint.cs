using Microsoft.AspNetCore.Routing.Constraints;
using Planarian.Model.Shared;

namespace Planarian.Shared.Routing;

public sealed class InvitationCodeRouteConstraint : LengthRouteConstraint
{
    public InvitationCodeRouteConstraint() : base(PropertyLength.InvitationCode)
    {
    }
}
