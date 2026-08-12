using Landlocked.Internal;

namespace Landlocked;

public static class Landlock
{
    private const int AddressFamilyNotSupported = 97;
    private const string RestrictionLockKey = "Landlocked.RestrictionLock";
    internal const int MinimumProcessSynchronizationAbi = 8;
    private static readonly object RestrictionLock = GetRestrictionLock();

    public static LandlockSupport GetSupport() => LandlockSupport.Detect();

    public static void Restrict(LandlockPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        lock (RestrictionLock)
        {
            RestrictCore(policy);
        }
    }

    private static object GetRestrictionLock()
    {
        var currentDomain = AppDomain.CurrentDomain;
        lock (currentDomain)
        {
            if (currentDomain.GetData(RestrictionLockKey) is object existingLock)
            {
                return existingLock;
            }

            var restrictionLock = new object();
            currentDomain.SetData(RestrictionLockKey, restrictionLock);
            return restrictionLock;
        }
    }

    private static unsafe void RestrictCore(LandlockPolicy policy)
    {
        var support = GetSupport();
        if (!support.IsAvailable)
        {
            throw new PlatformNotSupportedException(support.Reason);
        }

        var unsupportedFileSystemAccess = policy.HandledFileSystemAccess & ~support.SupportedFileSystemAccess;
        if (unsupportedFileSystemAccess != FileSystemAccess.None)
        {
            throw new PlatformNotSupportedException(
                $"Landlock ABI {support.AbiVersion} does not support filesystem access '{unsupportedFileSystemAccess}'.");
        }

        var unsupportedNetworkAccess = policy.HandledNetworkAccess & ~support.SupportedNetworkAccess;
        if (unsupportedNetworkAccess != NetworkAccess.None)
        {
            throw new PlatformNotSupportedException(
                $"Landlock ABI {support.AbiVersion} does not support network access '{unsupportedNetworkAccess}'.");
        }

        var rulesetAttributes = new LinuxNative.LandlockRulesetAttributes
        {
            HandledFileSystemAccess = (ulong)policy.HandledFileSystemAccess,
            HandledNetworkAccess = (ulong)policy.HandledNetworkAccess,
        };

        var createResult = LinuxNative.CreateRuleset(&rulesetAttributes);
        ThrowIfFailed(createResult, "create ruleset");

        using var ruleset = SafeFileDescriptor.Own(createResult.Value);
        foreach (var rule in policy.PathRules)
        {
            AddPathRule(ruleset, rule);
        }

        foreach (var rule in policy.NetworkRules)
        {
            AddNetworkRule(ruleset, rule);
        }

        EnforceProcessWide(ruleset.Descriptor);
    }

    private static unsafe void AddNetworkRule(SafeFileDescriptor ruleset, NetworkPortRule rule)
    {
        var attributes = new LinuxNative.LandlockNetworkPortAttributes
        {
            AllowedAccess = (ulong)rule.AllowedAccess,
            Port = rule.Port,
        };

        var addResult = LinuxNative.AddNetworkPortRule(ruleset.Descriptor, &attributes);
        if (addResult.Value < 0 && addResult.ErrorCode != AddressFamilyNotSupported)
        {
            ThrowIfFailed(addResult, $"add network port rule '{rule.Port}'");
        }
    }

    private static void EnforceProcessWide(int rulesetDescriptor)
    {
        NativeResult privilegesResult = default;
        NativeResult restrictResult = default;
        var restrictionAttempted = false;
        Exception? enforcementFailure = null;

        // Keep the irreversible no_new_privs prerequisite on a disposable thread unless TSYNC succeeds process-wide.
        var enforcementThread = new Thread(() =>
        {
            try
            {
                privilegesResult = LinuxNative.SetNoNewPrivilegesForCallingThread();
                if (privilegesResult.Value < 0)
                {
                    return;
                }

                restrictionAttempted = true;
                restrictResult = LinuxNative.RestrictAllThreads(rulesetDescriptor);
            }
            catch (Exception exception)
            {
                enforcementFailure = exception;
            }
        });

        enforcementThread.IsBackground = true;
        enforcementThread.Start();
        enforcementThread.Join();

        if (enforcementFailure is not null)
        {
            throw new InvalidOperationException("The Landlock enforcement thread failed.", enforcementFailure);
        }

        ThrowIfFailed(privilegesResult, "set no_new_privs");
        if (restrictionAttempted)
        {
            ThrowIfFailed(restrictResult, "restrict all process threads");
        }
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
