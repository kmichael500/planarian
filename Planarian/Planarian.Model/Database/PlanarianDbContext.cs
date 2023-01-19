using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.TripObjectives;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanarianDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_changesInterceptor);
    }
    
    public DbSet<PermissionType> PermissionTypes { get; set; } = null!;
    public DbSet<TripPhoto> Photos { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectInvitation> ProjectInvitations { get; set; } = null!;
    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;
    public DbSet<Trip> Trip { get; set; } = null!;
    public DbSet<Lead> Lead { get; set; } = null!;
    public DbSet<TripObjective> TripObjectives { get; set; } = null!;
    public DbSet<TripObjectiveTag> TripObjectiveTag { get; set; } = null!;
    public DbSet<TripObjectiveMember> TripObjectiveMembers { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<MessageType> MessageTypes { get; set; } = null!;
    public DbSet<MessageLog> MessageLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
}