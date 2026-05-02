using IhsanDev.Shared.Kernel.Dto.Tenant;

namespace Nasheed.Infrastructure.Services;

public class NasheedTenantCache : INasheedTenantCache
{
    private TenantInfo? _tenant;
    private readonly TaskCompletionSource _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => _tenant != null;
    public TenantInfo? Tenant => _tenant;

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        => _readyTcs.Task.WaitAsync(cancellationToken);

    public void SetTenant(TenantInfo tenant)
    {
        _tenant = tenant;
        _readyTcs.TrySetResult();
    }
}
