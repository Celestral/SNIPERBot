using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SNIPERBot;

/// <summary>
/// BackgroundService needed to host the console application properly inside a Container.
///
/// This is "required" because of the following action the Container App (AKS) (and AppService) tries to perform:
///     Startup probe failed: dial tcp 10.250.0.44:80: connect: connection refused
/// Simple Console app does not host a Http Listener :^), but now it does :^)
/// </summary>
public sealed class PodHealthyListener : BackgroundService
{
    private readonly ILogger _logger;

    public PodHealthyListener(ILogger<SniperHostedService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Started the PodHealthyListener");

        var listener = new TcpListener(IPAddress.Any, 80);
        listener.Start();


        while (!stoppingToken.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using Stream stream = client.GetStream();

            var continueRead = true;
            while (continueRead)
            {
                // the the buffer empty

                var requestBuffer = new byte[100];
                var size = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length);

                continueRead = size == 100;
            }

            // respond with generic message

            var content = "I'm alive!";

            var writer = new StreamWriter(client.GetStream());

            await writer.WriteAsync("HTTP/1.0 200 OK");
            await writer.WriteAsync(Environment.NewLine);
            await writer.WriteAsync("Content-Type: text/plain; charset=UTF-8");
            await writer.WriteAsync(Environment.NewLine);
            await writer.WriteAsync("Content-Length: " + content.Length);
            await writer.WriteAsync(Environment.NewLine);
            await writer.WriteAsync(Environment.NewLine);
            await writer.WriteAsync(content);
            await writer.FlushAsync();

            writer.Close();
        }

        _logger.LogInformation("Stopping the PodHealthyListener");
    }
}