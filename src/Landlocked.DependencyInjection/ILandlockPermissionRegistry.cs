namespace Landlocked.DependencyInjection;

public interface ILandlockPermissionRegistry
{
    FileSystemAccess HandledAccess { get; }

    FileSystemAccess HandledFileSystemAccess => HandledAccess;

    /// <summary>
    /// Gets the network rights handled by the registry. Custom registry implementations must override this member
    /// when they handle network access; the default preserves compatibility and denies all network allowances.
    /// </summary>
    NetworkAccess HandledNetworkAccess => NetworkAccess.None;

    LandlockPermissionClaim Claim(string? name = null);
}
