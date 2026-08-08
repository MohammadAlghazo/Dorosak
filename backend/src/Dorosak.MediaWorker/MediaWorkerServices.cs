using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Media;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

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
            .Validate(options => options.TimeoutSeconds is >= 5 and <= 600, "ClamAV timeout is invalid.")
            .ValidateOnStart();
        services.AddScoped<IMediaContentValidator, MagicByteMediaValidator>();
        services.AddScoped<MediaProcessRunner>();
        services.AddScoped<IMediaProcessor, FfmpegMediaProcessor>();
        services.AddScoped<IMalwareScanner, ClamAvInstreamScanner>();
        services.AddHostedService<MediaProcessingWorker>();
        services.AddHostedService<MediaCleanupWorker>();
        return services;
    }
}

public static class MediaWorkerTelemetry
{
    public const string MeterName = "Dorosak.MediaWorker";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> JobsClaimed = Meter.CreateCounter<long>("dorosak.media.jobs.claimed");

    public static readonly Counter<long> JobsCompleted = Meter.CreateCounter<long>("dorosak.media.jobs.completed");

    public static readonly Counter<long> JobsFailed = Meter.CreateCounter<long>("dorosak.media.jobs.failed");

    public static readonly Counter<long> CleanupSessions = Meter.CreateCounter<long>("dorosak.media.cleanup.sessions");
}

internal static partial class MediaContainerSmoke
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        MediaProcessRunner processes = scope.ServiceProvider.GetRequiredService<MediaProcessRunner>();
        IMalwareScanner scanner = scope.ServiceProvider.GetRequiredService<IMalwareScanner>();
        IMediaProcessor processor = scope.ServiceProvider.GetRequiredService<IMediaProcessor>();
        IObjectStorage storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        ILoggerFactory loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger("MediaContainerSmoke");
        string directory = Path.Combine(Path.GetTempPath(), "dorosak-media-smoke", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        var uploadedKeys = new List<string>();
        try
        {
            string source = Path.Combine(directory, "source.png");
            await processes.RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-f", "lavfi", "-i", "color=c=blue:s=1280x720", "-frames:v", "1", "-y", source], cancellationToken);
            await using (FileStream scanStream = File.OpenRead(source))
            {
                MalwareScanResult scan = await scanner.ScanAsync(scanStream, cancellationToken);
                if (scan.Status != MalwareScanStatus.Clean)
                {
                    throw new InvalidOperationException("Container smoke source did not pass ClamAV.");
                }
            }
            Guid assetId = Guid.CreateVersion7();
            MediaProcessingResult result = await processor.ProcessAsync(
                new MediaProcessingInput(assetId, Guid.NewGuid(), Guid.NewGuid(), MediaPurpose.CourseImage, "source.png", "image/png", new FileInfo(source).Length, new string('0', 64), "smoke/source"),
                source,
                Path.Combine(directory, "output"),
                cancellationToken);
            if (result.Variants.Count != 9)
            {
                throw new InvalidOperationException("Container smoke did not produce the complete image matrix.");
            }
            foreach (MediaVariantFile variant in result.Variants)
            {
                string key = $"smoke/{assetId:D}/{Path.GetFileName(variant.FilePath)}";
                await using FileStream content = File.OpenRead(variant.FilePath);
                await storage.PutObjectAsync(new ObjectStorageUploadRequest(key, variant.ContentType, content, content.Length), cancellationToken);
                uploadedKeys.Add(key);
            }
            await using ObjectStorageReadResult read = await storage.OpenReadAsync(uploadedKeys[0], cancellationToken);
            if (read.ContentLength is null or <= 0)
            {
                throw new InvalidOperationException("Container smoke could not read the generated object from storage.");
            }
            ContainerSmokePassed(logger, result.Variants.Count);
        }
        finally
        {
            foreach (string key in uploadedKeys)
            {
                await storage.DeleteObjectAsync(key, CancellationToken.None);
            }
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information, Message = "MediaWorker container smoke passed with {VariantCount} generated variants")]
    private static partial void ContainerSmokePassed(ILogger logger, int variantCount);
}

