namespace Category.API.Tests.Infrastructure;

/// <summary>
/// Forces all tests in the "Sequential" collection to run one at a time,
/// preventing database state conflicts between tests.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollectionDefinition
{
}
