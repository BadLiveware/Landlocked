using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Landlocked.LowLevel;

namespace Landlocked;

public readonly record struct LandlockSupport(
    LandlockAvailability Availability,
    int AbiVersion,
    FileSystemAccess SupportedFileSystemAccess,
    int? NativeErrorCode)
{
    public NetworkAccess SupportedNetworkAccess { get; init; }

    [SupportedOSPlatformGuard("linux")]
    public bool IsAvailable => Availability == LandlockAvailability.Available;

    public string Reason
    {
        get
        {
            switch (Availability)
            {
                case LandlockAvailability.Available:
                    return $"Landlock ABI {AbiVersion} is available with process-wide synchronization.";
                case LandlockAvailability.UnsupportedOperatingSystem:
                    return "Landlock is available only on Linux.";
                case LandlockAvailability.UnsupportedArchitecture:
                    return $"Landlocked does not support the {RuntimeInformation.ProcessArchitecture} architecture.";
                case LandlockAvailability.KernelUnavailable:
                    if (NativeErrorCode.HasValue)
                    {
                        return $"The kernel did not provide Landlock (errno {NativeErrorCode.Value}).";
                    }

                    return "The kernel did not provide Landlock.";
                case LandlockAvailability.ProcessSynchronizationUnavailable:
                    return $"Landlock ABI {AbiVersion} is available, but ABI {Landlock.MinimumProcessSynchronizationAbi} or newer is required to restrict every existing CLR thread.";
                default:
                    return "Landlock support could not be determined.";
            }
        }
    }

    internal static LandlockSupport Detect()
    {
        if (!OperatingSystem.IsLinux())
        {
            return new LandlockSupport(
                LandlockAvailability.UnsupportedOperatingSystem,
                0,
                FileSystemAccess.None,
                null);
        }

        var architecture = RuntimeInformation.ProcessArchitecture;
        if (architecture != Architecture.X64 && architecture != Architecture.Arm64)
        {
            return new LandlockSupport(
                LandlockAvailability.UnsupportedArchitecture,
                0,
                FileSystemAccess.None,
                null);
        }

        var abi = LandlockApi.QueryAbiVersion();
        if (abi.Value < 0)
        {
            return new LandlockSupport(
                LandlockAvailability.KernelUnavailable,
                0,
                FileSystemAccess.None,
                abi.ErrorCode);
        }

        var supportedFileSystemAccess = SupportedFileSystemAccessForAbi(abi.Value);
        var supportedNetworkAccess = SupportedNetworkAccessForAbi(abi.Value);
        if (abi.Value < Landlock.MinimumProcessSynchronizationAbi)
        {
            return new LandlockSupport(
                LandlockAvailability.ProcessSynchronizationUnavailable,
                abi.Value,
                supportedFileSystemAccess,
                null)
            {
                SupportedNetworkAccess = supportedNetworkAccess,
            };
        }

        return new LandlockSupport(
            LandlockAvailability.Available,
            abi.Value,
            supportedFileSystemAccess,
            null)
        {
            SupportedNetworkAccess = supportedNetworkAccess,
        };
    }

    internal static FileSystemAccess SupportedFileSystemAccessForAbi(int abiVersion)
    {
        var supported = FileSystemAccess.Execute |
                        FileSystemAccess.WriteFile |
                        FileSystemAccess.ReadFile |
                        FileSystemAccess.ReadDirectory |
                        FileSystemAccess.RemoveDirectory |
                        FileSystemAccess.RemoveFile |
                        FileSystemAccess.MakeCharacterDevice |
                        FileSystemAccess.MakeDirectory |
                        FileSystemAccess.MakeRegularFile |
                        FileSystemAccess.MakeUnixSocket |
                        FileSystemAccess.MakeFifo |
                        FileSystemAccess.MakeBlockDevice |
                        FileSystemAccess.MakeSymbolicLink;

        if (abiVersion >= 2)
        {
            supported |= FileSystemAccess.Refer;
        }

        if (abiVersion >= 3)
        {
            supported |= FileSystemAccess.Truncate;
        }

        if (abiVersion >= 5)
        {
            supported |= FileSystemAccess.IoctlDevice;
        }

        if (abiVersion >= 9)
        {
            supported |= FileSystemAccess.ResolveUnixSocket;
        }

        return supported;
    }

    internal static NetworkAccess SupportedNetworkAccessForAbi(int abiVersion)
    {
        var supported = NetworkAccess.None;
        if (abiVersion >= 4)
        {
            supported |= NetworkAccess.Tcp;
        }
        if (abiVersion >= 10)
        {
            supported |= NetworkAccess.Udp;
        }

        return supported;
    }
}