internal sealed partial class ClamAvInstreamScanner(IOptions<ClamAvOptions> options, ILogger<ClamAvInstreamScanner> logger)
    : IMalwareScanner
{
    private readonly ClamAvOptions _options = options.Value;

    public async Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
            await using NetworkStream network = client.GetStream();
            await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), timeout.Token);
            byte[] buffer = new byte[_options.ChunkBytes];
            int read;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            {
                byte[] size = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(read));
                await network.WriteAsync(size, timeout.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            await network.WriteAsync(new byte[4], timeout.Token);
            await network.FlushAsync(timeout.Token);
            string response = await ReadResponseAsync(network, timeout.Token);
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

    private static async Task<string> ReadResponseAsync(NetworkStream network, CancellationToken cancellationToken)
    {
        var response = new List<byte>(64);
        byte[] buffer = new byte[1];
        while (response.Count < 4096 && await network.ReadAsync(buffer, cancellationToken) > 0)
        {
            if (buffer[0] == 0)
            {
                break;
            }
            response.Add(buffer[0]);
        }
        return Encoding.ASCII.GetString([.. response]);
    }
}

internal sealed class MediaScannerUnavailableException(string message) : Exception(message);

public sealed class ClamAvOptions
{
    public const string SectionName = "Media:ClamAV";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 3310;

    public int ChunkBytes { get; set; } = 1024 * 1024;

    public int TimeoutSeconds { get; set; } = 120;
}

internal sealed class MagicByteMediaValidator(IOptions<MediaOptions> options) : IMediaContentValidator
{
    private readonly MediaOptions _options = options.Value;

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
            MediaPurpose.CourseDocument => IsPdf(header, read),
            MediaPurpose.SourceVideo => IsIsoBaseMedia(header, read),
            MediaPurpose.Caption => await IsWebVttAsync(filePath, cancellationToken),
            _ => false,
        };
        if (valid && purpose == MediaPurpose.CourseDocument)
        {
            valid = await IsStrictPdfAsync(filePath, stream, cancellationToken);
        }
        string invalidCode = purpose switch
        {
            MediaPurpose.CourseDocument => "MEDIA.PDF_INVALID_OR_ENCRYPTED",
            MediaPurpose.Caption => "MEDIA.WEBVTT_INVALID",
            _ => "MEDIA.MAGIC_BYTES_INVALID",
        };
        return valid
            ? new MediaValidationResult(true, null, declaredContentType)
            : new MediaValidationResult(false, invalidCode, null);
    }

    private static bool IsJpeg(byte[] bytes, int length) => length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    private static bool IsPng(byte[] bytes, int length) => length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    private static bool IsWebp(byte[] bytes, int length) => length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
    private static bool IsPdf(byte[] bytes, int length) => length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
    private static bool IsIsoBaseMedia(byte[] bytes, int length) => length >= 12 && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp";

    private async Task<bool> IsStrictPdfAsync(string filePath, FileStream stream, CancellationToken cancellationToken)
    {
        if (stream.Length > _options.PdfParserMaxBytes)
        {
            return false;
        }
        byte[] tail = new byte[Math.Min(4096, (int)stream.Length)];
        stream.Position = stream.Length - tail.Length;
        int tailRead = await stream.ReadAsync(tail, cancellationToken);
        if (!Encoding.ASCII.GetString(tail, 0, tailRead).Contains("%%EOF", StringComparison.Ordinal))
        {
            return false;
        }
        if (await ContainsAsciiTokenAsync(filePath, "/Encrypt", cancellationToken) ||
            await ContainsAsciiTokenAsync(filePath, "/Crypt", cancellationToken))
        {
            return false;
        }
        try
        {
            using PdfDocument document = PdfDocument.Open(filePath, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128 });
            if (document.IsEncrypted || document.NumberOfPages < 1 || document.NumberOfPages > _options.PdfParserMaxPages)
            {
                return false;
            }
            _ = document.GetPage(1);
            _ = document.GetPage(document.NumberOfPages);
            return true;
        }
        catch (PdfDocumentEncryptedException)
        {
            return false;
        }
        catch (PdfDocumentFormatException)
        {
            return false;
        }
        catch (PdfDocumentStackDepthException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<bool> ContainsAsciiTokenAsync(string filePath, string token, CancellationToken cancellationToken)
    {
        byte[] needle = Encoding.ASCII.GetBytes(token);
        byte[] buffer = new byte[64 * 1024 + needle.Length];
        int overlap = 0;
        await using FileStream stream = File.OpenRead(filePath);
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(overlap, buffer.Length - overlap), cancellationToken);
            if (read == 0)
            {
                return false;
            }
            int length = overlap + read;
            if (buffer.AsSpan(0, length).IndexOf(needle) >= 0)
            {
                return true;
            }
            overlap = Math.Min(needle.Length - 1, length);
            buffer.AsSpan(length - overlap, overlap).CopyTo(buffer);
        }
    }

    private static async Task<bool> IsWebVttAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            string? header = await reader.ReadLineAsync(cancellationToken);
            if (header is null || !header.StartsWith("WEBVTT", StringComparison.Ordinal))
            {
                return false;
            }
            bool hasCue = false;
            int lines = 0;
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (++lines > 100000 || line.Length > 4096 || line.Contains('\0'))
                {
                    return false;
                }
                if (line.Contains("-->", StringComparison.Ordinal) && line.Split("-->", StringSplitOptions.TrimEntries).Length == 2)
                {
                    hasCue = true;
                }
            }
            return hasCue;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}

