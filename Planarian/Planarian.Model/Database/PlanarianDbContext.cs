using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.RidgeWalker.Views;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Database.Extensions;
using Planarian.Model.Interceptors;
using Planarian.Model.Shared;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Model.Database;

public class PlanarianDbContextBase : DbContext
{
    protected readonly SaveChangesInterceptor ChangesInterceptor = new();
    public RequestUser RequestUser = null!;

    public PlanarianDbContextBase(DbContextOptions<PlanarianDbContextBase> contextOptions) : base(contextOptions)
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
    public DbSet<UserPermission> UserPermissions { get; set; } = null!;

    #endregion

    #region Member

    public DbSet<Member> Members { get; set; } = null!;

    #endregion

    #region Project

    public DbSet<Project> Projects { get; set; } = null!;

    #endregion

    #region Cave

    #region Views

    public DbSet<UserCavePermissionsView> UserCavePermissionView { get; set; } = null!;

    #endregion
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountState> AccountStates { get; set; } = null!;
    public DbSet<AccountUser> AccountUsers { get; set; } = null!;
    public DbSet<ArcheologyTag> ArcheologyTags { get; set; } = null!;
    public DbSet<BiologyTag> BiologyTags { get; set; } = null!;
    public DbSet<CartographerNameTag> CartographerNameTags { get; set; } = null!;
    public DbSet<Cave> Caves { get; set; } = null!;
    
    public DbSet<CaveChangeLog> CaveChangeLog { get; set; }
    public DbSet<CaveChangeRequest> CaveChangeRequests { get; set; }
    public DbSet<CaveChangeRequest> CaveChangeLogs { get; set; }

    public DbSet<CaveGeoJson> CaveGeoJsons { get; set; } = null!;
    public DbSet<CaveOtherTag> CaveOtherTags { get; set; } = null!;
    
    public DbSet<CavePermission> CavePermissions { get; set; } = null!;
    
    public DbSet<CaveReportedByNameTag> CaveReportedByNameTags { get; set; } = null!;
    public DbSet<County> Counties { get; set; } = null!;
    public DbSet<Entrance> Entrances { get; set; } = null!;
    public DbSet<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } = null!;
    public DbSet<EntranceOtherTag> EntranceOtherTag { get; set; } = null!;
    public DbSet<EntranceReportedByNameTag> EntranceReportedByNameTags { get; set; } = null!;
    public DbSet<EntranceStatusTag> EntranceStatusTags { get; set; } = null!;
    public DbSet<Favorite> Favorites { get; set; } = null!;
    public DbSet<FeatureSetting> FeatureSettings { get; set; } = null!;
    public DbSet<FieldIndicationTag> FieldIndicationTags { get; set; } = null!;
    public DbSet<File> Files { get; set; } = null!;
    public DbSet<GeologicAgeTag> GeologicAgeTags { get; set; } = null!;
    public DbSet<GeologyTag> GeologyTags { get; set; } = null!;
    public DbSet<MapStatusTag> MapStatusTags { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<PhysiographicProvinceTag> PhysiographicProvinceTags { get; set; } = null!;
    public DbSet<State> States { get; set; } = null!;

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    { 
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanarianDbContextBase).Assembly);
        
        var tsSimple = typeof(FullTextSearchExtensions).GetMethod(
            nameof(FullTextSearchExtensions.TsHeadlineSimple),
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) });
        
        if (tsSimple == null)
            throw new InvalidOperationException("Could not find ts_headline_simple method.");
        
        modelBuilder.HasDbFunction(tsSimple)       
            .HasName("ts_headline_simple")     
            .HasSchema("public");            
        
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.AddInterceptors(ChangesInterceptor);
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

public class PlanarianDbContext : PlanarianDbContextBase
{
    public PlanarianDbContext(DbContextOptions<PlanarianDbContextBase> contextOptions) : base(contextOptions)
    {
    }


    // on model creating
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cave>().HasQueryFilter(c =>
            c.AccountId == RequestUser.AccountId &&
            UserCavePermissionView.Any(ucp =>
                ucp.AccountId == RequestUser.AccountId &&
                ucp.UserId == RequestUser.Id &&
                ucp.CaveId == c.Id)
        );

    }
}