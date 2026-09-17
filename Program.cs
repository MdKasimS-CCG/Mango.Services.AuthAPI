using Microsoft.Extensions.Options;
using Mango.MessageBus;
using Mango.Services.AuthAPI.Data;
using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service;
using Mango.Services.AuthAPI.Service.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")
    ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

var profile = Environment.GetEnvironmentVariable("MANGO_PROFILE");

if (string.IsNullOrWhiteSpace(profile))
{
    profile = isDocker ? "Docker" : "Http";
}

if (!isDocker)
{
    LoadEnvFile(".env");
}

var builder = WebApplication.CreateBuilder(args);

// Select configuration values based on the active profile.
builder.Configuration.AddEnvironmentVariables(
    prefix: $"{profile}__");

// Database Options
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider
        .GetRequiredService<IOptions<DatabaseOptions>>()
        .Value;

    options.UseSqlServer(databaseOptions.DefaultConnection);
});

// JWT Options
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("ApiSettings"));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IMessageProducer, RabbitMQMessageProducer>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

ApplyMigration();

app.Run();


// Apply pending database migrations.
void ApplyMigration()
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        if (db.Database.GetPendingMigrations().Any())
        {
            db.Database.Migrate();
        }
    }
}


// Load .env file for local execution without requiring an
// additional NuGet package.
void LoadEnvFile(string fileName)
{
    var envPath = Path.Combine(
    Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
    fileName);

    if (!File.Exists(envPath))
    {
        throw new FileNotFoundException(
            $"The environment file '{fileName}' was not found.",
            envPath);
    }

    foreach (var line in File.ReadLines(envPath))
    {
        var trimmedLine = line.Trim();

        // Ignore blank lines and comments.
        if (string.IsNullOrWhiteSpace(trimmedLine) ||
            trimmedLine.StartsWith("#"))
        {
            continue;
        }

        // Support optional "export KEY=value".
        if (trimmedLine.StartsWith("export "))
        {
            trimmedLine = trimmedLine["export ".Length..].Trim();
        }

        var separatorIndex = trimmedLine.IndexOf('=');

        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmedLine[..separatorIndex].Trim();
        var value = trimmedLine[(separatorIndex + 1)..].Trim();

        // Remove surrounding quotes if present.
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}


// Strongly typed database configuration.
public class DatabaseOptions
{
    public string DefaultConnection { get; set; } = string.Empty;
}