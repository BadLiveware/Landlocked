using Landlocked.Internal;

namespace Landlocked;

public static class Landlock
{
    internal const int MinimumProcessSynchronizationAbi = 8;
    private static readonly object RestrictionLock = new();

    public static LandlockSupport GetSupport() => LandlockSupport.Detect();

    public static void Restrict(LandlockPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        lock (RestrictionLock)
        {
            RestrictCore(policy);
        }
    }

    private static unsafe void RestrictCore(LandlockPolicy policy)
    {
        var support = GetSupport();
        if (!support.IsAvailable)
        {
            throw new PlatformNotSupportedException(support.Reason);
        }

        var unsupportedAccess = policy.HandledAccess & ~support.SupportedFileSystemAccess;
        if (unsupportedAccess != FileSystemAccess.None)
        {
            throw new PlatformNotSupportedException(
                $"Landlock ABI {support.AbiVersion} does not support '{unsupportedAccess}'.");
        }

        var rulesetAttributes = new LinuxNative.LandlockRulesetAttributes
        {
            HandledFileSystemAccess = (ulong)policy.HandledAccess,
        };

        var createResult = LinuxNative.CreateRuleset(&rulesetAttributes);
        ThrowIfFailed(createResult, "create ruleset");

        using var ruleset = SafeFileDescriptor.Own(createResult.Value);
        foreach (var rule in policy.Rules)
        {
            AddPathRule(ruleset, rule);
        }

        var privilegesResult = LinuxNative.SetNoNewPrivilegesForCallingThread();
        ThrowIfFailed(privilegesResult, "set no_new_privs");

        var restrictResult = LinuxNative.RestrictAllThreads(ruleset.Descriptor);
        ThrowIfFailed(restrictResult, "restrict all process threads");
    }

    private static unsafe void AddPathRule(SafeFileDescriptor ruleset, PathAccessRule rule)
    {
        var openResult = LinuxNative.OpenPathDescriptor(rule.Path);
        ThrowIfFailed(openResult, $"open path '{rule.Path}'");

        using var parent = SafeFileDescriptor.Own(openResult.Value);
        var attributes = new LinuxNative.LandlockPathBeneathAttributes
        {
            AllowedAccess = (ulong)rule.AllowedAccess,
            ParentDescriptor = parent.Descriptor,
        };

        var addResult = LinuxNative.AddPathBeneathRule(ruleset.Descriptor, &attributes);
        ThrowIfFailed(addResult, $"add path rule '{rule.Path}'");
    }

    private static void ThrowIfFailed(NativeResult result, string operation)
    {
        if (result.Value < 0)
        {
            throw new LandlockException(operation, result.ErrorCode);
        }
    }
}
