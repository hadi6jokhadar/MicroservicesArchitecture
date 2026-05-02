namespace Nasheed.API.Tests.Infrastructure;

/// <summary>
/// Collection definition to force sequential test execution.
/// This prevents database conflicts when tests run in parallel.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition
{
}
