namespace Landlocked;

[Flags]
public enum NetworkAccess : ulong
{
    None = 0,
    BindTcp = 1UL << 0,
    ConnectTcp = 1UL << 1,
    BindUdp = 1UL << 2,
    ConnectSendUdp = 1UL << 3,

    Tcp = BindTcp | ConnectTcp,
    Udp = BindUdp | ConnectSendUdp,
    AllKnown = Tcp | Udp,
}
