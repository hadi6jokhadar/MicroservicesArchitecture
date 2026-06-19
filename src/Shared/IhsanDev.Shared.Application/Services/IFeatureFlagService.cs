namespace IhsanDev.Shared.Application.Services;

public interface IFeatureFlagService
{
    bool IsEnabled(string flagName, bool defaultValue = false);
}
