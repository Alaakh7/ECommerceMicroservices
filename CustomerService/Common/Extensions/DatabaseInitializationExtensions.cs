using CustomerService.Application.Services;
using CustomerService.Application.Validation;
using CustomerService.Domain.Entities;
using CustomerService.Domain.Enums;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CustomerService.Common.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeCustomerDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");

        if (options.ApplyMigrationsOnStartup)
        {
            Exception? last = null;
            for (var attempt = 1; attempt <= Math.Max(1, options.MigrationRetryCount); attempt++)
            {
                try
                {
                    logger.LogInformation("Applying customer database migrations (attempt {Attempt}/{MaximumAttempts})", attempt, options.MigrationRetryCount);
                    await db.Database.MigrateAsync(cancellationToken);
                    last = null;
                    break;
                }
                catch (Exception exception) when (attempt < options.MigrationRetryCount)
                {
                    last = exception;
                    logger.LogWarning("Customer database migration attempt {Attempt} failed", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(options.MigrationRetryDelaySeconds), cancellationToken);
                }
            }
            if (last is not null) throw new InvalidOperationException("Customer database migrations failed after all retry attempts.", last);
        }
        if (options.SeedData && !app.Environment.IsProduction()) await SeedAsync(db, cancellationToken);
    }

    private static async Task SeedAsync(CustomerDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            ("CUS-SEED000001", "Omar", "Hassan", "omar.hassan@example.com", "Riyadh", "SA"),
            ("CUS-SEED000002", "Lina", "Ahmad", "lina.ahmad@example.com", "Amman", "JO"),
            ("CUS-SEED000003", "Samer", "Ali", "samer.ali@example.com", "Dubai", "AE")
        };
        foreach (var seed in seeds)
        {
            var normalized = EmailNormalizer.Normalize(seed.Item4);
            if (await db.Customers.AnyAsync(x => x.NormalizedEmail == normalized.NormalizedEmail, cancellationToken)) continue;
            var now = DateTimeOffset.UtcNow;
            var customer = new Customer
            {
                Id = Guid.NewGuid(), CustomerNumber = seed.Item1, FirstName = seed.Item2, LastName = seed.Item3,
                Email = normalized.Email, NormalizedEmail = normalized.NormalizedEmail, Status = CustomerStatus.Active,
                CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
            };
            customer.Addresses.Add(new CustomerAddress
            {
                Id = Guid.NewGuid(), CustomerId = customer.Id, Customer = customer, Label = "Home", RecipientName = $"{seed.Item2} {seed.Item3}",
                AddressLine1 = "Example Street 1", City = seed.Item5, CountryCode = seed.Item6,
                IsDefaultShipping = true, IsDefaultBilling = true, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
            });
            db.Customers.Add(customer);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
