using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockSupportCompatibilityTests
{
    public static TheoryData<int, FileSystemAccess, NetworkAccess> AbiAccessCases => new()
    {
        { 1, FileSystemAccess.AllKnown & ~(FileSystemAccess.Refer | FileSystemAccess.Truncate | FileSystemAccess.IoctlDevice | FileSystemAccess.ResolveUnixSocket), NetworkAccess.None },
        { 2, FileSystemAccess.AllKnown & ~(FileSystemAccess.Truncate | FileSystemAccess.IoctlDevice | FileSystemAccess.ResolveUnixSocket), NetworkAccess.None },
        { 3, FileSystemAccess.AllKnown & ~(FileSystemAccess.IoctlDevice | FileSystemAccess.ResolveUnixSocket), NetworkAccess.None },
        { 4, FileSystemAccess.AllKnown & ~(FileSystemAccess.IoctlDevice | FileSystemAccess.ResolveUnixSocket), NetworkAccess.Tcp },
        { 5, FileSystemAccess.AllKnown & ~FileSystemAccess.ResolveUnixSocket, NetworkAccess.Tcp },
        { 9, FileSystemAccess.AllKnown, NetworkAccess.Tcp },
        { 10, FileSystemAccess.AllKnown, NetworkAccess.AllKnown },
    };

    [Theory]
    [MemberData(nameof(AbiAccessCases))]
    public void Supported_access_matches_each_ABI_threshold(
        int abiVersion,
        FileSystemAccess expectedFileSystemAccess,
        NetworkAccess expectedNetworkAccess)
    {
        Assert.Equal(expectedFileSystemAccess, LandlockSupport.SupportedFileSystemAccessForAbi(abiVersion));
        Assert.Equal(expectedNetworkAccess, LandlockSupport.SupportedNetworkAccessForAbi(abiVersion));
    }

    [Fact]
    public void Process_synchronization_reason_uses_the_minimum_supported_ABI()
    {
        var support = new LandlockSupport(
            LandlockAvailability.ProcessSynchronizationUnavailable,
            Landlock.MinimumProcessSynchronizationAbi - 1,
            FileSystemAccess.AllKnown,
            null);

        Assert.Contains($"ABI {Landlock.MinimumProcessSynchronizationAbi} or newer", support.Reason, StringComparison.Ordinal);
    }

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
