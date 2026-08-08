using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Database;

/// <summary>
/// Adds the revision/workflow account filters as a finalizing convention so
/// they compose with the existing model configuration without replacing any
/// existing entity configuration.
/// </summary>
public partial class PlanarianDbContext
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new RevisionTenantFilterConvention(this));
    }

    private sealed class RevisionTenantFilterConvention(PlanarianDbContext context) : IModelFinalizingConvention
    {
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
            IConventionContext<IConventionModelBuilder> conventionContext)
        {
            SetFilter<CaveRevision>(modelBuilder, row =>
                context.RequestUser.AccountId != null && row.AccountId == context.RequestUser.AccountId);
            SetFilter<CaveImportBatch>(modelBuilder, row =>
                context.RequestUser.AccountId != null && row.AccountId == context.RequestUser.AccountId);
            SetFilter<CaveChangeRequest>(modelBuilder, row =>
                context.RequestUser.AccountId != null && row.AccountId == context.RequestUser.AccountId);
            SetFilter<CaveProposalVersion>(modelBuilder, row =>
                context.RequestUser.AccountId != null && row.AccountId == context.RequestUser.AccountId);
            SetFilter<CaveChangeRequestStagedFile>(modelBuilder, row =>
                context.RequestUser.AccountId != null && row.AccountId == context.RequestUser.AccountId);
        }

        private static void SetFilter<TEntity>(IConventionModelBuilder modelBuilder,
            Expression<Func<TEntity, bool>> filter) where TEntity : class
        {
            var entityType = modelBuilder.Metadata.FindEntityType(typeof(TEntity))
                             ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is missing from the EF model.");
            entityType.SetQueryFilter(filter);
        }
    }
}
