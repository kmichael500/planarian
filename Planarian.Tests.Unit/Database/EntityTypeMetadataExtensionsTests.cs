using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Extensions;
using Planarian.Model.Shared.Base;
using Xunit;

namespace Planarian.Tests.Unit.Database;

public sealed class EntityTypeMetadataExtensionsTests
{
    [Fact]
    public void TagReferencesResolveActualNavigationAndIdForeignKeyMetadata()
    {
        using var context = CreateContext();
        var entityType = context.Model.GetRequiredEntityType<ArcheologyTag>();

        var caveReference = entityType.GetRequiredReferenceTo<Cave>();
        Assert.Equal(nameof(ArcheologyTag.Cave), caveReference.Name);
        Assert.Equal(nameof(ArcheologyTag.CaveId),
            caveReference.GetDependentPropertyForPrincipalProperty<string>(nameof(EntityBase.Id)).Name);

        var tagTypeReference = entityType.GetRequiredReferenceTo<TagType>();
        Assert.Equal(nameof(ArcheologyTag.TagType), tagTypeReference.Name);
        Assert.Equal(nameof(ArcheologyTag.TagTypeId),
            tagTypeReference.GetDependentPropertyForPrincipalProperty<string>(nameof(EntityBase.Id)).Name);
    }

    [Fact]
    public void SnapshotForeignKeysWithoutDependentNavigationsAreNotReferences()
    {
        using var context = CreateContext();
        var entityType = context.Model.GetRequiredEntityType<CaveChangeRequest>();

        Assert.Empty(entityType.GetReferencesTo(typeof(County)));
    }

    [Fact]
    public void CompositeReferenceMapsEachPrincipalKeyPropertyToItsDependentProperty()
    {
        using var context = CreateContext();
        var entityType = context.Model.GetRequiredEntityType<MessageLog>();

        var invitationReference = entityType.GetRequiredReferenceTo<AccountUser>();

        Assert.Equal(nameof(MessageLog.AccountInvitation), invitationReference.Name);
        Assert.Equal(nameof(MessageLog.AccountInvitationAccountId),
            invitationReference.GetDependentPropertyForPrincipalProperty<string>(nameof(AccountUser.AccountId)).Name);
        Assert.Equal(nameof(MessageLog.AccountInvitationUserId),
            invitationReference.GetDependentPropertyForPrincipalProperty<string>(nameof(AccountUser.UserId)).Name);
    }

    [Fact]
    public void RequiredReferenceFailsClosedWhenRelationshipDoesNotExist()
    {
        using var context = CreateContext();
        var entityType = context.Model.GetRequiredEntityType<ArcheologyTag>();

        var exception = Assert.Throws<InvalidOperationException>(() => entityType.GetRequiredReferenceTo<Entrance>());

        Assert.Contains(nameof(ArcheologyTag), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Entrance), exception.Message, StringComparison.Ordinal);
    }

    private static PlanarianDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=metadata_only;Password=metadata_only",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new PlanarianDbContext(options);
    }
}
