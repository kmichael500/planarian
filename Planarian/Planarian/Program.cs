using System.Text;
using LinqToDB.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.App.Services;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Repositories;
using Planarian.Modules.Leads.Repositories;
using Planarian.Modules.Leads.Services;
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
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Southport.Messaging.Email.Core;
using Southport.Messaging.Email.SendGrid.Interfaces;
using Southport.Messaging.Email.SendGrid.Message;
using FileOptions = Planarian.Shared.Options.FileOptions;

var builder = WebApplication.CreateBuilder(args);

var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfigConnectionString");

builder.Configuration.AddAzureAppConfiguration(appConfigConnectionString);

var isDevelopment = builder.Environment.IsDevelopment();
if (isDevelopment) builder.Configuration.AddJsonFile("appsettings.Development.json", false);

builder.Services.AddControllers();
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

builder.Services.AddSingleton(Options.Create<SendGridOptions>(emailOptions));
builder.Services.AddSingleton(emailOptions);

#endregion

#region Services

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BlobService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccountService>();
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

builder.Services.AddHttpClient<IEmailMessageFactory, SendGridMessageFactory>();

#endregion

#region Repositories

builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<AppRepository>();
builder.Services.AddScoped<TripRepository>();
builder.Services.AddScoped<AuthenticationRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<LeadRepository>();
builder.Services.AddScoped<PhotoRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MessageTypeRepository>();
builder.Services.AddScoped<AccountRepository>();
builder.Services.AddScoped<CaveRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped<TemporaryEntranceRepository>();

#endregion

builder.Services.AddScoped<RequestUser>();
builder.Services.AddSignalR();

#endregion

#region Database

builder.Services.AddDbContext<PlanarianDbContext>(options =>
{
    options.UseSqlServer(serverOptions.SqlConnectionString, e => e.UseNetTopologySuite());

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
    var tokenValidationParameters = new TokenValidationParameters
    {
        // The signing key must match!
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(authOptions.JwtSecret)),
        // Validate the JWT Issuer (iss) claim
        ValidateIssuer = true,
        ValidIssuer = authOptions.JwtIssuer,
        // Validate the JWT Audience (aud) claim
        ValidateAudience = true,
        ValidAudience = authOptions.JwtIssuer,
        // Validate the token expiry
        ValidateLifetime = true,
        // If you want to allow a certain amount of clock drift, set that here:
        ClockSkew = TimeSpan.Zero
    };

    options.TokenValidationParameters = tokenValidationParameters;

    // Required if using custom token handler
    // options.SecurityTokenValidators.Clear();
    //
    // options.TokenValidationParameters = tokenValidationParameters;
    // options.SecurityTokenValidators.Clear();
    //
    // options.SecurityTokenValidators.Add(new CustomJwtSecurityTokenHandler());
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for our hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken))
            {
                // Read the token out of the query string
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddMvc().AddSessionStateTempDataProvider();
builder.Services.AddSession();

#endregion

var app = builder.Build();

app.UseSession();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// correct order https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-3.1#middleware-order
app.UseRouting();
app.UseCors(x =>
    x.WithOrigins(serverOptions.ClientBaseUrl)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
);

app.UseAuthentication();
app.UseAuthorization();


app.MapHub<NotificationHub>("/api/notificationHub", options =>
{
});


app.UseMiddleware<HttpResponseExceptionMiddleware>();

app.MapControllers();

app.Run();

