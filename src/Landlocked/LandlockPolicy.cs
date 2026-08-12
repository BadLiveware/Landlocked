using System.Collections.ObjectModel;

namespace Landlocked;

public sealed class LandlockPolicy
{
    private readonly PathAccessRule[] _pathRules;
    private readonly NetworkPortRule[] _networkRules;
    private readonly ReadOnlyCollection<PathAccessRule> _readOnlyPathRules;
    private readonly ReadOnlyCollection<NetworkPortRule> _readOnlyNetworkRules;

    private LandlockPolicy(
        FileSystemAccess handledFileSystemAccess,
        NetworkAccess handledNetworkAccess,
        PathAccessRule[] pathRules,
        NetworkPortRule[] networkRules)
    {
        HandledFileSystemAccess = handledFileSystemAccess;
        HandledNetworkAccess = handledNetworkAccess;
        _pathRules = pathRules;
        _networkRules = networkRules;
        _readOnlyPathRules = Array.AsReadOnly(pathRules);
        _readOnlyNetworkRules = Array.AsReadOnly(networkRules);
    }

    public FileSystemAccess HandledAccess => HandledFileSystemAccess;

    public FileSystemAccess HandledFileSystemAccess { get; }

    public NetworkAccess HandledNetworkAccess { get; }

    public IReadOnlyList<PathAccessRule> Rules => _readOnlyPathRules;

    public IReadOnlyList<PathAccessRule> PathRules => _readOnlyPathRules;

    public IReadOnlyList<NetworkPortRule> NetworkRules => _readOnlyNetworkRules;

    public static LandlockPolicy Handle(FileSystemAccess handledAccess) =>
        Handle(handledAccess, NetworkAccess.None);

    public static LandlockPolicy HandleNetwork(NetworkAccess handledAccess) =>
        Handle(FileSystemAccess.None, handledAccess);

    public static LandlockPolicy Handle(
        FileSystemAccess handledFileSystemAccess,
        NetworkAccess handledNetworkAccess)
    {
        ValidateKnownAccess(handledFileSystemAccess, nameof(handledFileSystemAccess));
        ValidateKnownAccess(handledNetworkAccess, nameof(handledNetworkAccess));
        if (handledFileSystemAccess == FileSystemAccess.None && handledNetworkAccess == NetworkAccess.None)
        {
            throw new ArgumentException("At least one filesystem or network access right must be handled.");
        }

        return new LandlockPolicy(handledFileSystemAccess, handledNetworkAccess, [], []);
    }

    public LandlockPolicy Allow(string path, FileSystemAccess allowedAccess)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateKnownAccess(allowedAccess, nameof(allowedAccess));

        if (allowedAccess == FileSystemAccess.None)
        {
            throw new ArgumentException("At least one filesystem access right must be allowed.", nameof(allowedAccess));
        }

        var outsideHandledSet = allowedAccess & ~HandledFileSystemAccess;
        if (outsideHandledSet != FileSystemAccess.None)
        {
            throw new ArgumentException(
                $"Allowed access '{outsideHandledSet}' is not included in the policy's handled filesystem access set.",
                nameof(allowedAccess));
        }

        var fullPath = Path.GetFullPath(path);
        var existingIndex = -1;
        for (var index = 0; index < _pathRules.Length; index++)
        {
            if (string.Equals(_pathRules[index].Path, fullPath, StringComparison.Ordinal))
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            var mergedRules = (PathAccessRule[])_pathRules.Clone();
            var mergedAccess = mergedRules[existingIndex].AllowedAccess | allowedAccess;
            mergedRules[existingIndex] = new PathAccessRule(fullPath, mergedAccess);
            return new LandlockPolicy(
                HandledFileSystemAccess,
                HandledNetworkAccess,
                mergedRules,
                _networkRules);
        }

        var rules = new PathAccessRule[_pathRules.Length + 1];
        Array.Copy(_pathRules, rules, _pathRules.Length);
        rules[^1] = new PathAccessRule(fullPath, allowedAccess);
        return new LandlockPolicy(
            HandledFileSystemAccess,
            HandledNetworkAccess,
            rules,
            _networkRules);
    }

    public LandlockPolicy AllowPort(ushort port, NetworkAccess allowedAccess)
    {
        ValidateKnownAccess(allowedAccess, nameof(allowedAccess));
        if (allowedAccess == NetworkAccess.None)
        {
            throw new ArgumentException("At least one network access right must be allowed.", nameof(allowedAccess));
        }

        var outsideHandledSet = allowedAccess & ~HandledNetworkAccess;
        if (outsideHandledSet != NetworkAccess.None)
        {
            throw new ArgumentException(
                $"Allowed access '{outsideHandledSet}' is not included in the policy's handled network access set.",
                nameof(allowedAccess));
        }

        var existingIndex = -1;
        for (var index = 0; index < _networkRules.Length; index++)
        {
            if (_networkRules[index].Port == port)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            var mergedRules = (NetworkPortRule[])_networkRules.Clone();
            var mergedAccess = mergedRules[existingIndex].AllowedAccess | allowedAccess;
            mergedRules[existingIndex] = new NetworkPortRule(port, mergedAccess);
            return new LandlockPolicy(
                HandledFileSystemAccess,
                HandledNetworkAccess,
                _pathRules,
                mergedRules);
        }

        var rules = new NetworkPortRule[_networkRules.Length + 1];
        Array.Copy(_networkRules, rules, _networkRules.Length);
        rules[^1] = new NetworkPortRule(port, allowedAccess);
        return new LandlockPolicy(
            HandledFileSystemAccess,
            HandledNetworkAccess,
            _pathRules,
            rules);
    }

    private static void ValidateKnownAccess(FileSystemAccess access, string parameterName)
    {
        var unknown = access & ~FileSystemAccess.AllKnown;
        if (unknown != FileSystemAccess.None)
        {
            throw new ArgumentOutOfRangeException(parameterName, access, $"Unknown filesystem access bits: 0x{(ulong)unknown:x}.");
        }
    }

    private static void ValidateKnownAccess(NetworkAccess access, string parameterName)
    {
        var unknown = access & ~NetworkAccess.AllKnown;
        if (unknown != NetworkAccess.None)
        {
            throw new ArgumentOutOfRangeException(parameterName, access, $"Unknown network access bits: 0x{(ulong)unknown:x}.");
        }
    }
}
