using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class EmailDeliveryModelConfigurationTests
{
    [Fact]
    public void DeliveryEnumsUseStringProviderConversions()
    {
        using var context = CreateContext();

        AssertStringProviderType<MessageLog>(context.Model, nameof(MessageLog.Purpose));
        AssertStringProviderType<MessageLog>(context.Model, nameof(MessageLog.Provider));
        AssertStringProviderType<MessageLog>(context.Model, nameof(MessageLog.DeliveryStatus));
        AssertStringProviderType<MessageLogEvent>(context.Model, nameof(MessageLogEvent.Provider));
        AssertStringProviderType<MessageLogEvent>(context.Model, nameof(MessageLogEvent.EventType));
    }

    [Fact]
    public void MessageCorrelationAndProviderEventIdentityAreUniquelyIndexed()
    {
        using var context = CreateContext();
        var messageLog = AssertEntity<MessageLog>(context.Model);
        var messageEvent = AssertEntity<MessageLogEvent>(context.Model);

        var correlationIndex = FindIndex(messageLog, nameof(MessageLog.ProviderCorrelationId));
        Assert.True(correlationIndex.IsUnique);
        Assert.Equal("\"ProviderCorrelationId\" IS NOT NULL", correlationIndex.GetFilter());

        var providerEventIndex = FindIndex(messageEvent, nameof(MessageLogEvent.Provider),
            nameof(MessageLogEvent.ProviderDomain), nameof(MessageLogEvent.ProviderEventDay),
            nameof(MessageLogEvent.ProviderEventId));
        Assert.True(providerEventIndex.IsUnique);

        var webhookTokenIndex = FindIndex(messageEvent, nameof(MessageLogEvent.WebhookToken));
        Assert.True(webhookTokenIndex.IsUnique);
        Assert.Equal("\"WebhookToken\" IS NOT NULL", webhookTokenIndex.GetFilter());
    }

    [Fact]
    public void DeliveryRelationshipsUseExpectedDeleteBehavior()
    {
        using var context = CreateContext();

        var user = AssertEntity<User>(context.Model);
        var confirmationFk = FindForeignKey(user, nameof(User.EmailConfirmationMessageLogId));
        Assert.Equal(typeof(MessageLog), confirmationFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.SetNull, confirmationFk.DeleteBehavior);

        var messageLog = AssertEntity<MessageLog>(context.Model);
        var invitationFk = FindForeignKey(messageLog,
            nameof(MessageLog.AccountInvitationAccountId), nameof(MessageLog.AccountInvitationUserId));
        Assert.Equal(DeleteBehavior.SetNull, invitationFk.DeleteBehavior);

        var messageEvent = AssertEntity<MessageLogEvent>(context.Model);
        var eventFk = FindForeignKey(messageEvent, nameof(MessageLogEvent.MessageLogId));
        Assert.Equal(typeof(MessageLog), eventFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, eventFk.DeleteBehavior);
    }

    private static PlanarianDbContextBase CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContextBase>()
            .UseNpgsql("Host=localhost;Database=planarian_model_contract_tests;Username=unused;Password=unused",
                options => options.UseNetTopologySuite())
            .Options;
        return new PlanarianDbContextBase(options);
    }

    private static void AssertStringProviderType<TEntity>(IModel model, string propertyName)
    {
        var property = AssertEntity<TEntity>(model).FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.GetTypeMapping().Converter?.ProviderClrType);
    }

    private static IEntityType AssertEntity<TEntity>(IModel model)
    {
        return Assert.IsAssignableFrom<IEntityType>(model.FindEntityType(typeof(TEntity)));
    }

    private static IIndex FindIndex(IEntityType entityType, params string[] propertyNames)
    {
        return Assert.Single(entityType.GetIndexes().Where(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(propertyNames)));
    }

    private static IForeignKey FindForeignKey(IEntityType entityType, params string[] propertyNames)
    {
        return Assert.Single(entityType.GetForeignKeys().Where(foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(propertyNames)));
    }
}
