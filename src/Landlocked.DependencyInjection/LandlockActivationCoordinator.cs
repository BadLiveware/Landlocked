namespace Landlocked.DependencyInjection;

internal sealed class LandlockActivationCoordinator
{
    private readonly Lock _sync = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly LandlockRegistrationConfiguration _configuration;
    private LandlockPermissions? _configuredPermissions;
    private LandlockPermissions? _activatedPermissions;
    private Exception? _registrationFailure;
    private bool _isRegistering;

    internal LandlockActivationCoordinator(
        IServiceProvider serviceProvider,
        LandlockRegistrationConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    internal LandlockPermissions Activate()
    {
        lock (_sync)
        {
            if (_activatedPermissions is not null)
            {
                return _activatedPermissions;
            }

            if (_isRegistering)
            {
                throw new InvalidOperationException("Landlock activation cannot be called from a permission contributor.");
            }

            if (_registrationFailure is not null)
            {
                throw _registrationFailure;
            }

            var permissions = _configuredPermissions ?? RegisterPermissions();
            permissions.Activate();
            _configuredPermissions = permissions;
            _activatedPermissions = permissions;
            return permissions;
        }
    }

    private LandlockPermissions RegisterPermissions()
    {
        _isRegistering = true;
        try
        {
            var contributors = _configuration.ResolveContributors(_serviceProvider);
            var permissions = LandlockPermissions.Handle(
                _configuration.HandledFileSystemAccess,
                _configuration.HandledNetworkAccess);
            var registry = new LandlockPermissionRegistry(permissions);
            foreach (var contributor in contributors)
            {
                contributor.RegisterPermissions(registry);
            }

            _configuredPermissions = permissions;
            return permissions;
        }
        catch (Exception exception)
        {
            _registrationFailure = new InvalidOperationException(
                "Landlock permission contributor registration failed and cannot be retried.",
                exception);
            throw _registrationFailure;
        }
        finally
        {
            _isRegistering = false;
        }
    }

    private sealed class LandlockPermissionRegistry(LandlockPermissions permissions) : ILandlockPermissionRegistry
    {
        public FileSystemAccess HandledAccess => HandledFileSystemAccess;

        public FileSystemAccess HandledFileSystemAccess => permissions.HandledFileSystemAccess;

        public NetworkAccess HandledNetworkAccess => permissions.HandledNetworkAccess;

        public LandlockPermissionClaim Claim(string? name = null) => permissions.Claim(name);
    }
}
