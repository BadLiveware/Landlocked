using Microsoft.Extensions.DependencyInjection;

namespace Landlocked.DependencyInjection;

public static class LandlockServiceProviderExtensions
{
    public static LandlockPermissions ActivateLandlock(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return serviceProvider.GetRequiredService<LandlockActivationCoordinator>().Activate();
    }
}
