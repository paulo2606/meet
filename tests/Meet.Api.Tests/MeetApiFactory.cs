using Meet.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meet.Api.Tests;

public class MeetApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "test-key-with-at-least-32-characters-secure";

    public MeetApiFactory()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetDbContext>();
        db.Database.Migrate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var built = config.Build();
            var devConnectionString = built.GetConnectionString("Default");
            var testConnectionString = devConnectionString?.Replace(
                "Database=meet;",
                "Database=meet_test;",
                StringComparison.OrdinalIgnoreCase);

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = testConnectionString,
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "meet-api",
                ["Jwt:Audience"] = "meet-web",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["RateLimiting:LoginPermitLimit"] = "1000",
            });
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetDbContext>();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE refresh_tokens, users RESTART IDENTITY CASCADE");
    }
}
