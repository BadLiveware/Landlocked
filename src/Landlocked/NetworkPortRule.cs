namespace Landlocked;

public readonly record struct NetworkPortRule
{
    internal NetworkPortRule(ushort port, NetworkAccess allowedAccess)
    {
        Port = port;
        AllowedAccess = allowedAccess;
    }

    public ushort Port { get; }

    public NetworkAccess AllowedAccess { get; }
}
