using DotNetEnv;
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
    Env.Load(".env");
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

// Strongly typed database configuration.
public class DatabaseOptions
{
    public string DefaultConnection { get; set; } = string.Empty;
}