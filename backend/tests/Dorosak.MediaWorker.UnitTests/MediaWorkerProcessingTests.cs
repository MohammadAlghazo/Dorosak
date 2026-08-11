using System.Globalization;
using System.Text;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.MediaWorker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dorosak.MediaWorker.UnitTests;

public sealed class MediaWorkerProcessingTests
{
    [Fact]
    public async Task ImageProcessor_ProducesFixedJpegWebpAvifMatrixWithMetadata()
    {
        string directory = CreateDirectory();
        try
        {
            var options = Options.Create(new MediaOptions { Environment = "test", ProcessTimeout = TimeSpan.FromMinutes(2) });
            var runner = new MediaProcessRunner(options, NullLogger<MediaProcessRunner>.Instance);
            string source = Path.Combine(directory, "source.png");
            await runner.RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-f", "lavfi", "-i", "color=c=blue:s=1280x720", "-frames:v", "1", "-y", source], TestContext.Current.CancellationToken);
            var processor = new FfmpegMediaProcessor(options, runner);
            Guid assetId = Guid.CreateVersion7();

            MediaProcessingResult result = await processor.ProcessAsync(
                new MediaProcessingInput(assetId, Guid.NewGuid(), Guid.NewGuid(), MediaPurpose.CourseImage, "image.png", "image/png", new FileInfo(source).Length, new string('a', 64), "quarantine/test"),
                source,
                Path.Combine(directory, "output"),
                TestContext.Current.CancellationToken);

            Assert.Equal(9, result.Variants.Count);
            Assert.Equal(3, result.Variants.Count(variant => variant.ContentType == "image/jpeg"));
            Assert.Equal(3, result.Variants.Count(variant => variant.ContentType == "image/webp"));
            Assert.Equal(3, result.Variants.Count(variant => variant.ContentType == "image/avif"));
            Assert.All(result.Variants, variant =>
            {
                Assert.True(File.Exists(variant.FilePath));
                Assert.Matches("^[0-9a-f]{64}$", variant.Sha256);
                Assert.True(variant.Width is > 0);
                Assert.True(variant.Height is > 0);
            });
        }
        catch (MediaExecutableNotFoundException)
        {
            Assert.Skip("FFmpeg or ffprobe is unavailable on the test host.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task VideoProcessor_ProducesFmp4HlsMasterSixSecondSegmentsAndPoster()
    {
        string directory = CreateDirectory();
        try
        {
            var options = Options.Create(new MediaOptions { Environment = "test", ProcessTimeout = TimeSpan.FromMinutes(3) });
            var runner = new MediaProcessRunner(options, NullLogger<MediaProcessRunner>.Instance);
            string source = Path.Combine(directory, "source.mp4");
            await runner.RunAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=15", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-t", "2", "-shortest", "-c:v", "libx264", "-c:a", "aac", "-y", source], TestContext.Current.CancellationToken);
            var processor = new FfmpegMediaProcessor(options, runner);
            Guid assetId = Guid.CreateVersion7();

            MediaProcessingResult result = await processor.ProcessAsync(
                new MediaProcessingInput(assetId, Guid.NewGuid(), Guid.NewGuid(), MediaPurpose.SourceVideo, "source.mp4", "video/mp4", new FileInfo(source).Length, new string('a', 64), "quarantine/test"),
                source,
                Path.Combine(directory, "output"),
                TestContext.Current.CancellationToken);

            Assert.Contains(result.Variants, variant => variant.Kind == "video-hls-master");
            Assert.Contains(result.Variants, variant => variant.Kind == "video-poster");
            Assert.Contains(result.Variants, variant => variant.Kind == "video-fmp4-360p");
            Assert.Contains(result.Variants, variant => variant.Kind == "video-fmp4-720p");
            Assert.DoesNotContain(result.Variants, variant => variant.Kind == "video-fmp4-1080p");
            string[] playlists = result.Variants.Where(variant => variant.Kind.EndsWith("playlist", StringComparison.Ordinal)).Select(variant => File.ReadAllText(variant.FilePath)).ToArray();
            Assert.Equal(2, playlists.Length);
            Assert.All(playlists, playlist =>
            {
                Assert.Contains("#EXT-X-MAP:", playlist, StringComparison.Ordinal);
                Assert.Contains("#EXTINF:", playlist, StringComparison.Ordinal);
                Assert.DoesNotContain(playlist.Split('\n'), line => line.StartsWith("#EXTINF:", StringComparison.Ordinal) && decimal.Parse(line[8..].TrimEnd(','), CultureInfo.InvariantCulture) > 6.1m);
            });
            string master = File.ReadAllText(result.Variants.Single(variant => variant.Kind == "video-hls-master").FilePath);
            Assert.Contains("hls-360p.m3u8", master, StringComparison.Ordinal);
            Assert.Contains("hls-720p.m3u8", master, StringComparison.Ordinal);
            Assert.All(result.Variants, variant => Assert.Matches("^[0-9a-f]{64}$", variant.Sha256));
        }
        catch (MediaExecutableNotFoundException)
        {
            Assert.Skip("FFmpeg or ffprobe is unavailable on the test host.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ProcessRunner_KillsLongProcessAtConfiguredTimeout()
    {
        var options = Options.Create(new MediaOptions { ProcessTimeout = TimeSpan.FromMilliseconds(100) });
        var runner = new MediaProcessRunner(options, NullLogger<MediaProcessRunner>.Instance);
        string executable;
        IReadOnlyList<string> arguments;
        if (OperatingSystem.IsWindows())
        {
            executable = "powershell";
            arguments = ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 5"];
        }
        else
        {
            executable = "sh";
            arguments = ["-c", "sleep 5"];
        }

        await Assert.ThrowsAsync<MediaProcessTimeoutException>(() => runner.RunAsync(executable, arguments, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessRunner_ReportsAMissingExecutableWithoutHidingOtherProcessFailures()
    {
        var runner = new MediaProcessRunner(
            Options.Create(new MediaOptions()),
            NullLogger<MediaProcessRunner>.Instance);
        string executable = $"dorosak-missing-media-tool-{Guid.CreateVersion7():N}";

        MediaExecutableNotFoundException exception = await Assert.ThrowsAsync<MediaExecutableNotFoundException>(() =>
            runner.RunAsync(executable, [], TestContext.Current.CancellationToken));

        Assert.Equal(executable, exception.Executable);
    }

    [Fact]
    public async Task PdfValidator_RejectsEncryptionMarkerAndMalformedStructure()
    {
        string directory = CreateDirectory();
        try
        {
            var validator = new MagicByteMediaValidator(Options.Create(new MediaOptions()));
            string encrypted = Path.Combine(directory, "encrypted.pdf");
            await File.WriteAllTextAsync(encrypted, "%PDF-1.7\n/Encrypt 4 0 R\n%%EOF", Encoding.ASCII, TestContext.Current.CancellationToken);
            MediaValidationResult encryptedResult = await validator.ValidateAsync(encrypted, MediaPurpose.CourseDocument, "application/pdf", "encrypted.pdf", TestContext.Current.CancellationToken);
            Assert.False(encryptedResult.IsValid);
            Assert.Equal("MEDIA.PDF_INVALID_OR_ENCRYPTED", encryptedResult.Code);

            string malformed = Path.Combine(directory, "malformed.pdf");
            await File.WriteAllTextAsync(malformed, "%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF", Encoding.ASCII, TestContext.Current.CancellationToken);
            MediaValidationResult malformedResult = await validator.ValidateAsync(malformed, MediaPurpose.CourseDocument, "application/pdf", "malformed.pdf", TestContext.Current.CancellationToken);
            Assert.False(malformedResult.IsValid);
            Assert.Equal("MEDIA.PDF_INVALID_OR_ENCRYPTED", malformedResult.Code);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task WebVttValidator_RequiresHeaderAndCueTimestamp()
    {
        string directory = CreateDirectory();
        try
        {
            var validator = new MagicByteMediaValidator(Options.Create(new MediaOptions()));
            string valid = Path.Combine(directory, "valid.vtt");
            await File.WriteAllTextAsync(valid, "WEBVTT\n\n00:00.000 --> 00:01.000\nHello\n", Encoding.UTF8, TestContext.Current.CancellationToken);
            Assert.True((await validator.ValidateAsync(valid, MediaPurpose.Caption, "text/vtt", "valid.vtt", TestContext.Current.CancellationToken)).IsValid);
            string invalid = Path.Combine(directory, "invalid.vtt");
            await File.WriteAllTextAsync(invalid, "not a webvtt file", Encoding.UTF8, TestContext.Current.CancellationToken);
            Assert.Equal("MEDIA.WEBVTT_INVALID", (await validator.ValidateAsync(invalid, MediaPurpose.Caption, "text/vtt", "invalid.vtt", TestContext.Current.CancellationToken)).Code);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"dorosak-media-processing-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
