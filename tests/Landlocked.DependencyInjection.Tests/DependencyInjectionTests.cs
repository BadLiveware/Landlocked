using Landlocked.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Landlocked.DependencyInjection.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddLandlocked_requires_handled_access()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddLandlocked(FileSystemAccess.None));
    }

    [Fact]
    public void Repeated_registration_requires_the_same_handled_access()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);

        Assert.Same(services, services.AddLandlocked(FileSystemAccess.WriteFile));
        Assert.Throws<InvalidOperationException>(() => services.AddLandlocked(FileSystemAccess.ReadFile));
    }

    [Fact]
    public void Contributors_require_the_root_Landlocked_registration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddLandlockPermissionContributor<TestContributor>());
    }

    [Fact]
    public void Contributor_registration_preserves_the_concrete_singleton_identity()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddLandlockPermissionContributor<TestContributor>();

        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<TestContributor>();
        var contribution = Assert.Single(provider.GetServices<ILandlockPermissionContributor>());
        Assert.Same(concrete, contribution);
    }

    [Fact]
    public void Contributor_registration_rejects_a_shorter_lifetime()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddScoped<TestContributor>();

        Assert.Throws<InvalidOperationException>(
            () => services.AddLandlockPermissionContributor<TestContributor>());
    }

    [Fact]
    public void Repeated_contributor_registration_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddLandlockPermissionContributor<TestContributor>();
        services.AddLandlockPermissionContributor<TestContributor>();

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<ILandlockPermissionContributor>());
    }

    [Fact]
    public void Replacing_a_contributor_after_registration_is_rejected_before_activation()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddLandlockPermissionContributor<TestContributor>();
        services.AddTransient<TestContributor>();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
    }

    [Fact]
    public void Contributor_construction_cannot_recursively_activate_Landlock()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddLandlockPermissionContributor<ReentrantContributor>();
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
    }

    [Fact]
    public void Contributor_failure_is_terminal_and_does_not_reinvoke_prior_contributors()
    {
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddLandlockPermissionContributor<CountingContributor>();
        services.AddLandlockPermissionContributor<FailingContributor>();
        using var provider = services.BuildServiceProvider();
        var counting = provider.GetRequiredService<CountingContributor>();

        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
        Assert.Equal(1, counting.RegistrationCount);
    }

    [Fact]
    public void Distinct_interface_instances_of_the_same_type_are_both_invoked()
    {
        var manualContributor = new CountingContributor();
        var services = new ServiceCollection();
        services.AddLandlocked(FileSystemAccess.WriteFile);
        services.AddSingleton<ILandlockPermissionContributor>(manualContributor);
        services.AddLandlockPermissionContributor<CountingContributor>();
        services.AddSingleton<ILandlockPermissionContributor, FailingContributor>();
        using var provider = services.BuildServiceProvider();
        var registeredContributor = provider.GetRequiredService<CountingContributor>();

        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
        Assert.Equal(1, registeredContributor.RegistrationCount);
        Assert.Equal(1, manualContributor.RegistrationCount);
    }

    [Fact]
    public void ActivateLandlock_requires_DI_registration()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.ActivateLandlock());
    }

    private sealed class TestContributor : ILandlockPermissionContributor
    {
        public void RegisterPermissions(ILandlockPermissionRegistry permissions)
        {
            _ = permissions.Claim(nameof(TestContributor));
        }
    }

    private sealed class ReentrantContributor : ILandlockPermissionContributor
    {
        public ReentrantContributor(IServiceProvider serviceProvider)
        {
            _ = serviceProvider.ActivateLandlock();
        }

        public void RegisterPermissions(ILandlockPermissionRegistry permissions)
        {
            _ = permissions.Claim(nameof(ReentrantContributor));
        }
    }

    private sealed class CountingContributor : ILandlockPermissionContributor
    {
        internal int RegistrationCount { get; private set; }

        public void RegisterPermissions(ILandlockPermissionRegistry permissions)
        {
            RegistrationCount++;
            _ = permissions.Claim(nameof(CountingContributor));
        }
    }

    private sealed class FailingContributor : ILandlockPermissionContributor
    {
        public void RegisterPermissions(ILandlockPermissionRegistry permissions)
        {
            throw new InvalidOperationException("Contributor failure.");
        }
    }
}
