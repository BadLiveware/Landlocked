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