internal sealed class MediaProcessTimeoutException(string executable) : InvalidOperationException($"{executable} exceeded the media processing timeout.");

internal sealed record MediaProcessResult(string StandardOutput, string StandardError);

internal sealed partial class MediaProcessRunner(IOptions<MediaOptions> options, ILogger<MediaProcessRunner> logger)
{
    private readonly MediaOptions _options = options.Value;

    public async Task<MediaProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
        Task<string> output = ReadBoundedAsync(process.StandardOutput, _options.ProcessOutputCharacterLimit, cancellationToken);
        Task<string> error = ReadBoundedAsync(process.StandardError, _options.ProcessOutputCharacterLimit, cancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await Task.WhenAll(output, error);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            throw new MediaProcessTimeoutException(executable);
        }
        string[] outputValues = await Task.WhenAll(output, error);
        if (process.ExitCode != 0)
        {
            MediaProcessFailed(logger, process.ExitCode, outputValues[1]);
            throw new InvalidOperationException("Media processing failed.");
        }
        return new MediaProcessResult(outputValues[0], outputValues[1]);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken cancellationToken)
    {
        var captured = new StringBuilder(Math.Min(limit, 8192));
        char[] buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            int remaining = limit - captured.Length;
            if (remaining > 0)
            {
                captured.Append(buffer, 0, Math.Min(remaining, read));
            }
        }
        return captured.ToString();
    }

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "Media process failed with code {ExitCode}: {Error}")]
    private static partial void MediaProcessFailed(ILogger logger, int exitCode, string error);
}

