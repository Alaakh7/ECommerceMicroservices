namespace OrderService.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool ApplyMigrationsOnStartup { get; init; }
    public int MigrationRetryCount { get; init; } = 5;
    public int MigrationRetryDelaySeconds { get; init; } = 5;
}

public sealed class OrderRulesOptions
{
    public const string SectionName = "OrderRules";
    public string DefaultCurrency { get; init; } = "USD";
    public int MaximumItemsPerOrder { get; init; } = 100;
    public int MaximumQuantityPerItem { get; init; } = 99;
}

public sealed class OrderRecoveryOptions
{
    public const string SectionName = "OrderRecovery";
    public bool Enabled { get; init; } = true;
    public int CheckIntervalSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 50;
    public int MaximumRetryCount { get; init; } = 10;
    public int InitialRetryDelaySeconds { get; init; } = 10;
    public int MaximumRetryDelayMinutes { get; init; } = 30;
}

public sealed class ExternalServiceOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
}
