using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class UserAuthenticationModelConfigurationTests
{
    [Fact]
    public void ResetCodesAreExpandedWithoutChangingInvitationOrConfirmationCodes()
    {
        using var context = CreateContext();
        var user = GetUserEntity(context);

        Assert.Equal(PropertyLength.PasswordResetCode, user.FindProperty(nameof(User.PasswordResetCode))!.GetMaxLength());
        Assert.Equal(PropertyLength.InvitationCode, user.FindProperty(nameof(User.EmailConfirmationCode))!.GetMaxLength());
        Assert.Equal(64, PropertyLength.PasswordResetCode);
        Assert.Equal(10, PropertyLength.InvitationCode);
    }

    [Fact]
    public void VerifiedEmailIsUniqueWhilePendingEmailDoesNotReserveOwnership()
    {
        using var context = CreateContext();
        var user = GetUserEntity(context);

        Assert.DoesNotContain(user.GetIndexes(), index =>
            index.Properties.Any(property => property.Name == nameof(User.PendingEmailAddress)));

        var verifiedEmailIndex = Assert.Single(user.GetIndexes().Where(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(User.EmailAddress) })));
        Assert.True(verifiedEmailIndex.IsUnique);
        Assert.Equal("\"IsTemporary\" = false", verifiedEmailIndex.GetFilter());
    }

    private static IEntityType GetUserEntity(PlanarianDbContextBase context) =>
        Assert.IsAssignableFrom<IEntityType>(context.Model.FindEntityType(typeof(User)));

    private static PlanarianDbContextBase CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContextBase>()
            .UseNpgsql("Host=localhost;Database=planarian_auth_model_tests;Username=unused;Password=unused",
                options => options.UseNetTopologySuite())
            .Options;
        return new PlanarianDbContextBase(options);
    }
}
