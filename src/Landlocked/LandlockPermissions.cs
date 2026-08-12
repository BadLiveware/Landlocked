namespace Landlocked;

public sealed class LandlockPermissions
{
    private readonly Lock _sync = new();
    private readonly List<LandlockPermissionClaim> _claims = [];
    private LandlockPolicy? _effectivePolicy;
    private bool _isActivated;

    private LandlockPermissions(
        FileSystemAccess handledFileSystemAccess,
        NetworkAccess handledNetworkAccess)
    {
        _ = LandlockPolicy.Handle(handledFileSystemAccess, handledNetworkAccess);
        HandledFileSystemAccess = handledFileSystemAccess;
        HandledNetworkAccess = handledNetworkAccess;
    }

    public FileSystemAccess HandledAccess => HandledFileSystemAccess;

    public FileSystemAccess HandledFileSystemAccess { get; }

    public NetworkAccess HandledNetworkAccess { get; }

    public bool IsActivated
    {
        get
        {
            lock (_sync)
            {
                return _isActivated;
            }
        }
    }

    public static LandlockPermissions Handle(FileSystemAccess handledAccess) =>
        Handle(handledAccess, NetworkAccess.None);

    public static LandlockPermissions HandleNetwork(NetworkAccess handledAccess) =>
        Handle(FileSystemAccess.None, handledAccess);

    public static LandlockPermissions Handle(
        FileSystemAccess handledFileSystemAccess,
        NetworkAccess handledNetworkAccess) =>
        new(handledFileSystemAccess, handledNetworkAccess);

    public LandlockPermissionClaim Claim(string? name = null)
    {
        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }

        lock (_sync)
        {
            if (_isActivated)
            {
                throw new InvalidOperationException("Permission claims cannot be added after activation.");
            }

            var claim = new LandlockPermissionClaim(
                this,
                name,
                LandlockPolicy.Handle(HandledFileSystemAccess, HandledNetworkAccess));
            _claims.Add(claim);
            return claim;
        }
    }

    public void Activate()
    {
        lock (_sync)
        {
            if (_isActivated)
            {
                throw new InvalidOperationException("The permission claims have already been activated.");
            }

            var initialPolicy = BuildEffectivePolicy();
            Landlock.Restrict(initialPolicy);
            _effectivePolicy = initialPolicy;
            _isActivated = true;
        }
    }

    internal LandlockPermissionClaim AddFileSystemPermission(
        LandlockPermissionClaim claim,
        string path,
        FileSystemAccess allowedAccess)
    {
        lock (_sync)
        {
            EnsureMutableClaim(claim);
            claim.Policy = claim.Policy.Allow(path, allowedAccess);
            return claim;
        }
    }

    internal LandlockPermissionClaim AddNetworkPermission(
        LandlockPermissionClaim claim,
        ushort port,
        NetworkAccess allowedAccess)
    {
        lock (_sync)
        {
            EnsureMutableClaim(claim);
            claim.Policy = claim.Policy.AllowPort(port, allowedAccess);
            return claim;
        }
    }

    internal bool IsReleased(LandlockPermissionClaim claim)
    {
        lock (_sync)
        {
            EnsureOwnedClaim(claim);
            return claim.IsReleasedCore;
        }
    }

    internal void Release(LandlockPermissionClaim claim)
    {
        lock (_sync)
        {
            EnsureOwnedClaim(claim);
            if (claim.IsReleasedCore)
            {
                return;
            }

            if (!_isActivated)
            {
                claim.IsReleasedCore = true;
                return;
            }

            var remainingPolicy = BuildEffectivePolicy(claim);
            if (!PoliciesAreEquivalent(_effectivePolicy!, remainingPolicy))
            {
                Landlock.Restrict(remainingPolicy);
                _effectivePolicy = remainingPolicy;
            }

            claim.IsReleasedCore = true;
        }
    }

    private LandlockPolicy BuildEffectivePolicy(LandlockPermissionClaim? excludedClaim = null)
    {
        var policy = LandlockPolicy.Handle(HandledFileSystemAccess, HandledNetworkAccess);
        foreach (var claim in _claims)
        {
            if (claim.IsReleasedCore || ReferenceEquals(claim, excludedClaim))
            {
                continue;
            }

            foreach (var rule in claim.Policy.PathRules)
            {
                policy = policy.Allow(rule.Path, rule.AllowedAccess);
            }

            foreach (var rule in claim.Policy.NetworkRules)
            {
                policy = policy.AllowPort(rule.Port, rule.AllowedAccess);
            }
        }

        return policy;
    }

    private void EnsureMutableClaim(LandlockPermissionClaim claim)
    {
        EnsureOwnedClaim(claim);
        if (_isActivated)
        {
            throw new InvalidOperationException("Permission claims cannot be changed after activation.");
        }

        if (claim.IsReleasedCore)
        {
            throw new InvalidOperationException("A released permission claim cannot be changed.");
        }
    }

    private void EnsureOwnedClaim(LandlockPermissionClaim claim)
    {
        if (!ReferenceEquals(claim.Owner, this))
        {
            throw new ArgumentException("The permission claim belongs to another coordinator.", nameof(claim));
        }
    }

    private static bool PoliciesAreEquivalent(LandlockPolicy first, LandlockPolicy second) =>
        first.HandledFileSystemAccess == second.HandledFileSystemAccess &&
        first.HandledNetworkAccess == second.HandledNetworkAccess &&
        PathRulesAreEquivalent(first.PathRules, second.PathRules) &&
        NetworkRulesAreEquivalent(first.NetworkRules, second.NetworkRules);

    private static bool PathRulesAreEquivalent(
        IReadOnlyList<PathAccessRule> first,
        IReadOnlyList<PathAccessRule> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var firstRule in first)
        {
            if (!second.Any(secondRule =>
                    string.Equals(firstRule.Path, secondRule.Path, StringComparison.Ordinal) &&
                    firstRule.AllowedAccess == secondRule.AllowedAccess))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NetworkRulesAreEquivalent(
        IReadOnlyList<NetworkPortRule> first,
        IReadOnlyList<NetworkPortRule> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var firstRule in first)
        {
            if (!second.Any(secondRule =>
                    firstRule.Port == secondRule.Port &&
                    firstRule.AllowedAccess == secondRule.AllowedAccess))
            {
                return false;
            }
        }

        return true;
    }
}
