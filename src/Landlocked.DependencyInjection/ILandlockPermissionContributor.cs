namespace Landlocked.DependencyInjection;

public interface ILandlockPermissionContributor
{
    void RegisterPermissions(ILandlockPermissionRegistry permissions);
}
