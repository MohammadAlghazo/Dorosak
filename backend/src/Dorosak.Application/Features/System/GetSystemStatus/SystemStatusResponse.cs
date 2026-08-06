namespace Dorosak.Application.Features.System.GetSystemStatus;

public sealed record SystemStatusResponse(string Service, string Version, DateTimeOffset UtcTime);
