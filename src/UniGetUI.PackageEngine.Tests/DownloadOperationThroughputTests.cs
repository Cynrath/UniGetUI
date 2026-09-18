using System.Net;
using System.Net.Sockets;
using System.Text;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Operations;
using UniGetUI.PackageEngine.Tests.Infrastructure.Builders;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

/// <summary>
/// Proves the HTTP <see cref="DownloadOperation"/> path surfaces measured download
/// progress through the generic operation layer: a throttled loopback server streams a
/// real payload with <c>Content-Length</c>, and at least one structured report must
/// carry a finite positive <c>BytesPerSecond</c> derived from real bytes over real
/// time. No synthetic speeds, no CLI parsing.
/// </summary>
public sealed class DownloadOperationThroughputTests
{
    private sealed class ProbeDownloadOperation(IPackage package, string downloadPath)
        : DownloadOperation(package, downloadPath)
    {
        public Task<OperationVeredict> InvokePerformOperationForTests() =>
            PerformOperation();
    }

    /// <summary>
    /// Minimal throttled HTTP/1.1 server over loopback TCP: serves one fixed payload
    /// with <c>Content-Length</c>, pacing chunks so the client's real clock observes
    /// distinct progress samples with measurable throughput.
    /// </summary>
    private sealed class ThrottledLoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly byte[] _payload;
        private readonly int _chunkSize;
        private readonly TimeSpan _chunkDelay;
        private readonly Task _serveTask;

        public Uri Url { get; }

        public ThrottledLoopbackServer(int totalBytes, int chunkSize, TimeSpan chunkDelay)
        {
            _payload = new byte[totalBytes];
            new Random(42).NextBytes(_payload);
            _chunkSize = chunkSize;
            _chunkDelay = chunkDelay;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Url = new Uri(
                $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/payload.bin"
            );
            _serveTask = Task.Run(ServeOnceAsync);
        }

        private async Task ServeOnceAsync()
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();

            // Consume the request headers.
            var request = new byte[4096];
            int seen = 0;
            while (seen < request.Length - 1)
            {
                int read = await stream.ReadAsync(request.AsMemory(seen));
                if (read == 0)
                    break;
                seen += read;
                if (Encoding.ASCII.GetString(request, 0, seen).Contains("\r\n\r\n"))
                    break;
            }

            string header =
                $"HTTP/1.1 200 OK\r\nContent-Length: {_payload.Length}\r\n"
                + "Content-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes);
            await stream.FlushAsync();

            for (int offset = 0; offset < _payload.Length; offset += _chunkSize)
            {
                int count = Math.Min(_chunkSize, _payload.Length - offset);
                await stream.WriteAsync(_payload.AsMemory(offset, count));
                await stream.FlushAsync();
                await Task.Delay(_chunkDelay);
            }
        }

        public void Dispose()
        {
            try
            {
                _serveTask.Wait(TimeSpan.FromSeconds(30));
            }
            catch
            {
                // Best-effort: a failed transfer still ends the test via its verdict.
            }
            _listener.Stop();
        }
    }

    [Fact]
    public async Task HttpDownload_ReportsMeasuredThroughput()
    {
        const int TotalBytes = 3 * 1024 * 1024;
        using var server = new ThrottledLoopbackServer(
            TotalBytes,
            chunkSize: 256 * 1024,
            chunkDelay: TimeSpan.FromMilliseconds(100)
        );

        var manager = new PackageManagerBuilder()
            .ConfigureDetails(helper =>
            {
                helper.PopulateDetails = details =>
                {
                    details.InstallerUrl = server.Url;
                    details.InstallerType = "exe";
                };
            })
            .Build();
        IPackage package = new PackageBuilder().WithManager(manager).Build();

        string downloadPath = Path.Join(
            Path.GetTempPath(),
            $"unigetui-throughput-{Guid.NewGuid():N}.bin"
        );
        try
        {
            using var operation = new ProbeDownloadOperation(package, downloadPath);
            var seenSpeeds = new List<double?>();
            operation.ProgressChanged += (_, progress) =>
            {
                lock (seenSpeeds)
                    seenSpeeds.Add(progress.BytesPerSecond);
            };

            OperationVeredict verdict = await operation.InvokePerformOperationForTests();

            Assert.Equal(OperationVeredict.Success, verdict);
            Assert.Equal(TotalBytes, new FileInfo(downloadPath).Length);

            List<double?> speeds;
            lock (seenSpeeds)
                speeds = [.. seenSpeeds];
            Assert.NotEmpty(speeds);
            Assert.Contains(
                speeds,
                static speed =>
                    speed.HasValue
                    && !double.IsNaN(speed.Value)
                    && !double.IsInfinity(speed.Value)
                    && speed.Value > 0
            );

            // The enriched report formats with a live throughput suffix.
            OperationProgress last = operation.CurrentProgress;
            Assert.True(last.HasThroughput);
            Assert.Contains("/s", OperationProgressFormatter.Format(last));
        }
        finally
        {
            if (File.Exists(downloadPath))
                File.Delete(downloadPath);
        }
    }
}
