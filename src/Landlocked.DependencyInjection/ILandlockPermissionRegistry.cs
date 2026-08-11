namespace Landlocked.DependencyInjection;

public interface ILandlockPermissionRegistry
{
    FileSystemAccess HandledAccess { get; }

    LandlockPermissionClaim Claim(string? name = null);
}
