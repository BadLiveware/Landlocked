using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockPermissionsTests
{
    [Fact]
    public void Handle_requires_at_least_one_access_right()
    {
        Assert.Throws<ArgumentException>(() => LandlockPermissions.Handle(FileSystemAccess.None));
    }

    [Fact]
    public void Existing_default_handle_call_remains_unambiguous()
    {
        Assert.Throws<ArgumentException>(() => LandlockPermissions.Handle(default));
    }

    [Fact]
    public void Claim_names_must_not_be_blank()
    {
        var permissions = LandlockPermissions.Handle(FileSystemAccess.WriteFile);

        Assert.Throws<ArgumentException>(() => permissions.Claim(" "));
    }

    [Fact]
    public void Claims_can_only_allow_handled_rights()
    {
        var permissions = LandlockPermissions.Handle(FileSystemAccess.WriteFile);
        var claim = permissions.Claim("module");

        Assert.Throws<ArgumentException>(() => claim.Allow(".", FileSystemAccess.ReadFile));
    }

    [Fact]
    public void Claims_can_only_allow_handled_network_rights()
    {
        var permissions = LandlockPermissions.HandleNetwork(NetworkAccess.ConnectTcp);
        var claim = permissions.Claim("module");

        Assert.Throws<ArgumentException>(() => claim.AllowPort(443, NetworkAccess.BindTcp));
    }

    [Fact]
    public void Combined_permissions_expose_both_handled_sets()
    {
        var permissions = LandlockPermissions.Handle(
            FileSystemAccess.WriteFile,
            NetworkAccess.ConnectTcp);

        Assert.Equal(FileSystemAccess.WriteFile, permissions.HandledFileSystemAccess);
        Assert.Equal(NetworkAccess.ConnectTcp, permissions.HandledNetworkAccess);
    }

    [Fact]
    public void Claim_can_be_released_before_activation()
    {
        var permissions = LandlockPermissions.Handle(FileSystemAccess.WriteFile);
        var claim = permissions
            .Claim("module")
            .Allow(".", FileSystemAccess.WriteFile);

        claim.Release();
        claim.Release();

        Assert.True(claim.IsReleased);
        Assert.Throws<InvalidOperationException>(() => claim.Allow(".", FileSystemAccess.WriteFile));
    }
}
