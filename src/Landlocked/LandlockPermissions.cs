namespace Landlocked;

public sealed class LandlockPermissions
{
    private readonly Lock _sync = new();
    private readonly List<LandlockPermissionClaim> _claims = [];
    private LandlockPolicy? _effectivePolicy;
    private bool _isActivated;

    private LandlockPermissions(FileSystemAccess handledAccess)
    {
        _ = LandlockPolicy.Handle(handledAccess);
        HandledAccess = handledAccess;
    }

    public FileSystemAccess HandledAccess { get; }

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

    public static LandlockPermissions Handle(FileSystemAccess handledAccess) => new(handledAccess);

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

            var claim = new LandlockPermissionClaim(this, name, LandlockPolicy.Handle(HandledAccess));
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

    internal LandlockPermissionClaim AddPermission(
        LandlockPermissionClaim claim,
        string path,
        FileSystemAccess allowedAccess)
    {
        lock (_sync)
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

            claim.Policy = claim.Policy.Allow(path, allowedAccess);
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
        var policy = LandlockPolicy.Handle(HandledAccess);
        foreach (var claim in _claims)
        {
            if (claim.IsReleasedCore || ReferenceEquals(claim, excludedClaim))
            {
                continue;
            }

            foreach (var rule in claim.Policy.Rules)
            {
                policy = policy.Allow(rule.Path, rule.AllowedAccess);
            }
        }

        return policy;
    }

    private void EnsureOwnedClaim(LandlockPermissionClaim claim)
    {
        if (!ReferenceEquals(claim.Owner, this))
        {
            throw new ArgumentException("The permission claim belongs to another coordinator.", nameof(claim));
        }
    }

    private static bool PoliciesAreEquivalent(LandlockPolicy first, LandlockPolicy second)
    {
        if (first.HandledAccess != second.HandledAccess || first.Rules.Count != second.Rules.Count)
        {
            return false;
        }

        foreach (var firstRule in first.Rules)
        {
            var found = false;
            foreach (var secondRule in second.Rules)
            {
                if (string.Equals(firstRule.Path, secondRule.Path, StringComparison.Ordinal) &&
                    firstRule.AllowedAccess == secondRule.AllowedAccess)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