internal sealed class FfmpegMediaProcessor(IOptions<MediaOptions> options, MediaProcessRunner processes)
    : IMediaProcessor
{
    private static readonly int[] ImageWidths = [320, 640, 1280];
    private static readonly int[] VideoHeights = [360, 720, 1080];
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
            MediaPurpose.CourseDocument => await ProcessDocumentAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            MediaPurpose.SourceVideo => await ProcessVideoAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            MediaPurpose.Caption => await ProcessCaptionAsync(input, sourceFilePath, outputDirectory, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported media purpose."),
        };
    }

    private async Task<MediaProcessingResult> ProcessImageAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        MediaProbe sourceProbe = await ProbeAsync(source, cancellationToken);
        int[] widths = ImageWidths.Where(width => width <= sourceProbe.Width).ToArray();
        if (widths.Length == 0)
        {
            widths = [sourceProbe.Width];
        }
        var files = new List<MediaVariantFile>();
        foreach (int width in widths)
        {
            files.Add(await EncodeImageAsync(input.AssetId, source, output, width, "jpeg", "image/jpeg", ["-c:v", "mjpeg", "-q:v", "2"], cancellationToken));
            files.Add(await EncodeImageAsync(input.AssetId, source, output, width, "webp", "image/webp", ["-c:v", "libwebp", "-q:v", "82"], cancellationToken));
            files.Add(await EncodeImageAsync(input.AssetId, source, output, width, "avif", "image/avif", ["-c:v", "libaom-av1", "-still-picture", "1", "-crf", "32", "-b:v", "0"], cancellationToken));
        }
        return new MediaProcessingResult(files);
    }

    private async Task<MediaProcessingResult> ProcessDocumentAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        string file = Path.Combine(output, "document.pdf");
        Guid variantId = Guid.CreateVersion7();
        File.Copy(source, file, overwrite: true);
        return new MediaProcessingResult([await CreateVariantAsync(variantId, "document", file, "application/pdf", MediaObjectKeys.Ready(_options.Environment, input.AssetId, variantId, "document.pdf"), null, null, null, cancellationToken)]);
    }

    private async Task<MediaProcessingResult> ProcessVideoAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        MediaProbe sourceProbe = await ProbeAsync(source, cancellationToken);
        int[] heights = VideoHeights.Where(height => height <= sourceProbe.Height).ToArray();
        if (heights.Length == 0)
        {
            heights = [sourceProbe.Height - (sourceProbe.Height % 2)];
        }
        var files = new List<MediaVariantFile>();
        var masterEntries = new List<(Guid Id, int Width, int Height, int Bandwidth)>();
        foreach (int height in heights)
        {
            int width = Math.Max(2, (int)Math.Round((double)sourceProbe.Width * height / sourceProbe.Height / 2, MidpointRounding.AwayFromZero) * 2);
            Guid groupId = Guid.CreateVersion7();
            string fmp4 = Path.Combine(output, $"video-{height}p.mp4");
            await processes.RunAsync("ffmpeg", VideoArguments(source, width, height, sourceProbe.HasAudio, ["-movflags", "+frag_keyframe+empty_moov+default_base_moof", "-f", "mp4", "-y", fmp4]), cancellationToken);
            Guid fmp4Id = Guid.CreateVersion7();
            files.Add(await CreateVariantAsync(fmp4Id, $"video-fmp4-{height}p", fmp4, "video/mp4", MediaObjectKeys.Ready(_options.Environment, input.AssetId, fmp4Id, Path.GetFileName(fmp4)), width, height, sourceProbe.DurationSeconds, cancellationToken));

            string playlist = Path.Combine(output, $"hls-{height}p.m3u8");
            string init = $"hls-{height}p-init.mp4";
            string segmentPattern = Path.Combine(output, $"hls-{height}p-%05d.m4s");
            await processes.RunAsync("ffmpeg", VideoArguments(source, width, height, sourceProbe.HasAudio, ["-hls_time", "6", "-hls_playlist_type", "vod", "-hls_segment_type", "fmp4", "-hls_fmp4_init_filename", init, "-hls_segment_filename", segmentPattern, "-y", playlist]), cancellationToken);
            files.Add(await CreateVariantAsync(groupId, $"video-hls-{height}p-playlist", playlist, "application/vnd.apple.mpegurl", MediaObjectKeys.Ready(_options.Environment, input.AssetId, groupId, Path.GetFileName(playlist)), width, height, sourceProbe.DurationSeconds, cancellationToken));
            foreach (string artifact in Directory.EnumerateFiles(output, $"hls-{height}p-*").OrderBy(path => path, StringComparer.Ordinal))
            {
                bool segment = Path.GetExtension(artifact).Equals(".m4s", StringComparison.OrdinalIgnoreCase);
                string suffix = segment ? $"segment-{Path.GetFileNameWithoutExtension(artifact)[^5..]}" : "init";
                Guid artifactId = Guid.CreateVersion7();
                files.Add(await CreateVariantAsync(artifactId, $"video-hls-{height}p-{suffix}", artifact, segment ? "video/iso.segment" : "video/mp4", MediaObjectKeys.Ready(_options.Environment, input.AssetId, groupId, Path.GetFileName(artifact)), width, height, sourceProbe.DurationSeconds, cancellationToken));
            }
            int bandwidth = height switch { <= 360 => 800000, <= 720 => 2800000, _ => 5000000 };
            masterEntries.Add((groupId, width, height, bandwidth));
        }
        Guid masterId = Guid.CreateVersion7();
        string master = Path.Combine(output, "master.m3u8");
        await File.WriteAllLinesAsync(master, ["#EXTM3U", "#EXT-X-VERSION:7", .. masterEntries.Select(entry => $"#EXT-X-STREAM-INF:BANDWIDTH={entry.Bandwidth},RESOLUTION={entry.Width}x{entry.Height}\n../{entry.Id:D}/hls-{entry.Height}p.m3u8")], cancellationToken);
        files.Add(await CreateVariantAsync(masterId, "video-hls-master", master, "application/vnd.apple.mpegurl", MediaObjectKeys.Ready(_options.Environment, input.AssetId, masterId, "master.m3u8"), null, null, sourceProbe.DurationSeconds, cancellationToken));
        Guid posterId = Guid.CreateVersion7();
        string poster = Path.Combine(output, "poster.jpg");
        decimal posterOffset = Math.Min(10m, (sourceProbe.DurationSeconds ?? 0m) / 10m);
        await processes.RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-ss", posterOffset.ToString(CultureInfo.InvariantCulture), "-i", source, "-frames:v", "1", "-vf", "scale='min(1280,iw)':-2", "-map_metadata", "-1", "-c:v", "mjpeg", "-q:v", "2", "-y", poster], cancellationToken);
        MediaProbe posterProbe = await ProbeAsync(poster, cancellationToken);
        files.Add(await CreateVariantAsync(posterId, "video-poster", poster, "image/jpeg", MediaObjectKeys.Ready(_options.Environment, input.AssetId, posterId, "poster.jpg"), posterProbe.Width, posterProbe.Height, null, cancellationToken));
        return new MediaProcessingResult(files);
    }

    private static async Task<MediaProcessingResult> ProcessCaptionAsync(MediaProcessingInput input, string source, string output, CancellationToken cancellationToken)
    {
        if (input.CaptionTrackId is null || string.IsNullOrWhiteSpace(input.CaptionObjectKey))
        {
            throw new InvalidOperationException("Caption processing requires a caption track association.");
        }
        string file = Path.Combine(output, "caption.vtt");
        File.Copy(source, file, overwrite: true);
        return new MediaProcessingResult([await CreateVariantAsync(input.CaptionTrackId.Value, "caption", file, "text/vtt", input.CaptionObjectKey, null, null, null, cancellationToken)]);
    }

    private async Task<MediaVariantFile> EncodeImageAsync(Guid assetId, string source, string output, int width, string format, string contentType, IReadOnlyList<string> encoderArguments, CancellationToken cancellationToken)
    {
        Guid variantId = Guid.CreateVersion7();
        string file = Path.Combine(output, $"image-{width}.{format}");
        await processes.RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-vf", $"scale={width}:-2:flags=lanczos", "-frames:v", "1", "-map_metadata", "-1", .. encoderArguments, "-y", file], cancellationToken);
        MediaProbe probe = await ProbeAsync(file, cancellationToken);
        return await CreateVariantAsync(variantId, $"image-{format}-{width}", file, contentType, MediaObjectKeys.Ready(_options.Environment, assetId, variantId, Path.GetFileName(file)), probe.Width, probe.Height, null, cancellationToken);
    }

    private static async Task<MediaVariantFile> CreateVariantAsync(Guid variantId, string kind, string filePath, string contentType, string objectKey, int? width, int? height, decimal? durationSeconds, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        string sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        return new MediaVariantFile(variantId, kind, filePath, contentType, objectKey, sha256, width, height, durationSeconds);
    }

    private async Task<MediaProbe> ProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        MediaProcessResult result = await processes.RunAsync("ffprobe", ["-v", "error", "-show_entries", "stream=codec_type,width,height:format=duration", "-of", "json", filePath], cancellationToken);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement streams = document.RootElement.GetProperty("streams");
        JsonElement video = streams.EnumerateArray().First(stream => stream.GetProperty("codec_type").GetString() == "video");
        int width = video.GetProperty("width").GetInt32();
        int height = video.GetProperty("height").GetInt32();
        bool hasAudio = streams.EnumerateArray().Any(stream => stream.GetProperty("codec_type").GetString() == "audio");
        string? durationText = document.RootElement.GetProperty("format").TryGetProperty("duration", out JsonElement duration) ? duration.GetString() : null;
        decimal? durationSeconds = decimal.TryParse(durationText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
        return new MediaProbe(width, height, durationSeconds, hasAudio);
    }

    private static IReadOnlyList<string> VideoArguments(string source, int width, int height, bool hasAudio, IReadOnlyList<string> outputArguments) =>
    ["-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-map", "0:v:0", .. (hasAudio ? new[] { "-map", "0:a:0?" } : []), "-vf", $"scale={width}:{height}:flags=lanczos", "-map_metadata", "-1", "-c:v", "libx264", "-preset", "medium", "-crf", "23", "-force_key_frames", "expr:gte(t,n_forced*6)", "-c:a", "aac", "-b:a", "128k", .. outputArguments];

    private sealed record MediaProbe(int Width, int Height, decimal? DurationSeconds, bool HasAudio);
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
            MediaWorkerTelemetry.JobsClaimed.Add(1);
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
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tempDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            string source = Path.Combine(tempDirectory, "source.bin");
            string output = Path.Combine(tempDirectory, "output");
            try
            {
                if (work.ExistingState == MediaAssetState.Uploaded.ToString())
                {
                    await store.MarkScanningAsync(work.Input.AssetId, cancellationToken);
                }
                else
                {
                    await store.ResetForRetryAsync(work.Input.AssetId, cancellationToken);
                }
                await using (ObjectStorageReadResult read = await storage.OpenReadAsync(work.Input.QuarantineObjectKey, cancellationToken))
                await using (FileStream destination = new(source, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await CopyBoundedAsync(read.Content, destination, work.Input.DeclaredBytes, cancellationToken);
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
                    throw new MediaScannerUnavailableException("The malware scanner was unavailable.");
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
                await store.MarkReadyAsync(work.Input.AssetId, new FileInfo(source).Length, hash, processed.Variants, uploads, timeProvider.GetUtcNow(), cancellationToken);
                await jobs.CompleteAsync(claim, timeProvider.GetUtcNow(), cancellationToken);
                MediaWorkerTelemetry.JobsCompleted.Add(1);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { }
            }
        }
        catch (Exception exception) when (exception is StorageUnavailableException or MediaScannerUnavailableException or IOException or InvalidOperationException)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IMediaJobStore jobs = scope.ServiceProvider.GetRequiredService<IMediaJobStore>();
            if (claim.AttemptCount >= _options.WorkerMaxAttempts)
            {
                using IServiceScope failureScope = scopeFactory.CreateScope();
                IMediaProcessingStore store = failureScope.ServiceProvider.GetRequiredService<IMediaProcessingStore>();
                string failureCode = exception is MediaScannerUnavailableException
                    ? "MEDIA.SCANNER_UNAVAILABLE"
                    : "MEDIA.PERMANENT_FAILURE";
                await store.RejectAsync(claim.AssetId, failureCode, cancellationToken);
                await jobs.FailAsync(claim, timeProvider.GetUtcNow(), failureCode, cancellationToken);
            }
            else
            {
                double exponentialSeconds = Math.Min(300, Math.Pow(2, claim.AttemptCount) * 5);
                TimeSpan delay = TimeSpan.FromSeconds(exponentialSeconds * (1 + (Random.Shared.NextDouble() * 0.2)));
                await jobs.RetryAsync(claim, timeProvider.GetUtcNow(), "MEDIA.TRANSIENT_FAILURE", delay, cancellationToken);
            }
            MediaJobFailed(logger, exception, claim.AttemptCount);
            MediaWorkerTelemetry.JobsFailed.Add(1);
        }
        finally
        {
            concurrency.Release();
        }
    }

    [LoggerMessage(EventId = 7003, Level = LogLevel.Warning, Message = "Media job failed; attempt {AttemptCount}")]
    private static partial void MediaJobFailed(ILogger logger, Exception exception, int attemptCount);

    private static async Task CopyBoundedAsync(Stream source, Stream destination, long maximumBytes, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidOperationException("The stored object exceeds the declared media size.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total != maximumBytes)
        {
            throw new InvalidOperationException("The stored object is shorter than the declared media size.");
        }
    }
}

