namespace CustomerService.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool ApplyMigrationsOnStartup { get; init; }
    public bool SeedData { get; init; }
    public int MigrationRetryCount { get; init; } = 5;
    public int MigrationRetryDelaySeconds { get; init; } = 5;
}
