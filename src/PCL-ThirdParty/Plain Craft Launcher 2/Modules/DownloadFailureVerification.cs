using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using PCL.Core.App;
using PCL.Network;

namespace PCL;

internal static class DownloadFailureVerification
{
    public static async Task RunAsync()
    {
        foreach (var status in new[] { 403, 200 })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var requests = 0;
            var server = Task.Run(async () =>
            {
                try
                {
                    while (!timeout.IsCancellationRequested)
                    {
                        using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                        await using var stream = socket.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
                        Interlocked.Increment(ref requests);
                        var body = "incorrect-file";
                        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
                        await stream.WriteAsync(response, timeout.Token);
                    }
                }
                catch (OperationCanceledException) { }
            });
            var path = Path.Combine(ThirdPartyDistribution.DataRoot, "negative-test-" + status + ".bin");
            if (status == 200) await File.WriteAllTextAsync(path, "previous-valid-resource");
            var address = "http://127.0.0.1:" + port + "/file";
            var tracked = new DownloadFile(new[] { address }, path,
                new ModBase.FileChecker(actualSize: 14, hash: new string('0', 40)));
            var rejected = false;
            try
            {
                await FileDownloader.DownloadAsync(address, path, cancellationToken: timeout.Token,
                    enableParallelChunks: false, trackedFile: tracked);
            }
            catch (HttpRequestException error) when (status == 403 && error.StatusCode == HttpStatusCode.Forbidden) { rejected = true; }
            catch (IOException) when (status == 200) { rejected = true; }
            finally
            {
                timeout.Cancel();
                await server;
                listener.Stop();
            }
            if (!rejected || (status == 403 && File.Exists(path)) || File.Exists(path + ModNet.NetDownloadEnd))
                throw new InvalidOperationException("Rejected download left a successful or partial artifact: " + status);
            if (status == 200)
            {
                if (!File.Exists(path) || await File.ReadAllTextAsync(path) != "previous-valid-resource")
                    throw new InvalidOperationException("Failed resource update destroyed the previous file.");
                File.Delete(path);
            }
            if (status == 403 && requests != 1)
                throw new InvalidOperationException("An authorization denial was retried.");
        }
    }
}
