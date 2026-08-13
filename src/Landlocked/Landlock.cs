using Landlocked.LowLevel;

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
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Landlock is available only on Linux.");
        }

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

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void RestrictCore(LandlockPolicy policy)
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

        var createResult = LandlockApi.CreateRuleset(
            (LandlockFileSystemAccess)policy.HandledFileSystemAccess,
            (LandlockNetworkAccess)policy.HandledNetworkAccess,
            out var ruleset);
        ThrowIfFailed(createResult, "create ruleset");

        var createdRuleset = ruleset
            ?? throw new InvalidOperationException("A successful ruleset creation did not return a descriptor.");
        using (createdRuleset)
        {
            foreach (var rule in policy.PathRules)
            {
                AddPathRule(createdRuleset, rule);
            }

            foreach (var rule in policy.NetworkRules)
            {
                AddNetworkRule(createdRuleset, rule);
            }

            EnforceProcessWide(createdRuleset);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void AddNetworkRule(LandlockRuleset ruleset, NetworkPortRule rule)
    {
        var addResult = LandlockApi.AddNetworkPortRule(
            ruleset,
            rule.Port,
            (LandlockNetworkAccess)rule.AllowedAccess);
        if (!addResult.IsSuccess && addResult.ErrorCode != AddressFamilyNotSupported)
        {
            ThrowIfFailed(addResult, $"add network port rule '{rule.Port}'");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void EnforceProcessWide(LandlockRuleset ruleset)
    {
        LandlockResult privilegesResult = default;
        LandlockResult restrictResult = default;
        var restrictionAttempted = false;
        Exception? enforcementFailure = null;

        // Keep the irreversible no_new_privs prerequisite on a disposable thread unless TSYNC succeeds process-wide.
        var enforcementThread = new Thread(() =>
        {
            try
            {
                privilegesResult = LandlockApi.SetNoNewPrivilegesForCallingThread();
                if (!privilegesResult.IsSuccess)
                {
                    return;
                }

                restrictionAttempted = true;
                restrictResult = LandlockApi.RestrictAllThreads(ruleset);
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

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void AddPathRule(LandlockRuleset ruleset, PathAccessRule rule)
    {
        var addResult = LandlockApi.AddPathBeneathRule(
            ruleset,
            rule.Path,
            (LandlockFileSystemAccess)rule.AllowedAccess);
        var operation = addResult.Operation == LandlockRuleOperation.OpenPath
            ? $"open path '{rule.Path}'"
            : $"add path rule '{rule.Path}'";
        ThrowIfFailed(addResult.Result, operation);
    }

    private static void ThrowIfFailed(LandlockResult result, string operation)
    {
        if (!result.IsSuccess)
        {
            throw new LandlockException(operation, result.ErrorCode);
        }
    }
}
