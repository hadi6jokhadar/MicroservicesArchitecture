namespace IhsanDev.Shared.Testing.Helpers;

/// <summary>
/// Extension methods and helpers for testing
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Generate a unique email address for testing
    /// </summary>
    public static string GenerateUniqueEmail(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@example.com";
    }

    /// <summary>
    /// Generate a unique string for testing
    /// </summary>
    public static string GenerateUniqueString(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Generate a unique integer for testing
    /// </summary>
    public static int GenerateUniqueInt()
    {
        return Random.Shared.Next(100000, 999999);
    }

    /// <summary>
    /// Wait for a condition to be true with timeout
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null)
    {
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(interval);
        }

        return false;
    }

    /// <summary>
    /// Wait for a condition to be true with timeout (synchronous version)
    /// </summary>
    public static bool WaitForCondition(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null)
    {
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(100);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(interval);
        }

        return false;
    }
}
