using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockPolicyTests
{
    [Fact]
    public void Handle_requires_at_least_one_access_right()
    {
        Assert.Throws<ArgumentException>(() => LandlockPolicy.Handle(FileSystemAccess.None));
    }

    [Fact]
    public void Allow_requires_rights_to_be_handled()
    {
        var policy = LandlockPolicy.Handle(FileSystemAccess.WriteFile);

        var exception = Assert.Throws<ArgumentException>(
            () => policy.Allow(".", FileSystemAccess.ReadFile));

        Assert.Contains(nameof(FileSystemAccess.ReadFile), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Allow_normalizes_paths_without_mutating_the_original_policy()
    {
        var original = LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation);
        var updated = original.Allow(".", FileSystemAccess.WriteFile);

        Assert.Empty(original.Rules);
        var rule = Assert.Single(updated.Rules);
        Assert.Equal(Path.GetFullPath("."), rule.Path);
        Assert.Equal(FileSystemAccess.WriteFile, rule.AllowedAccess);
    }

    [Fact]
    public void Repeated_path_rules_are_merged()
    {
        var policy = LandlockPolicy
            .Handle(FileSystemAccess.ContentAndHierarchyMutation)
            .Allow(".", FileSystemAccess.WriteFile)
            .Allow(".", FileSystemAccess.Truncate);

        var rule = Assert.Single(policy.Rules);
        Assert.Equal(FileSystemAccess.WriteFile | FileSystemAccess.Truncate, rule.AllowedAccess);
    }

    [Fact]
    public void Existing_default_handle_call_remains_unambiguous()
    {
        Assert.Throws<ArgumentException>(() => LandlockPolicy.Handle(default));
    }

    [Fact]
    public void Network_policy_requires_at_least_one_access_right()
    {
        Assert.Throws<ArgumentException>(() => LandlockPolicy.HandleNetwork(NetworkAccess.None));
    }

    [Fact]
    public void Network_rules_are_immutable_and_merge_access_for_the_same_port()
    {
        var original = LandlockPolicy.HandleNetwork(NetworkAccess.Tcp);
        var updated = original
            .AllowPort(443, NetworkAccess.ConnectTcp)
            .AllowPort(443, NetworkAccess.BindTcp);

        Assert.Empty(original.NetworkRules);
        var rule = Assert.Single(updated.NetworkRules);
        Assert.Equal((ushort)443, rule.Port);
        Assert.Equal(NetworkAccess.Tcp, rule.AllowedAccess);
    }

    [Fact]
    public void Network_rules_must_be_in_the_handled_set()
    {
        var policy = LandlockPolicy.HandleNetwork(NetworkAccess.ConnectTcp);

        Assert.Throws<ArgumentException>(() => policy.AllowPort(443, NetworkAccess.BindTcp));
    }

    [Fact]
    public void Zero_is_a_valid_network_port_rule()
    {
        var policy = LandlockPolicy
            .HandleNetwork(NetworkAccess.BindUdp)
            .AllowPort(0, NetworkAccess.BindUdp);

        Assert.Equal((ushort)0, Assert.Single(policy.NetworkRules).Port);
    }

    [Fact]
    public void Unknown_access_bits_are_rejected()
    {
        var unknownFileSystemAccess = (FileSystemAccess)(1UL << 63);
        var unknownNetworkAccess = (NetworkAccess)(1UL << 63);

        Assert.Throws<ArgumentOutOfRangeException>(() => LandlockPolicy.Handle(unknownFileSystemAccess));
        Assert.Throws<ArgumentOutOfRangeException>(() => LandlockPolicy.HandleNetwork(unknownNetworkAccess));
    }
}
