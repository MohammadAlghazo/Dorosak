using System.Globalization;
using System.IO.Compression;
using Dorosak.Api;
using Dorosak.Api.Health;
using Dorosak.Api.Middleware;
using Dorosak.Api.Realtime;
using Dorosak.Application;
using Dorosak.Infrastructure;
using Dorosak.Infrastructure.Observability;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Dorosak.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

    builder.Services.AddApplication(
        builder.Configuration["Licensing:MediatRKey"],
        builder.Configuration["Licensing:AutoMapperKey"]);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration, builder.Environment);
    builder.Services.AddDorosakObservability(builder.Configuration, "Dorosak.Api", true);

    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        options.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        options.Level = CompressionLevel.Fastest);

    WebApplication app = builder.Build();

    app.UseForwardedHeaders();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseResponseCompression();
    app.UseRouting();
    app.UseCors(ApiConstants.CorsPolicy);
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseMiddleware<OriginValidationMiddleware>();
    app.UseAuthorization();
    app.UseOutputCache();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dorosak API v1"));
    }

    app.MapControllers();
    app.MapHub<CommunicationsHub>(CommunicationsHub.Path, options =>
        options.CloseOnAuthenticationExpiration = true)
        .RequireAuthorization()
        .RequireRateLimiting(ApiConstants.SensitiveRateLimitPolicy);
    app.MapDorosakHealthChecks();

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Dorosak API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;

