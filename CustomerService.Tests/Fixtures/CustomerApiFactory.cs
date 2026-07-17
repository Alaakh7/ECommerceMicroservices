using CustomerService.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CustomerService.Tests.Fixtures;

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Database:SeedData", "false");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerDbContext>>();
            services.RemoveAll<CustomerDbContext>();
            services.AddDbContext<CustomerDbContext>(options => options.UseSqlite(connection));
            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}
