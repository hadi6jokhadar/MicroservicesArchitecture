namespace IhsanDev.Shared.Infrastructure.Persistence;

public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSql;
    public string ConnectionString { get; set; } = string.Empty;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public int MaxRetryDelay { get; set; } = 30;
}

public enum DatabaseProvider
{
    Sqlite,
    PostgreSql
}