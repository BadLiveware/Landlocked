using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockSupportCompatibilityTests
{
    [Fact]
    public void Existing_constructor_and_deconstruction_contract_are_preserved()
    {
        var support = new LandlockSupport(
            LandlockAvailability.Available,
            8,
            FileSystemAccess.AllKnown,
            null)
        {
            SupportedNetworkAccess = NetworkAccess.Tcp,
        };

        var (availability, abiVersion, supportedFileSystemAccess, nativeErrorCode) = support;

        Assert.Equal(LandlockAvailability.Available, availability);
        Assert.Equal(8, abiVersion);
        Assert.Equal(FileSystemAccess.AllKnown, supportedFileSystemAccess);
        Assert.Null(nativeErrorCode);
        Assert.Equal(NetworkAccess.Tcp, support.SupportedNetworkAccess);
    }
}
