using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.MediaWorker;

public static class MediaWorkerServices
{
    public static IServiceCollection AddMediaWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClamAvOptions>()
            .Bind(configuration.GetSection(ClamAvOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "ClamAV host is required.")
            .Validate(options => options.Port is > 0 and <= 65535, "ClamAV port is invalid.")
            .Validate(options => options.ChunkBytes is >= 4096 and <= 1024 * 1024, "ClamAV chunk size is invalid.")
            .ValidateOnStart();
        services.AddScoped<IMediaContentValidator, MagicByteMediaValidator>();
        services.AddScoped<IMediaProcessor, FfmpegMediaProcessor>();
        services.AddScoped<IMalwareScanner, ClamAvInstreamScanner>();
        services.AddHostedService<MediaProcessingWorker>();
        return services;
    }
}

internal sealed partial class ClamAvInstreamScanner(IOptions<ClamAvOptions> options, ILogger<ClamAvInstreamScanner> logger)
    : IMalwareScanner
{
    private readonly ClamAvOptions _options = options.Value;

    public async Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
            await using NetworkStream network = client.GetStream();
            await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), cancellationToken);
            byte[] buffer = new byte[_options.ChunkBytes];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                byte[] size = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(read));
                await network.WriteAsync(size, cancellationToken);
                await network.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            await network.WriteAsync(new byte[4], cancellationToken);
            await network.FlushAsync(cancellationToken);
            using var reader = new StreamReader(network, Encoding.ASCII, leaveOpen: true);
            string response = await reader.ReadToEndAsync(cancellationToken);
            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return new MalwareScanResult(MalwareScanStatus.Infected, response.Trim());
            }
            if (response.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                return new MalwareScanResult(MalwareScanStatus.Clean);
            }
            return new MalwareScanResult(MalwareScanStatus.Unavailable, response.Trim());
        }
        catch (Exception exception) when (exception is IOException or SocketException or TimeoutException or OperationCanceledException)
        {
            ScannerUnavailable(logger, exception);
            return new MalwareScanResult(MalwareScanStatus.Unavailable);
        }
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Warning, Message = "ClamAV INSTREAM scan was unavailable")]
    private static partial void ScannerUnavailable(ILogger logger, Exception exception);
}

public sealed class ClamAvOptions
{
    public const string SectionName = "Media:ClamAV";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 3310;

    public int ChunkBytes { get; set; } = 1024 * 1024;
}

internal sealed class MagicByteMediaValidator : IMediaContentValidator
{
    public async Task<MediaValidationResult> ValidateAsync(
        string filePath,
        MediaPurpose purpose,
        string declaredContentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] header = new byte[16];
        int read = await stream.ReadAsync(header, cancellationToken);
        bool valid = purpose switch
        {
            MediaPurpose.ProfileImage or MediaPurpose.CourseImage => IsJpeg(header, read) || IsPng(header, read) || IsWebp(header, read),
            MediaPurpose.CourseDocument or MediaPurpose.AssignmentSubmission => IsPdf(header, read),
            MediaPurpose.SourceVideo => IsIsoBaseMedia(header, read),
            _ => false,
        };
        return valid
            ? new MediaValidationResult(true, null, declaredContentType)
            : new MediaValidationResult(false, "MEDIA.MAGIC_BYTES_INVALID", null);
    }

    private static bool IsJpeg(byte[] bytes, int length) => length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    private static bool IsPng(byte[] bytes, int length) => length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    private static bool IsWebp(byte[] bytes, int length) => length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
    private static bool IsPdf(byte[] bytes, int length) => length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
    private static bool IsIsoBaseMedia(byte[] bytes, int length) => length >= 12 && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp";
}

