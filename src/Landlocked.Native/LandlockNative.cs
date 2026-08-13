using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Landlocked.Native;

/// <summary>Direct bindings to the Linux Landlock userspace ABI.</summary>
/// <remarks>
/// Methods return the kernel result unchanged. On failure, the result is <c>-1</c> and callers can read
/// <see cref="Marshal.GetLastPInvokeError()"/> immediately. This type does not own descriptors, retry interrupted
/// operations, validate ABI support, or coordinate restrictions across threads.
/// </remarks>
[SupportedOSPlatform("linux")]
public static partial class LandlockNative
{
    public const nint OpenAt2SystemCall = 437;
    public const nint LandlockCreateRulesetSystemCall = 444;
    public const nint LandlockAddRuleSystemCall = 445;
    public const nint LandlockRestrictSelfSystemCall = 446;

    public const uint CreateRulesetVersion = 1U << 0;
    public const uint PathBeneathRule = 1;
    public const uint NetworkPortRule = 2;
    public const uint RestrictSelfThreadSync = 1U << 3;

    public const int CurrentWorkingDirectory = -100;
    public const ulong OpenPath = 0x200000;
    public const ulong OpenCloseOnExec = 0x80000;
    public const ulong ResolveNoSymbolicLinks = 0x04;
    public const int SetNoNewPrivileges = 38;

    public const ulong AccessFileSystemExecute = 1UL << 0;
    public const ulong AccessFileSystemWriteFile = 1UL << 1;
    public const ulong AccessFileSystemReadFile = 1UL << 2;
    public const ulong AccessFileSystemReadDirectory = 1UL << 3;
    public const ulong AccessFileSystemRemoveDirectory = 1UL << 4;
    public const ulong AccessFileSystemRemoveFile = 1UL << 5;
    public const ulong AccessFileSystemMakeCharacterDevice = 1UL << 6;
    public const ulong AccessFileSystemMakeDirectory = 1UL << 7;
    public const ulong AccessFileSystemMakeRegularFile = 1UL << 8;
    public const ulong AccessFileSystemMakeUnixSocket = 1UL << 9;
    public const ulong AccessFileSystemMakeFifo = 1UL << 10;
    public const ulong AccessFileSystemMakeBlockDevice = 1UL << 11;
    public const ulong AccessFileSystemMakeSymbolicLink = 1UL << 12;
    public const ulong AccessFileSystemRefer = 1UL << 13;
    public const ulong AccessFileSystemTruncate = 1UL << 14;
    public const ulong AccessFileSystemIoctlDevice = 1UL << 15;
    public const ulong AccessFileSystemResolveUnixSocket = 1UL << 16;

    public const ulong AccessNetworkBindTcp = 1UL << 0;
    public const ulong AccessNetworkConnectTcp = 1UL << 1;
    public const ulong AccessNetworkBindUdp = 1UL << 2;
    public const ulong AccessNetworkConnectSendUdp = 1UL << 3;

    [LibraryImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static partial nint SystemCall3(nint number, nint argument1, nuint argument2, uint argument3);

    [LibraryImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static partial nint SystemCall4(nint number, int argument1, uint argument2, nint argument3, uint argument4);

    [LibraryImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static partial nint SystemCall2(nint number, int argument1, uint argument2);

    [LibraryImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static partial nint SystemCallOpenAt2(
        nint number,
        int directoryDescriptor,
        nint path,
        nint how,
        nuint size);

    public static nint CreateRuleset(nint attributes, nuint size, uint flags) =>
        SystemCall3(LandlockCreateRulesetSystemCall, attributes, size, flags);

    public static nint AddRule(int rulesetDescriptor, uint ruleType, nint attributes, uint flags) =>
        SystemCall4(LandlockAddRuleSystemCall, rulesetDescriptor, ruleType, attributes, flags);

    public static nint RestrictSelf(int rulesetDescriptor, uint flags) =>
        SystemCall2(LandlockRestrictSelfSystemCall, rulesetDescriptor, flags);

    public static nint OpenAt2(int directoryDescriptor, nint path, nint how, nuint size) =>
        SystemCallOpenAt2(OpenAt2SystemCall, directoryDescriptor, path, how, size);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    public static partial int Close(nint descriptor);

    [LibraryImport("libc", EntryPoint = "prctl", SetLastError = true)]
    public static partial int Prctl(int option, nuint argument2, nuint argument3, nuint argument4, nuint argument5);
}

[StructLayout(LayoutKind.Sequential)]
public struct LandlockRulesetAttributes
{
    public ulong HandledFileSystemAccess;
    public ulong HandledNetworkAccess;
    public ulong Scoped;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LandlockPathBeneathAttributes
{
    public ulong AllowedAccess;
    public int ParentDescriptor;
}

[StructLayout(LayoutKind.Sequential)]
public struct LandlockNetworkPortAttributes
{
    public ulong AllowedAccess;
    public ulong Port;
}

[StructLayout(LayoutKind.Sequential)]
public struct OpenHow
{
    public ulong Flags;
    public ulong Mode;
    public ulong Resolve;
}
