using System.Diagnostics;

namespace Dorosak.Application.Common.Telemetry;

public static class ApplicationTelemetry
{
    public const string ActivitySourceName = "Dorosak.Application";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
