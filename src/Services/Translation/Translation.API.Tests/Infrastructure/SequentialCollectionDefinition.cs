namespace Translation.API.Tests.Infrastructure;

/// <summary>
/// Collection definition to ensure tests run sequentially
/// This prevents database conflicts when tests share the same database instance
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition
{
}
