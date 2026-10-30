using Microsoft.EntityFrameworkCore.Metadata;

namespace Planarian.Model.Database.Extensions;

public static class EntityTypeMetadataExtensions
{
    public static IEntityType GetRequiredEntityType<T>(this IModel model)
        where T : class
    {
        return model.FindEntityType(typeof(T))
               ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped by EF Core.");
    }

    public static IReadOnlyList<INavigation> GetReferencesTo(this IEntityType entityType, Type principalClrType)
    {
        return entityType.GetNavigations()
            .Where(navigation => navigation.IsOnDependent &&
                                 navigation.TargetEntityType.ClrType == principalClrType)
            .ToArray();
    }

    public static INavigation GetRequiredReferenceTo<TPrincipal>(this IEntityType entityType)
        where TPrincipal : class
    {
        var references = entityType.GetReferencesTo(typeof(TPrincipal));
        if (references.Count != 1)
            throw new InvalidOperationException(
                $"Expected exactly one dependent reference from {entityType.ClrType.Name} to {typeof(TPrincipal).Name}, " +
                $"but found {references.Count}.");

        return references[0];
    }

    public static IProperty GetDependentPropertyForPrincipalProperty<TProperty>(this INavigation navigation,
        string principalPropertyName)
    {
        var foreignKey = navigation.ForeignKey;
        var principalPropertyIndex = -1;
        for (var index = 0; index < foreignKey.PrincipalKey.Properties.Count; index++)
            if (foreignKey.PrincipalKey.Properties[index].Name == principalPropertyName)
            {
                principalPropertyIndex = index;
                break;
            }

        if (principalPropertyIndex < 0)
            throw new InvalidOperationException(
                $"Principal key for {navigation.TargetEntityType.ClrType.Name} does not contain {principalPropertyName}.");
        if (foreignKey.Properties.Count != foreignKey.PrincipalKey.Properties.Count)
            throw new InvalidOperationException(
                $"Foreign-key metadata for {navigation.DeclaringEntityType.ClrType.Name}.{navigation.Name} is inconsistent.");

        var dependentProperty = foreignKey.Properties[principalPropertyIndex];
        if (dependentProperty.ClrType != typeof(TProperty))
            throw new InvalidOperationException(
                $"Dependent property {dependentProperty.Name} on {navigation.DeclaringEntityType.ClrType.Name} " +
                $"is {dependentProperty.ClrType.Name}, not {typeof(TProperty).Name}.");

        return dependentProperty;
    }
}
