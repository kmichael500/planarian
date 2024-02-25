using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Interceptors;
using Planarian.Model.Shared;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Model.Database;

public class PlanarianDbContext : DbContext
{
    private readonly SaveChangesInterceptor _changesInterceptor = new();
    public RequestUser RequestUser = null!;

    public PlanarianDbContext(DbContextOptions<PlanarianDbContext> contextOptions) : base(contextOptions)
    {
    }

    #region Photo

    public DbSet<Photo> Photos { get; set; } = null!;

    #endregion

    #region Tag

    public DbSet<TagType> TagTypes { get; set; } = null!;

    #endregion

    #region User

    public DbSet<User> Users { get; set; } = null!;

    #endregion

    #region Member

    public DbSet<Member> Members { get; set; } = null!;

    #endregion

    #region Project

    public DbSet<Project> Projects { get; set; } = null!;

    #endregion

    #region RidgeWalker

    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountState> AccountStates { get; set; } = null!;
    public DbSet<AccountUser> AccountUsers { get; set; } = null!;
    public DbSet<Cave> Caves { get; set; } = null!;
    public DbSet<County> Counties { get; set; } = null!;
    public DbSet<Entrance> Entrances { get; set; } = null!;
    public DbSet<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } = null!;
    public DbSet<EntranceStatusTag> EntranceStatusTags { get; set; } = null!;
    public DbSet<FieldIndicationTag> FieldIndicationTags { get; set; } = null!;
    public DbSet<GeologyTag> GeologyTags { get; set; } = null!;
    public DbSet<File> Files { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanarianDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.AddInterceptors(_changesInterceptor);
    }

    #region Trip

    public DbSet<Trip> Trips { get; set; } = null!;
    public DbSet<TripTag> TripTags { get; set; } = null!;

    #endregion

    #region Lead

    public DbSet<Lead> Leads { get; set; } = null!;
    public DbSet<LeadTag> LeadTags { get; set; } = null!;

    #endregion

    #region Message

    public DbSet<MessageType> MessageTypes { get; set; } = null!;
    public DbSet<MessageLog> MessageLogs { get; set; } = null!;

    #endregion
}