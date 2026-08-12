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
        Owner.AddFileSystemPermission(this, path, allowedAccess);

    public LandlockPermissionClaim AllowPort(ushort port, NetworkAccess allowedAccess) =>
        Owner.AddNetworkPermission(this, port, allowedAccess);

    public void Release() => Owner.Release(this);
}
