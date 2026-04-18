using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Generators;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.App.Services;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.FeatureSettings.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Repositories;
using Planarian.Modules.Leads.Repositories;
using Planarian.Modules.Leads.Services;
using Planarian.Modules.Map.Controllers;
using Planarian.Modules.Map.Services;
using Planarian.Modules.Notifications.Hubs;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Photos.Repositories;
using Planarian.Modules.Photos.Services;
using Planarian.Modules.Projects.Repositories;
using Planarian.Modules.Projects.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Settings.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Modules.Tags.Services;
using Planarian.Modules.Trips.Repositories;
using Planarian.Modules.Trips.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Southport.Messaging.Email.Core;
using Southport.Messaging.Email.MailGun;
using FileOptions = Planarian.Shared.Options.FileOptions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestLineSize = 1024 * 1024; // 1MB for entire request line query params on searching with polygons
    serverOptions.Limits.MaxRequestHeadersTotalSize = 1024 * 1024; // 1MB for headers
    serverOptions.Limits.MaxRequestBodySize = 500 * 1024 * 1024; // 200MB for large GeoJSON files
});

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfigConnectionString");

var isDevelopment = builder.Environment.IsDevelopment();

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(appConfigConnectionString)
        .Select(KeyFilter.Any, LabelFilter.Null)
        .Select(KeyFilter.Any,
            isDevelopment ? "Development" : "Production");
});

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.Development.json", false);
#endif


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Configure JSON options for large GeoJSON files
        options.JsonSerializerOptions.MaxDepth = 64; // Increase max depth for complex GeoJSON
        options.JsonSerializerOptions.DefaultBufferSize = 16 * 1024; // 16KB buffer
    });

// Configure form options for large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500MB
    options.ValueLengthLimit = 500 * 1024 * 1024; // 500MB
    options.MemoryBufferThreshold = Int32.MaxValue;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Planarian API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

#region DI

#region Options

var serverOptions = builder.Configuration.GetSection(ServerOptions.Key).Get<ServerOptions>();
if (serverOptions == null) throw new Exception("Server options not found");
builder.Services.AddSingleton(serverOptions);

var authOptions = builder.Configuration.GetSection(AuthOptions.Key).Get<AuthOptions>();
if (authOptions == null) throw new Exception("Auth options not found");
builder.Services.AddSingleton(authOptions);

var blobOptions = builder.Configuration.GetSection(BlobOptions.Key).Get<BlobOptions>();
if (blobOptions == null) throw new Exception("Blob options not found");
builder.Services.AddSingleton(blobOptions);

var emailOptions = builder.Configuration.GetSection(EmailOptions.Key).Get<EmailOptions>();
if (emailOptions == null) throw new Exception("Email options not found");

var fileOptions = builder.Configuration.GetSection(FileOptions.Key).Get<FileOptions>();
if (fileOptions == null) throw new Exception("Email options not found");
builder.Services.AddSingleton(fileOptions);

var requestThrottleOptions =
    builder.Configuration.GetSection(RequestThrottleOptions.Key).Get<RequestThrottleOptions>() ??
    new RequestThrottleOptions();
builder.Services.AddSingleton(requestThrottleOptions);

builder.Services.AddSingleton(Options.Create<MailGunOptions>(emailOptions));
builder.Services.AddSingleton(emailOptions);

#endregion

#region Services

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<RequestThrottleService>();
builder.Services.AddSingleton<ImportUploadAdmissionService>();
builder.Services.AddScoped<ThrottleEventLogService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BlobService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddSingleton<ArchiveJobCoordinator>();
builder.Services.AddScoped<AccountUserManagerService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<CaveService>();
builder.Services.AddScoped<CaveService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<AppService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHttpClient<MjmlService>();
builder.Services.AddSingleton<MemoryCache>();

builder.Services.AddHttpClient<IEmailMessageFactory, MailGunMessageFactory>();

#endregion

#region Repositories

builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<AppRepository>();
builder.Services.AddScoped<TripRepository>();
builder.Services.AddScoped<AuthenticationRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped(typeof(SettingsRepository<>));
builder.Services.AddScoped<LeadRepository>();
builder.Services.AddScoped<PhotoRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped(typeof(TagRepository<>));
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MessageTypeRepository>();
builder.Services.AddScoped<AccountRepository>();
builder.Services.AddScoped(typeof(AccountRepository<>));
builder.Services.AddScoped<PermissionRepository>();
builder.Services.AddScoped<CaveRepository>();
builder.Services.AddScoped(typeof(CaveRepository<>));
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped(typeof(FileRepository<>));
builder.Services.AddScoped<MapService>();
builder.Services.AddScoped<MapRepository>();
builder.Services.AddScoped<TemporaryEntranceRepository>();
builder.Services.AddScoped<FeatureSettingRepository>();


