namespace Backup.Infrastructure.Options;

/// <summary>
/// One entry of the <c>Backup:GlobalTargets</c> configuration array — a service's global
/// database connection string, keyed by <see cref="ServiceName"/>.
/// </summary>
public class GlobalTargetOptions
{
    public string ServiceName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}
