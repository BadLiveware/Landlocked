namespace Landlocked;

public sealed class LandlockPermissionClaim
{
    internal LandlockPermissionClaim(
        LandlockPermissions owner,
        string? name,
        LandlockPolicy policy)
    {
        Owner = owner;
        Name = name;
        Policy = policy;
    }

    public string? Name { get; }

    public bool IsReleased => Owner.IsReleased(this);

    internal LandlockPermissions Owner { get; }

    internal LandlockPolicy Policy { get; set; }

    internal bool IsReleasedCore { get; set; }

    public LandlockPermissionClaim Allow(string path, FileSystemAccess allowedAccess) =>
        Owner.AddPermission(this, path, allowedAccess);

    public void Release() => Owner.Release(this);
}