internal sealed partial class FfmpegMediaProcessor(IOptions<MediaOptions> options, ILogger<FfmpegMediaProcessor> logger)
    : IMediaProcessor
{
    private readonly MediaOptions _options = options.Value;

    public async Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingInput input,
        string sourceFilePath,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        return input.Purpose switch
        {
            MediaPurpose.ProfileImage or MediaPurpose.CourseImage => await ProcessImageAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            MediaPurpose.CourseDocument or MediaPurpose.AssignmentSubmission => await ProcessDocumentAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            MediaPurpose.SourceVideo => await ProcessVideoAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported media purpose."),
        };
    }

    private async Task<MediaProcessingResult> ProcessImageAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        var files = new List<MediaVariantFile>();
        foreach (int width in new[] { 320, 640, 1280 })
        {
            Guid variantId = Guid.CreateVersion7();
            string file = Path.Combine(output, $"image-{width}.webp");
            await RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-vf", $"scale='min({width},iw)':-2", "-frames:v", "1", "-map_metadata", "-1", "-c:v", "libwebp", "-y", file], cancellationToken);
            if (File.Exists(file))
            {
                files.Add(new MediaVariantFile(variantId, $"image-{width}", file, "image/webp", MediaObjectKeys.Ready(_options.Environment, input.AssetId, variantId, $"image-{width}.webp")));
            }
        }
        return new MediaProcessingResult(files);
    }

    private async Task<MediaProcessingResult> ProcessDocumentAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        string file = Path.Combine(output, "document.pdf");
        Guid variantId = Guid.CreateVersion7();
        File.Copy(source, file, overwrite: true);
        await Task.CompletedTask;
        return new MediaProcessingResult([new MediaVariantFile(variantId, "document", file, "application/pdf", MediaObjectKeys.Ready(_options.Environment, input.AssetId, variantId, "document.pdf"))]);
    }

    private async Task<MediaProcessingResult> ProcessVideoAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        string file = Path.Combine(output, "video.mp4");
        Guid variantId = Guid.CreateVersion7();
        await RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-map_metadata", "-1", "-c:v", "libx264", "-c:a", "aac", "-movflags", "+faststart", "-y", file], cancellationToken);
        return new MediaProcessingResult([new MediaVariantFile(variantId, "video", file, "video/mp4", MediaObjectKeys.Ready(_options.Environment, input.AssetId, variantId, "video.mp4"))]);
    }

    private async Task RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true } };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {executable}.");
        }
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            MediaProcessFailed(logger, process.ExitCode, error[..Math.Min(error.Length, 500)]);
            throw new InvalidOperationException("Media processing failed.");
        }
    }

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "Media process failed with code {ExitCode}: {Error}")]
    private static partial void MediaProcessFailed(ILogger logger, int exitCode, string error);
}

