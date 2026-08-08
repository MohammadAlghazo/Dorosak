using System.Globalization;
using Dorosak.Application;
using Dorosak.Infrastructure;
using Dorosak.Infrastructure.MediaWorker;
using Dorosak.Infrastructure.Observability;
using OpenTelemetry.Metrics;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Dorosak.MediaWorker")
        .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName));
    builder.Services.AddApplication(
        builder.Configuration["Licensing:MediatRKey"],
        builder.Configuration["Licensing:AutoMapperKey"]);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddDorosakObservability(builder.Configuration, "Dorosak.MediaWorker", false);
    builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(MediaWorkerTelemetry.MeterName));
    builder.Services.AddMediaWorkerServices(builder.Configuration);

    using IHost host = builder.Build();
    if (args.Contains("--container-smoke", StringComparer.Ordinal))
    {
        await MediaContainerSmoke.RunAsync(host.Services, CancellationToken.None);
    }
    else
    {
        await host.RunAsync();
    }
}
catch (Exception exception)
{
    Log.Fatal(exception, "Dorosak MediaWorker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
