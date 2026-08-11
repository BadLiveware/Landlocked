using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Landlocked.DependencyInjection;

public static class LandlockServiceCollectionExtensions
{
    public static IServiceCollection AddLandlocked(
        this IServiceCollection services,
        FileSystemAccess handledAccess)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = LandlockPermissions.Handle(handledAccess);

        var existingConfiguration = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(LandlockRegistrationConfiguration))?
            .ImplementationInstance as LandlockRegistrationConfiguration;
        if (existingConfiguration is not null)
        {
            if (existingConfiguration.HandledAccess != handledAccess)
            {
                throw new InvalidOperationException(
                    "Landlocked has already been registered with a different handled access set.");
            }

            return services;
        }

        var configuration = new LandlockRegistrationConfiguration(services, handledAccess);
        services.AddSingleton(configuration);
        services.AddSingleton(serviceProvider =>
            new LandlockActivationCoordinator(serviceProvider, configuration));
        return services;
    }

    public static IServiceCollection AddLandlockPermissionContributor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TContributor>(
        this IServiceCollection services)
        where TContributor : class, ILandlockPermissionContributor
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(LandlockRegistrationConfiguration))?
            .ImplementationInstance as LandlockRegistrationConfiguration;
        if (configuration is null)
        {
            throw new InvalidOperationException(
                "Call AddLandlocked before registering permission contributors.");
        }

        if (configuration.HasContributor(typeof(TContributor)))
        {
            return services;
        }

        var concreteDescriptor = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(TContributor));
        if (concreteDescriptor is null)
        {
            concreteDescriptor = ServiceDescriptor.Singleton<TContributor, TContributor>();
            services.Add(concreteDescriptor);
        }
        else if (concreteDescriptor.Lifetime != ServiceLifetime.Singleton)
        {
            throw new InvalidOperationException(
                $"Permission contributor '{typeof(TContributor)}' must be registered as a singleton.");
        }

        var registration =
            new LandlockRegistrationConfiguration.ContributorRegistration<TContributor>(concreteDescriptor);
        configuration.AddContributor(registration);
        services.AddSingleton<ILandlockPermissionContributor>(registration.Resolve);
        return services;
    }
}
