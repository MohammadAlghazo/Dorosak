using AutoMapper;
using Dorosak.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        string? mediatRLicenseKey,
        string? autoMapperLicenseKey)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            configuration.LicenseKey = mediatRLicenseKey;
            configuration.AddOpenBehavior(typeof(TelemetryBehavior<,>));
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            configuration.AddOpenBehavior(typeof(QueryCacheBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
            configuration.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        services.AddAutoMapper(
            configuration => configuration.LicenseKey = autoMapperLicenseKey,
            AssemblyReference.Assembly);

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
