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
    public void Unknown_access_bits_are_rejected()
    {
        var unknown = (FileSystemAccess)(1UL << 63);

        Assert.Throws<ArgumentOutOfRangeException>(() => LandlockPolicy.Handle(unknown));
    }
}
