namespace Landlocked.DependencyInjection;

public interface ILandlockPermissionRegistry
{
    FileSystemAccess HandledAccess { get; }

    FileSystemAccess HandledFileSystemAccess => HandledAccess;

    NetworkAccess HandledNetworkAccess => NetworkAccess.None;

    LandlockPermissionClaim Claim(string? name = null);
}