#endregion

#region Http Clients

builder.Services.AddHttpClient<GeologicMapHttpClient>();

#endregion

builder.Services.AddScoped<RequestUser>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

#endregion

#region Database

builder.Services.AddDbContext<PlanarianDbContext>(options =>
{
    options.UseNpgsql(serverOptions.SqlConnectionString, e => e.UseNetTopologySuite());
});

builder.Services.AddDbContextFactory<PlanarianDbContext>(options =>
{
    options.UseNpgsql(serverOptions.SqlConnectionString, e => e.UseNetTopologySuite());
}, ServiceLifetime.Scoped);

builder.Services.AddDbContext<PlanarianDbContextBase>(options =>
{
    options.UseNpgsql(serverOptions.SqlConnectionString, e => e.UseNetTopologySuite());
});

LinqToDBForEFTools.Initialize();
//
// // Convert NetTopologySuite Point to SqlGeometry
// MappingSchema.Default.SetConverter<Point, SqlGeometry>(p =>
// {
//     var sqlGeometry = SqlGeometry.Point(p.X, p.Y, 4326); // Example SRID
//     return sqlGeometry;
// });
//
// // Convert SqlGeometry to NetTopologySuite Point
// MappingSchema.Default.SetConverter<SqlGeometry, Point>(sqlGeometry => new Point(sqlGeometry.STX.Value, sqlGeometry.STY.Value));

#endregion

#region Authentication

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for our hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken))
                // Read the token out of the query string
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((options, tokenService) =>
    {
        options.TokenValidationParameters = tokenService.GetTokenValidationParameters();
    });

builder.Services.AddMvc().AddSessionStateTempDataProvider();
builder.Services.AddSession();

#endregion

#region Authorization

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PermissionPolicyKey.Admin, policy =>
    {
        policy.Requirements.Add(new PermissionRequirement([PermissionKey.PlanarianAdmin, PermissionKey.Admin]));
    });
    options.AddPolicy(PermissionPolicyKey.PlanarianAdmin, policy =>
    {
        policy.Requirements.Add(new PermissionRequirement([PermissionKey.PlanarianAdmin]));
    });
    options.AddPolicy(PermissionPolicyKey.Manager, policy =>
    {
        policy.Requirements.Add(new PermissionRequirement([
            PermissionKey.PlanarianAdmin, PermissionKey.Admin, PermissionKey.Manager
        ]));
    });
    options.AddPolicy(PermissionPolicyKey.View, policy =>
    {
        policy.Requirements.Add(new PermissionRequirement([
            PermissionKey.PlanarianAdmin, PermissionKey.Admin, PermissionKey.Manager, PermissionKey.View
        ]));
    });
    options.AddPolicy(PermissionPolicyKey.Export, policy =>
    {
        policy.Requirements.Add(new PermissionRequirement([
            PermissionPolicyKey.Export
        ]));
    });
});



#endregion

#region Rate Limiting

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var requestThrottleService =
            context.HttpContext.RequestServices.GetRequiredService<RequestThrottleService>();

        int? retryAfterSeconds = null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            headers["Retry-After"] = retryAfterSeconds.Value.ToString();
        }

        await requestThrottleService.RecordEndpointRateLimitHit(context.HttpContext, retryAfterSeconds, cancellationToken);

        await HttpResponseExceptionMiddleware.WriteErrorResponseAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Rate limit exceeded. Please try again later.",
            ApiExceptionType.TooManyRequests,
            headers: headers,
            showContactInfo: true,
            serverOptions: serverOptions);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        httpContext.RequestServices.GetRequiredService<RequestThrottleService>()
            .GetEndpointRateLimitPartition(httpContext));
});

#endregion

#region Compression

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        "application/json",      // all your normal JSON
        "application/geo+json",  // GeoJSON
        "application/gpx+xml"    // GPX files
    };
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(o =>
    o.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o =>
    o.Level = CompressionLevel.Optimal);


#endregion

var app = builder.Build();

app.UseResponseCompression();

if (false)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<PlanarianDbContext>();
    var dataGenerator = new DataGenerator(dbContext);

    await dataGenerator.AddOrUpdateDefaultData();
}

app.UseSession();

app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();
// correct order https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-3.1#middleware-order
app.UseRouting();

var corsOrigins = serverOptions.AllowedCorsOrigins.SplitAndTrim(',').Append(serverOptions.ClientBaseUrl).ToArray();

app.UseCors(x =>
    x.WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition")
);

app.UseAuthentication();
app.UseMiddleware<RequestUserMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();


app.MapHub<NotificationHub>("/api/notificationHub", options => { });


app.UseMiddleware<HttpResponseExceptionMiddleware>();

app.MapControllers();

app.Run();
