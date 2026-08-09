using System.Collections.ObjectModel;

namespace Landlocked;

public sealed class LandlockPolicy
{
    private readonly PathAccessRule[] _rules;
    private readonly ReadOnlyCollection<PathAccessRule> _readOnlyRules;

    private LandlockPolicy(FileSystemAccess handledAccess, PathAccessRule[] rules)
    {
        HandledAccess = handledAccess;
        _rules = rules;
        _readOnlyRules = Array.AsReadOnly(rules);
    }

    public FileSystemAccess HandledAccess { get; }

    public IReadOnlyList<PathAccessRule> Rules => _readOnlyRules;

    public static LandlockPolicy Handle(FileSystemAccess handledAccess)
    {
        ValidateKnownAccess(handledAccess, nameof(handledAccess));
        if (handledAccess == FileSystemAccess.None)
        {
            throw new ArgumentException("At least one filesystem access right must be handled.", nameof(handledAccess));
        }

        return new LandlockPolicy(handledAccess, []);
    }

    public LandlockPolicy Allow(string path, FileSystemAccess allowedAccess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateKnownAccess(allowedAccess, nameof(allowedAccess));

        if (allowedAccess == FileSystemAccess.None)
        {
            throw new ArgumentException("At least one filesystem access right must be allowed.", nameof(allowedAccess));
        }

        var outsideHandledSet = allowedAccess & ~HandledAccess;
        if (outsideHandledSet != FileSystemAccess.None)
        {
            throw new ArgumentException(
                $"Allowed access '{outsideHandledSet}' is not included in the policy's handled access set.",
                nameof(allowedAccess));
        }

        var fullPath = Path.GetFullPath(path);
        var existingIndex = -1;
        for (var index = 0; index < _rules.Length; index++)
        {
            if (string.Equals(_rules[index].Path, fullPath, StringComparison.Ordinal))
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            var mergedRules = (PathAccessRule[])_rules.Clone();
            var mergedAccess = mergedRules[existingIndex].AllowedAccess | allowedAccess;
            mergedRules[existingIndex] = new PathAccessRule(fullPath, mergedAccess);
            return new LandlockPolicy(HandledAccess, mergedRules);
        }

        var rules = new PathAccessRule[_rules.Length + 1];
        Array.Copy(_rules, rules, _rules.Length);
        rules[^1] = new PathAccessRule(fullPath, allowedAccess);
        return new LandlockPolicy(HandledAccess, rules);
    }

    private static void ValidateKnownAccess(FileSystemAccess access, string parameterName)
    {
        var unknown = access & ~FileSystemAccess.AllKnown;
        if (unknown != FileSystemAccess.None)
        {
            throw new ArgumentOutOfRangeException(parameterName, access, $"Unknown filesystem access bits: 0x{(ulong)unknown:x}.");
        }
    }
}
