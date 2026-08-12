using Microsoft.Extensions.DependencyInjection;

namespace Landlocked.DependencyInjection;

internal sealed class LandlockRegistrationConfiguration(
    IServiceCollection services,
    FileSystemAccess handledFileSystemAccess,
    NetworkAccess handledNetworkAccess)
{
    private readonly List<IContributorRegistration> _contributorRegistrations = [];

    internal FileSystemAccess HandledFileSystemAccess { get; } = handledFileSystemAccess;

    internal NetworkAccess HandledNetworkAccess { get; } = handledNetworkAccess;

    internal bool HasContributor(Type contributorType) =>
        _contributorRegistrations.Any(registration => registration.ContributorType == contributorType);

    internal void AddContributor(IContributorRegistration registration) =>
        _contributorRegistrations.Add(registration);

    internal IReadOnlyList<ILandlockPermissionContributor> ResolveContributors(IServiceProvider serviceProvider)
    {
        foreach (var registration in _contributorRegistrations)
        {
            var effectiveDescriptor = services.LastOrDefault(
                descriptor => descriptor.ServiceType == registration.ContributorType);
            if (!ReferenceEquals(effectiveDescriptor, registration.ConcreteDescriptor))
            {
                throw new InvalidOperationException(
                    $"Permission contributor '{registration.ContributorType}' was replaced after Landlocked registration.");
            }
        }

        var contributors = new List<ILandlockPermissionContributor>();
        var contributorInstances = new HashSet<ILandlockPermissionContributor>(ReferenceEqualityComparer.Instance);
        foreach (var registration in _contributorRegistrations)
        {
            var contributor = registration.Resolve(serviceProvider);
            if (contributorInstances.Add(contributor))
            {
                contributors.Add(contributor);
            }
        }

        foreach (var contributor in serviceProvider.GetServices<ILandlockPermissionContributor>())
        {
            if (contributorInstances.Add(contributor))
            {
                contributors.Add(contributor);
            }
        }

        return contributors;
    }

    internal interface IContributorRegistration
    {
        Type ContributorType { get; }

        ServiceDescriptor ConcreteDescriptor { get; }

        ILandlockPermissionContributor Resolve(IServiceProvider serviceProvider);
    }

    internal sealed class ContributorRegistration<TContributor>(ServiceDescriptor concreteDescriptor)
        : IContributorRegistration
        where TContributor : class, ILandlockPermissionContributor
    {
        public Type ContributorType => typeof(TContributor);

        public ServiceDescriptor ConcreteDescriptor { get; } = concreteDescriptor;

        public ILandlockPermissionContributor Resolve(IServiceProvider serviceProvider) =>
            serviceProvider.GetRequiredService<TContributor>();
    }
}
