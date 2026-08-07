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
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.GetCourseQuery>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetCourseQuery>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.UpdateCourseMetadataCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.UpdateCourseMetadataCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.ArchiveCourseCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.ArchiveCourseCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.GetCurriculumQuery>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetCurriculumQuery>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.UpdateCurriculumCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.UpdateCurriculumCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.AddCollaboratorCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.AddCollaboratorCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.RemoveCollaboratorCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.RemoveCollaboratorCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.RequestPublicationCommand>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.RequestPublicationCommand>));
        services.AddScoped(typeof(Common.Authorization.IRequestAuthorizer<Features.Phase6.GetPublicationStatusQuery>),
            typeof(Features.Phase6.Phase6ResourceAuthorizer<Features.Phase6.GetPublicationStatusQuery>));
        services.AddAutoMapper(
            configuration => configuration.LicenseKey = autoMapperLicenseKey,
            AssemblyReference.Assembly);

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
