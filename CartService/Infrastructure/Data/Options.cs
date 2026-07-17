namespace CartService.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public bool ApplyMigrationsOnStartup { get; init; }
    public int MigrationRetryCount { get; init; } = 5;
    public int MigrationRetryDelaySeconds { get; init; } = 5;
}

public sealed class CartRulesOptions
{
    public const string SectionName = "CartRules";
    public string DefaultCurrency { get; init; } = "USD";
    public int MaximumDistinctItems { get; init; } = 100;
    public int MaximumQuantityPerItem { get; init; } = 99;
    public int CartExpirationDays { get; init; } = 30;
    public int CheckoutLockMinutes { get; init; } = 10;
    public bool RefreshCartExpirationOnModification { get; init; } = true;
}

public sealed class CartExpirationOptions
{
    public const string SectionName = "CartExpiration";
    public bool Enabled { get; init; } = true;
    public int CheckIntervalMinutes { get; init; } = 10;
    public int BatchSize { get; init; } = 100;
}

public sealed class ExternalServiceOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
}
