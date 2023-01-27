using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Interceptors;
using Planarian.Model.Shared;

namespace Planarian.Model.Database;

public class PlanarianDbContext : DbContext
{
    private readonly SaveChangesInterceptor _changesInterceptor = new();
    public RequestUser RequestUser = null!;

    public PlanarianDbContext(DbContextOptions<PlanarianDbContext> contextOptions) : base(contextOptions)
    {
    }

    #region Project
    
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    
    #endregion

    #region Trip

    public DbSet<Trip> Trips { get; set; } = null!;
    public DbSet<TripTag> TripTags { get; set; } = null!;
    public DbSet<TripMember> TripMembers { get; set; } = null!;

    #endregion

    #region Lead

    public DbSet<Lead> Leads { get; set; } = null!;
    public DbSet<LeadTag> LeadTags { get; set; } = null!;

    #endregion

    #region Photo

    public DbSet<Photo> Photos { get; set; } = null!;

    #endregion

    #region Tag

    public DbSet<Tag> Tags { get; set; } = null!;

    #endregion

    #region Message

    public DbSet<MessageType> MessageTypes { get; set; } = null!;
    public DbSet<MessageLog> MessageLogs { get; set; } = null!;

    #endregion

    #region User

    public DbSet<User> Users { get; set; } = null!;

    #endregion


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanarianDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_changesInterceptor);
    }
}