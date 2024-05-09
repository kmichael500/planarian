using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Model.Interceptors;

public class SaveChangesInterceptor : ISaveChangesInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        eventData.Context?.ChangeTracker.DetectChanges();
        if (eventData.Context == null) return new ValueTask<InterceptionResult<int>>(result);

        var context = (PlanarianDbContext)eventData.Context;
        foreach (var entity in eventData.Context.ChangeTracker.Entries())
            if (entity.Entity.GetType().BaseType == typeof(EntityBase) ||
                entity.Entity.GetType().BaseType == typeof(EntityBaseNameId))
                switch (entity.State)
                {
                    case EntityState.Added:
                        ((EntityBase)entity.Entity).CreatedOn = DateTime.UtcNow;
                        ((EntityBase)entity.Entity).CreatedByUserId = !string.IsNullOrWhiteSpace(context?.RequestUser?.Id)
                            ? context.RequestUser.Id
                            : null;
                        ((EntityBase)entity.Entity).Id = !string.IsNullOrWhiteSpace(((EntityBase)entity.Entity).Id) &&
                                                         ((EntityBase)entity.Entity).Id.Length == PropertyLength.Id
                            ? ((EntityBase)entity.Entity).Id // use the provided id if it matches our id format
                            : IdGenerator.Generate();
                        break;
                    case EntityState.Modified:
                        ((EntityBase)entity.Entity).ModifiedOn = DateTime.UtcNow;
                        ((EntityBase)entity.Entity).ModifiedByUserId =
                            !string.IsNullOrWhiteSpace(context?.RequestUser?.Id)
                                ? context.RequestUser.Id
                                : null;
                        break;
                    case EntityState.Detached:
                        break;
                    case EntityState.Unchanged:
                        break;
                    case EntityState.Deleted:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

        return new ValueTask<InterceptionResult<int>>(result);
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = new())
    {
        return new ValueTask<int>(result);
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }
}