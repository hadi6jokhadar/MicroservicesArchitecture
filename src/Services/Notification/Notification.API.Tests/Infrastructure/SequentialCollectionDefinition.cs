namespace Notification.API.Tests.Infrastructure;

/// <summary>
/// Collection definition to ensure tests run sequentially
/// This prevents database conflicts when using shared in-memory database
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition
{
}
