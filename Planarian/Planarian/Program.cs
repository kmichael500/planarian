using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Leads.Controllers;
using Planarian.Modules.Project.Repositories;
using Planarian.Modules.Project.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Settings.Services;
using Planarian.Modules.Tags.Services;
using Planarian.Modules.TripObjectives.Repositories;
using Planarian.Modules.TripObjectives.Services;
using Planarian.Modules.TripPhotos.Controllers;
using Planarian.Modules.Trips.Repositories;
using Planarian.Modules.Trips.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Email;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Southport.Messaging.Email.Core;
using Southport.Messaging.Email.SendGrid.Interfaces;
using Southport.Messaging.Email.SendGrid.Message;

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

builder.Services.AddSingleton(Options.Create<SendGridOptions>(emailOptions));
builder.Services.AddSingleton(emailOptions);

#endregion

#region Services

builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<TripObjectiveService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<BlobService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<TripPhotoService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<MjmlService>();
builder.Services.AddSingleton<MemoryCache>();

builder.Services.AddHttpClient<IEmailMessageFactory, SendGridMessageFactory>();

#endregion

#region Repositories

builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<TripRepository>();
builder.Services.AddScoped<TripObjectiveRepository>();
builder.Services.AddScoped<AuthenticationRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped<LeadRepository>();
builder.Services.AddScoped<TripPhotoRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MessageTypeRepository>();

#endregion

builder.Services.AddScoped<RequestUser>();

#endregion

#region Database

builder.Services.AddDbContext<PlanarianDbContext>(options =>
{
    options.UseSqlServer(serverOptions.SqlConnectionString);
});

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

app.UseAuthentication();
app.UseAuthorization();
app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(o => true));

app.UseMiddleware<HttpResponseExceptionMiddleware>();

app.MapControllers();

app.Run();