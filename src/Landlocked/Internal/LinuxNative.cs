using System.Runtime.InteropServices;
using System.Text;

namespace Landlocked.Internal;

internal static partial class LinuxNative
{
    private const nint OpenAt2SystemCall = 437;
    private const nint LandlockCreateRulesetSystemCall = 444;
    private const nint LandlockAddRuleSystemCall = 445;
    private const nint LandlockRestrictSelfSystemCall = 446;

    private const uint CreateRulesetVersion = 1U << 0;

    internal const uint PathBeneathRule = 1;
    internal const uint RestrictSelfThreadSync = 1U << 3;
    private const int CurrentWorkingDirectory = -100;
    private const ulong OpenPath = 0x200000;
    private const ulong OpenCloseOnExec = 0x80000;
    private const ulong ResolveNoSymbolicLinks = 0x04;
    internal const int SetNoNewPrivileges = 38;

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

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(nint descriptor);

    [LibraryImport("libc", EntryPoint = "prctl", SetLastError = true)]
    private static partial int Prctl(int option, nuint argument2, nuint argument3, nuint argument4, nuint argument5);

    internal static NativeResult QueryAbiVersion()
    {
        var result = SystemCall3(LandlockCreateRulesetSystemCall, 0, 0, CreateRulesetVersion);
        return CaptureResult(result);
    }

    internal static unsafe NativeResult CreateRuleset(LandlockRulesetAttributes* attributes)
    {
        var result = SystemCall3(
            LandlockCreateRulesetSystemCall,
            (nint)attributes,
            (nuint)sizeof(LandlockRulesetAttributes),
            0);
        return CaptureResult(result);
    }

    internal static unsafe NativeResult AddPathBeneathRule(int rulesetDescriptor, LandlockPathBeneathAttributes* attributes)
    {
        var result = SystemCall4(
            LandlockAddRuleSystemCall,
            rulesetDescriptor,
            PathBeneathRule,
            (nint)attributes,
            0);
        return CaptureResult(result);
    }

    internal static NativeResult SetNoNewPrivilegesForCallingThread()
    {
        var result = Prctl(SetNoNewPrivileges, 1, 0, 0, 0);
        return CaptureResult(result);
    }

    internal static NativeResult RestrictAllThreads(int rulesetDescriptor)
    {
        var result = SystemCall2(LandlockRestrictSelfSystemCall, rulesetDescriptor, RestrictSelfThreadSync);
        return CaptureResult(result);
    }

    internal static unsafe NativeResult OpenPathDescriptor(string path)
    {
        var byteCount = Encoding.UTF8.GetByteCount(path);
        var pathBytes = new byte[byteCount + 1];
        Encoding.UTF8.GetBytes(path, pathBytes);
        pathBytes[^1] = 0;

        var how = new OpenHow
        {
            Flags = OpenPath | OpenCloseOnExec,
            Resolve = ResolveNoSymbolicLinks,
        };

        nint result;
        fixed (byte* pathPointer = pathBytes)
        {
            do
            {
                result = SystemCallOpenAt2(
                    OpenAt2SystemCall,
                    CurrentWorkingDirectory,
                    (nint)pathPointer,
                    (nint)(&how),
                    (nuint)sizeof(OpenHow));
            }
            while (result < 0 && Marshal.GetLastPInvokeError() == 4);
        }

        return CaptureResult(result);
    }

    private static NativeResult CaptureResult(nint result)
    {
        if (result >= 0)
        {
            return new NativeResult(checked((int)result), 0);
        }

        return new NativeResult(checked((int)result), Marshal.GetLastPInvokeError());
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LandlockRulesetAttributes
    {
        internal ulong HandledFileSystemAccess;
        internal ulong HandledNetworkAccess;
        internal ulong Scoped;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenHow
    {
        internal ulong Flags;
        internal ulong Mode;
        internal ulong Resolve;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LandlockPathBeneathAttributes
    {
        internal ulong AllowedAccess;
        internal int ParentDescriptor;
    }
}
