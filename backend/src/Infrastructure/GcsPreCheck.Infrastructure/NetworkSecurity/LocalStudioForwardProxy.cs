using System.Net;
using System.Net.Sockets;
using System.Text;
using GcsPreCheck.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GcsPreCheck.Infrastructure.NetworkSecurity;

public interface ILocalStudioForwardProxy
{
    int Port { get; }
    void Start();
    void Stop();
}

public class LocalStudioForwardProxy : BackgroundService, ILocalStudioForwardProxy
{
    private readonly IStudioNetworkValidator _validator;
    private readonly ILogger<LocalStudioForwardProxy> _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; } = 8888;
    public TargetEnvironment ActiveEnvironment { get; set; } = TargetEnvironment.Production;

    public LocalStudioForwardProxy(
        IStudioNetworkValidator validator,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<LocalStudioForwardProxy> logger)
    {
        _validator = validator;
        _logger = logger;
        if (int.TryParse(configuration["Proxy:Port"], out var configuredPort) && configuredPort > 0)
        {
            Port = configuredPort;
        }
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = ExecuteAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new TcpListener(IPAddress.Loopback, Port);
        try
        {
            _listener.Start();
            _logger.LogInformation("Local Studio Forward Proxy listening on 127.0.0.1:{Port}", Port);

            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientConnectionAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local Studio Forward Proxy encountered fatal error on port {Port}", Port);
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        await using (var clientStream = client.GetStream())
        {
            try
            {
                var buffer = new byte[8192];
                var bytesRead = await clientStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (bytesRead <= 0) return;

                var requestString = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                var firstLine = requestString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
                var parts = firstLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0].ToUpperInvariant();
                var target = parts[1];

                string host;
                int port = 443;

                if (method == "CONNECT")
                {
                    // HTTPS CONNECT host:port
                    var hostPort = target.Split(':');
                    host = hostPort[0];
                    if (hostPort.Length > 1 && int.TryParse(hostPort[1], out var p))
                        port = p;
                }
                else
                {
                    // Plain HTTP GET http://host/path
                    if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
                    {
                        host = uri.Host;
                        port = uri.Port;
                    }
                    else
                    {
                        var hostPort = target.Split(':');
                        host = hostPort[0];
                    }
                }

                var targetUri = new Uri($"{(method == "CONNECT" ? "https" : "http")}://{host}:{port}");

                // Validate destination through SSRF & Allowlist Policy
                var validation = await _validator.ValidateDestinationAsync(targetUri, ActiveEnvironment, ct);
                if (!validation.IsAllowed || validation.ResolvedIp == null)
                {
                    _logger.LogWarning("SSRF Proxy: BLOCKED connection to '{Target}' - Reason: {Reason}",
                        target, validation.FailureReason);

                    var forbiddenResponse = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\n[SSRF Blocked by Enterprise Studio Allowlist Policy]";
                    var forbiddenBytes = Encoding.ASCII.GetBytes(forbiddenResponse);
                    await clientStream.WriteAsync(forbiddenBytes, ct);
                    return;
                }

                // Establish tunnel to the validated IP address
                using var upstreamClient = new TcpClient();
                await upstreamClient.ConnectAsync(validation.ResolvedIp, port, ct);
                await using var upstreamStream = upstreamClient.GetStream();

                if (method == "CONNECT")
                {
                    // Respond to Chromium that tunnel is open
                    var okResponse = "HTTP/1.1 200 Connection Established\r\n\r\n";
                    var okBytes = Encoding.ASCII.GetBytes(okResponse);
                    await clientStream.WriteAsync(okBytes, ct);
                }
                else
                {
                    // Forward initial HTTP request bytes
                    await upstreamStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                }

                // Bi-directional byte forwarding
                var t1 = clientStream.CopyToAsync(upstreamStream, ct);
                var t2 = upstreamStream.CopyToAsync(clientStream, ct);
                await Task.WhenAny(t1, t2);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Proxy tunnel closed or reset.");
            }
        }
    }
}
