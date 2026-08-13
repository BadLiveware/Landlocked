using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Landlocked.Native;

namespace Landlocked.LowLevel;

/// <summary>Resource-safe C# operations over Linux Landlock rulesets.</summary>
/// <remarks>
/// This API owns returned ruleset descriptors and captures <c>errno</c>, but callers remain responsible for ABI
/// compatibility, rule semantics, irreversible restriction ordering, and cross-thread coordination.
/// </remarks>
[SupportedOSPlatform("linux")]
public static class LandlockApi
{
    private const int InterruptedSystemCall = 4;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static LandlockResult QueryAbiVersion()
    {
        EnsureSupportedPlatform();
        return CaptureResult(LandlockNative.CreateRuleset(0, 0, LandlockNative.CreateRulesetVersion));
    }

    public static unsafe LandlockResult CreateRuleset(
        LandlockFileSystemAccess handledFileSystemAccess,
        LandlockNetworkAccess handledNetworkAccess,
        out LandlockRuleset? ruleset)
    {
        EnsureSupportedPlatform();
        var attributes = new LandlockRulesetAttributes
        {
            HandledFileSystemAccess = (ulong)handledFileSystemAccess,
            HandledNetworkAccess = (ulong)handledNetworkAccess,
        };

        var result = CaptureResult(LandlockNative.CreateRuleset(
            (nint)(&attributes),
            (nuint)sizeof(LandlockRulesetAttributes),
            0));

        ruleset = result.IsSuccess ? LandlockRuleset.Own(result.Value) : null;
        return result;
    }

    public static unsafe LandlockRuleResult AddPathBeneathRule(
        LandlockRuleset ruleset,
        string path,
        LandlockFileSystemAccess allowedAccess)
    {
        EnsureSupportedPlatform();
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Paths cannot contain a null character.", nameof(path));
        }

        var openResult = OpenPath(path, out var parentDescriptor);
        if (!openResult.IsSuccess)
        {
            return new LandlockRuleResult(openResult, LandlockRuleOperation.OpenPath);
        }

        try
        {
            var attributes = new LandlockPathBeneathAttributes
            {
                AllowedAccess = (ulong)allowedAccess,
                ParentDescriptor = parentDescriptor,
            };

            using var rulesetDescriptor = ruleset.BorrowDescriptor();
            return new LandlockRuleResult(
                CaptureResult(LandlockNative.AddRule(
                    rulesetDescriptor.Descriptor,
                    LandlockNative.PathBeneathRule,
                    (nint)(&attributes),
                    0)),
                LandlockRuleOperation.AddRule);
        }
        finally
        {
            Close(parentDescriptor);
        }
    }

    public static unsafe LandlockResult AddNetworkPortRule(
        LandlockRuleset ruleset,
        ushort port,
        LandlockNetworkAccess allowedAccess)
    {
        EnsureSupportedPlatform();
        ArgumentNullException.ThrowIfNull(ruleset);

        var attributes = new LandlockNetworkPortAttributes
        {
            AllowedAccess = (ulong)allowedAccess,
            Port = port,
        };

        using var rulesetDescriptor = ruleset.BorrowDescriptor();
        return CaptureResult(LandlockNative.AddRule(
            rulesetDescriptor.Descriptor,
            LandlockNative.NetworkPortRule,
            (nint)(&attributes),
            0));
    }

    public static LandlockResult SetNoNewPrivilegesForCallingThread()
    {
        EnsureSupportedPlatform();
        return CaptureResult(LandlockNative.Prctl(LandlockNative.SetNoNewPrivileges, 1, 0, 0, 0));
    }

    public static LandlockResult RestrictCallingThread(LandlockRuleset ruleset)
    {
        EnsureSupportedPlatform();
        ArgumentNullException.ThrowIfNull(ruleset);
        using var rulesetDescriptor = ruleset.BorrowDescriptor();
        return CaptureResult(LandlockNative.RestrictSelf(rulesetDescriptor.Descriptor, 0));
    }

    public static LandlockResult RestrictAllThreads(LandlockRuleset ruleset)
    {
        EnsureSupportedPlatform();
        ArgumentNullException.ThrowIfNull(ruleset);
        using var rulesetDescriptor = ruleset.BorrowDescriptor();
        return CaptureResult(LandlockNative.RestrictSelf(
            rulesetDescriptor.Descriptor,
            LandlockNative.RestrictSelfThreadSync));
    }

    private static unsafe LandlockResult OpenPath(string path, out int descriptor)
    {
        byte[] pathBytes;
        try
        {
            var byteCount = Utf8.GetByteCount(path);
            pathBytes = new byte[byteCount + 1];
            Utf8.GetBytes(path, pathBytes);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("Paths must contain valid Unicode text.", nameof(path), exception);
        }
        pathBytes[^1] = 0;

        var how = new OpenHow
        {
            Flags = LandlockNative.OpenPath | LandlockNative.OpenCloseOnExec,
            Resolve = LandlockNative.ResolveNoSymbolicLinks,
        };

        nint nativeResult;
        int errorCode;
        fixed (byte* pathPointer = pathBytes)
        {
            do
            {
                nativeResult = LandlockNative.OpenAt2(
                    LandlockNative.CurrentWorkingDirectory,
                    (nint)pathPointer,
                    (nint)(&how),
                    (nuint)sizeof(OpenHow));
                errorCode = nativeResult < 0 ? Marshal.GetLastPInvokeError() : 0;
            }
            while (nativeResult < 0 && errorCode == InterruptedSystemCall);
        }

        var result = CaptureResult(nativeResult, errorCode);
        descriptor = result.IsSuccess ? result.Value : -1;
        return result;
    }

    internal static int Close(nint descriptor) => LandlockNative.Close(descriptor);

    private static void EnsureSupportedPlatform()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Landlock is available only on Linux.");
        }

        var architecture = RuntimeInformation.ProcessArchitecture;
        if (architecture != Architecture.X64 && architecture != Architecture.Arm64)
        {
            throw new PlatformNotSupportedException(
                $"Landlocked.LowLevel does not support the {architecture} architecture.");
        }
    }

    private static LandlockResult CaptureResult(nint result) =>
        CaptureResult(result, result < 0 ? Marshal.GetLastPInvokeError() : 0);

    private static LandlockResult CaptureResult(nint result, int errorCode) =>
        new(checked((int)result), result < 0 ? errorCode : 0);
}
