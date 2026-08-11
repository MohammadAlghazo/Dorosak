using Dorosak.Api.Controllers;
using Dorosak.Application.Features.Communications;
using Dorosak.Domain.Common;
using Dorosak.Worker;
using NetArchTest.Rules;
using ArchitectureTestResult = NetArchTest.Rules.TestResult;

namespace Dorosak.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        ArchitectureTestResult result = Types.InAssembly(typeof(Entity<>).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Dorosak.Application", "Dorosak.Infrastructure", "Dorosak.Api", "Dorosak.Worker")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrHosts()
    {
        ArchitectureTestResult result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Dorosak.Infrastructure", "Dorosak.Api", "Dorosak.Worker")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ApplicationRealtimePorts_DoNotReferenceSignalR()
    {
        Assert.DoesNotContain(
            typeof(ICommunicationsRealtimePublisher).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name?.StartsWith("Microsoft.AspNetCore.SignalR", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnHosts()
    {
        ArchitectureTestResult result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Dorosak.Api", "Dorosak.Worker")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ApiControllers_DoNotExposeDomainDependencies()
    {
        ArchitectureTestResult result = Types.InAssembly(typeof(SystemController).Assembly)
            .That()
            .ResideInNamespace("Dorosak.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("Dorosak.Domain")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void ApplicationHandlers_AreSealed()
    {
        ArchitectureTestResult result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .BeSealed()
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Worker_DoesNotLeakIntoReusableLayers()
    {
        ArchitectureTestResult result = Types.InAssemblies([
                typeof(Entity<>).Assembly,
                typeof(Application.DependencyInjection).Assembly,
                typeof(Infrastructure.DependencyInjection).Assembly,
            ])
            .ShouldNot()
            .HaveDependencyOn(typeof(WorkerHeartbeatService).Namespace!)
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(ArchitectureTestResult result)
    {
        string failures = string.Join(", ", result.FailingTypes?.Select(type => type.FullName) ?? []);
        Assert.True(result.IsSuccessful, failures);
    }
}