internal sealed partial class MediaCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaOptions> options,
    ILogger<MediaCleanupWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly MediaOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is IOException or StorageUnavailableException or DbUpdateException)
            {
                CleanupFailed(logger, exception);
            }
        }
    }

    internal async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        DorosakDbContext db = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        IObjectStorage storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        DateTimeOffset now = timeProvider.GetUtcNow();
        UploadSession[] sessions = await db.Set<UploadSession>()
            .Where(session => (session.State == UploadSessionState.Initiated || session.State == UploadSessionState.Uploading) &&
                session.ExpiresAt <= now)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        foreach (UploadSession session in sessions)
        {
            if (session.MultipartUploadId is not null)
            {
                await storage.AbortMultipartUploadAsync(session.QuarantineObjectKey, session.MultipartUploadId, cancellationToken);
            }
            session.Expire(now);
            db.Set<AuditLog>().Add(AuditLog.Create(
                session.OwnerUserId,
                "media.upload-session-expired",
                "UploadSession",
                session.Id,
                "succeeded",
                null,
                now));
            MediaWorkerTelemetry.CleanupSessions.Add(1);
        }
        if (sessions.Length > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        DateTimeOffset orphanCutoff = now.Subtract(_options.OrphanGracePeriod);
        UploadSession[] orphans = await db.Set<UploadSession>()
            .Where(session => session.State == UploadSessionState.Expired && session.ExpiresAt <= orphanCutoff)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        foreach (UploadSession session in orphans)
        {
            MediaAsset asset = await db.Set<MediaAsset>().SingleAsync(candidate => candidate.Id == session.AssetId, cancellationToken);
            if (await db.Set<LessonRevision>().AnyAsync(revision => revision.MediaAssetId == asset.Id, cancellationToken))
            {
                continue;
            }
            await storage.DeleteObjectAsync(session.QuarantineObjectKey, cancellationToken);
            asset.Delete(now);
            CaptionTrack? caption = await db.Set<CaptionTrack>().SingleOrDefaultAsync(
                track => track.SourceMediaAssetId == asset.Id,
                cancellationToken);
            caption?.Reject("MEDIA.SESSION_EXPIRED", now);
            db.Set<AuditLog>().Add(AuditLog.Create(
                asset.OwnerUserId,
                "media.asset-deleted",
                "MediaAsset",
                asset.Id,
                "succeeded",
                null,
                now));
        }
        if (orphans.Length > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(EventId = 7004, Level = LogLevel.Warning, Message = "Media cleanup failed")]
    private static partial void CleanupFailed(ILogger logger, Exception exception);
}