internal sealed partial class MediaProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaOptions> options,
    ILogger<MediaProcessingWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly MediaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var concurrency = new SemaphoreSlim(_options.WorkerConcurrency);
        var running = new List<Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            await concurrency.WaitAsync(stoppingToken);
            using IServiceScope scope = scopeFactory.CreateScope();
            IMediaJobStore jobs = scope.ServiceProvider.GetRequiredService<IMediaJobStore>();
            MediaJobClaim? claim = await jobs.TryClaimAsync(timeProvider.GetUtcNow(), _options.WorkerLockDuration, stoppingToken);
            if (claim is null)
            {
                concurrency.Release();
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }
            running.Add(ProcessClaimAsync(claim, concurrency, stoppingToken));
            running.RemoveAll(task => task.IsCompleted);
        }
        await Task.WhenAll(running);
    }

    private async Task ProcessClaimAsync(MediaJobClaim claim, SemaphoreSlim concurrency, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IMediaJobStore jobs = scope.ServiceProvider.GetRequiredService<IMediaJobStore>();
            IMediaProcessingStore store = scope.ServiceProvider.GetRequiredService<IMediaProcessingStore>();
            IObjectStorage storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            IMalwareScanner scanner = scope.ServiceProvider.GetRequiredService<IMalwareScanner>();
            IMediaContentValidator validator = scope.ServiceProvider.GetRequiredService<IMediaContentValidator>();
            IMediaProcessor processor = scope.ServiceProvider.GetRequiredService<IMediaProcessor>();
            MediaAssetWorkItem? work = await store.GetWorkItemAsync(claim.AssetId, cancellationToken);
            if (work is null)
            {
                await jobs.FailAsync(claim, timeProvider.GetUtcNow(), "MEDIA.ASSET_MISSING", cancellationToken);
                return;
            }
            string tempDirectory = Path.Combine(Path.GetTempPath(), "dorosak-media", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string source = Path.Combine(tempDirectory, "source.bin");
            string output = Path.Combine(tempDirectory, "output");
            try
            {
                await store.MarkScanningAsync(work.Input.AssetId, cancellationToken);
                await using (ObjectStorageReadResult read = await storage.OpenReadAsync(work.Input.QuarantineObjectKey, cancellationToken))
                await using (FileStream destination = new(source, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await read.Content.CopyToAsync(destination, cancellationToken);
                }
                await using FileStream scanStream = File.OpenRead(source);
                MalwareScanResult scan = await scanner.ScanAsync(scanStream, cancellationToken);
                if (scan.Status == MalwareScanStatus.Infected)
                {
                    await store.RejectAsync(work.Input.AssetId, "MEDIA.MALWARE_DETECTED", cancellationToken);
                    await jobs.CompleteAsync(claim, timeProvider.GetUtcNow(), cancellationToken);
                    return;
                }
                if (scan.Status != MalwareScanStatus.Clean)
                {
                    throw new StorageUnavailableException("The malware scanner was unavailable.");
                }
                MediaValidationResult validation = await validator.ValidateAsync(source, work.Input.Purpose, work.Input.ContentType, work.Input.FileName, cancellationToken);
                if (!validation.IsValid)
                {
                    await store.RejectAsync(work.Input.AssetId, validation.Code ?? "MEDIA.CONTENT_INVALID", cancellationToken);
                    await jobs.CompleteAsync(claim, timeProvider.GetUtcNow(), cancellationToken);
                    return;
                }
                await store.MarkProcessingAsync(work.Input.AssetId, cancellationToken);
                MediaProcessingResult processed = await processor.ProcessAsync(work.Input, source, output, cancellationToken);
                Dictionary<string, ObjectStoragePutResult> uploads = new(StringComparer.Ordinal);
                foreach (MediaVariantFile variant in processed.Variants)
                {
                    await using FileStream stream = File.OpenRead(variant.FilePath);
                    uploads[variant.Kind] = await storage.PutObjectAsync(
                        new ObjectStorageUploadRequest(variant.ObjectKey, variant.ContentType, stream, stream.Length),
                        cancellationToken);
                }
                string hash;
                await using (FileStream stream = File.OpenRead(source))
                {
                    hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
                }
                if (!string.Equals(hash, work.Input.DeclaredSha256, StringComparison.OrdinalIgnoreCase))
                {
                    await store.RejectAsync(work.Input.AssetId, "MEDIA.CHECKSUM_MISMATCH", cancellationToken);
                    await jobs.CompleteAsync(claim, timeProvider.GetUtcNow(), cancellationToken);
                    return;
                }
                await store.MarkReadyAsync(work.Input.AssetId, new FileInfo(source).Length, hash, processed.Variants, uploads, timeProvider.GetUtcNow(), cancellationToken);
                await jobs.CompleteAsync(claim, timeProvider.GetUtcNow(), cancellationToken);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { }
            }
        }
        catch (Exception exception) when (exception is StorageUnavailableException or IOException or InvalidOperationException)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IMediaJobStore jobs = scope.ServiceProvider.GetRequiredService<IMediaJobStore>();
            if (claim.AttemptCount >= _options.WorkerMaxAttempts)
            {
                await jobs.FailAsync(claim, timeProvider.GetUtcNow(), "MEDIA.PERMANENT_FAILURE", cancellationToken);
            }
            else
            {
                TimeSpan delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, claim.AttemptCount) * 5));
                await jobs.RetryAsync(claim, timeProvider.GetUtcNow(), "MEDIA.TRANSIENT_FAILURE", delay, cancellationToken);
            }
            MediaJobFailed(logger, exception, claim.AttemptCount);
        }
        finally
        {
            concurrency.Release();
        }
    }

    [LoggerMessage(EventId = 7003, Level = LogLevel.Warning, Message = "Media job failed; attempt {AttemptCount}")]
    private static partial void MediaJobFailed(ILogger logger, Exception exception, int attemptCount);
}
