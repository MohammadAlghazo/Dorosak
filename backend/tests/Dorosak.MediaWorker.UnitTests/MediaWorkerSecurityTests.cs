using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Dorosak.Application.Features.Media;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.MediaWorker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dorosak.MediaWorker.UnitTests;

public sealed class MediaWorkerSecurityTests
{
    private const string Eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    [Fact]
    public async Task ClamAvScanner_StreamsEicarAndReturnsInfected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = ServeClamResponseAsync(listener, "stream: Eicar-Test-Signature FOUND\0", timeout.Token);
            var scanner = new ClamAvInstreamScanner(
                Options.Create(new ClamAvOptions { Host = "127.0.0.1", Port = port, ChunkBytes = 4096, TimeoutSeconds = 5 }),
                NullLogger<ClamAvInstreamScanner>.Instance);

            await using var content = new MemoryStream(Encoding.ASCII.GetBytes(Eicar));
            MalwareScanResult result = await scanner.ScanAsync(content, timeout.Token);

            Assert.Equal(MalwareScanStatus.Infected, result.Status);
            Assert.Contains("Eicar-Test-Signature", result.Signature, StringComparison.Ordinal);
            await server;
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ClamAvScanner_ConnectionFailureNeverReturnsClean()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var scanner = new ClamAvInstreamScanner(
            Options.Create(new ClamAvOptions { Host = "127.0.0.1", Port = port, ChunkBytes = 4096, TimeoutSeconds = 5 }),
            NullLogger<ClamAvInstreamScanner>.Instance);

        await using var content = new MemoryStream(Encoding.ASCII.GetBytes(Eicar));
        MalwareScanResult result = await scanner.ScanAsync(content, TestContext.Current.CancellationToken);

        Assert.Equal(MalwareScanStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task MagicValidator_RejectsDeclaredPdfWithExecutableBytes()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"dorosak-media-test-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllBytesAsync(filePath, [0x4D, 0x5A, 0x90, 0x00], TestContext.Current.CancellationToken);
            var validator = new MagicByteMediaValidator(Options.Create(new MediaOptions()));

            MediaValidationResult result = await validator.ValidateAsync(
                filePath,
                MediaPurpose.CourseDocument,
                "application/pdf",
                "document.pdf",
                TestContext.Current.CancellationToken);

            Assert.False(result.IsValid);
            Assert.Equal("MEDIA.PDF_INVALID_OR_ENCRYPTED", result.Code);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealClamAvInstream_ReturnsExpectedResultWhenComposeIsAvailable(bool infected)
    {
        int port = 0;
        if (!TryGetComposeValue("DOROSAK_CLAMAV_PORT", out string? portValue) ||
            !int.TryParse(portValue, out port) ||
            !await CanConnectAsync(port))
        {
            Assert.Skip("The local Compose ClamAV service is unavailable.");
        }
        var scanner = new ClamAvInstreamScanner(
            Options.Create(new ClamAvOptions { Host = "127.0.0.1", Port = port, ChunkBytes = 4096, TimeoutSeconds = 10 }),
            NullLogger<ClamAvInstreamScanner>.Instance);
        string value = infected ? Eicar : "Dorosak clean caption content";
        await using var content = new MemoryStream(Encoding.ASCII.GetBytes(value));

        MalwareScanResult result = await scanner.ScanAsync(content, TestContext.Current.CancellationToken);

        Assert.Equal(infected ? MalwareScanStatus.Infected : MalwareScanStatus.Clean, result.Status);
    }

    private static async Task ServeClamResponseAsync(TcpListener listener, string response, CancellationToken cancellationToken)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = client.GetStream();
        byte[] command = new byte[10];
        await stream.ReadExactlyAsync(command, cancellationToken);
        Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));

        using var content = new MemoryStream();
        byte[] sizeBuffer = new byte[4];
        while (true)
        {
            await stream.ReadExactlyAsync(sizeBuffer, cancellationToken);
            int chunkLength = BinaryPrimitives.ReadInt32BigEndian(sizeBuffer);
            if (chunkLength == 0)
            {
                break;
            }
            byte[] chunk = new byte[chunkLength];
            await stream.ReadExactlyAsync(chunk, cancellationToken);
            await content.WriteAsync(chunk, cancellationToken);
        }
        Assert.Equal(Eicar, Encoding.ASCII.GetString(content.ToArray()));
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool TryGetComposeValue(string name, out string? value)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
        {
            directory = directory.Parent;
        }
        string? environmentPath = directory is null ? null : Path.Combine(directory.FullName, ".env.local");
        if (environmentPath is null || !File.Exists(environmentPath))
        {
            value = null;
            return false;
        }
        string prefix = name + "=";
        string? line = File.ReadLines(environmentPath).FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        value = line?[prefix.Length..];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<bool> CanConnectAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
