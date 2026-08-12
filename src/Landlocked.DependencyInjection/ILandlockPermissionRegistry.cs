namespace Landlocked.DependencyInjection;

public interface ILandlockPermissionRegistry
{
    FileSystemAccess HandledAccess { get; }

    FileSystemAccess HandledFileSystemAccess { get; }

    NetworkAccess HandledNetworkAccess { get; }

    LandlockPermissionClaim Claim(string? name = null);
}
