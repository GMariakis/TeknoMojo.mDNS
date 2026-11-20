using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TeknoMojo.mDNS.Core.ServiceDiscovery;
using TeknoMojo.mDNS.ServiceHost.Hubs;

namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// SignalR service listener that hosts a SignalR hub for real-time communication
/// </summary>
public class SignalRServiceListener(
    int port,
    Dictionary<string, string> properties,
    ILogger<SignalRServiceListener> logger)
    : IServiceListener
{
    private readonly ILogger<SignalRServiceListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int _port = port;
    private readonly Dictionary<string, string> _properties = properties;
    private IWebHost? _webHost;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var hubPath = _properties.GetValueOrDefault(
                SignalRServiceHelper.Constants.TxtKeyHubPath,
                SignalRServiceHelper.Constants.DefaultHubPath);

            _logger.LogInformation("Starting SignalR server on port {Port} with hub path: {HubPath}", _port, hubPath);

            _webHost = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenAnyIP(_port);
                })
                .ConfigureServices(services =>
                {
                    // Add SignalR services
                    services.AddSignalR(options =>
                    {
                        // Configure SignalR options
                        options.EnableDetailedErrors = true;
                        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                    });

                    // Add logging
                    services.AddLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Debug);
                    });

                    // Register the logger instance from constructor
                    services.AddSingleton(_logger);
                })
                .Configure(app =>
                {
                    // Enable WebSockets
                    app.UseWebSockets();

                    // Map the SignalR hub
                    app.UseSignalR(routes =>
                    {
                        routes.MapHub<NotificationHub>(hubPath);
                    });

                    // Add a simple HTTP handler for health check
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/health")
                        {
                            var bytes = Encoding.UTF8.GetBytes("SignalR Service is running");
                            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                        }
                        else if (context.Request.Path == "/info")
                        {
                            context.Response.ContentType = "application/json";

                            var version = _properties.GetValueOrDefault(
                                SignalRServiceHelper.Constants.TxtKeyVersion,
                                SignalRServiceHelper.Constants.DefaultVersion);

                            var featuresStr = _properties.GetValueOrDefault(
                                SignalRServiceHelper.Constants.TxtKeyFeatures,
                                string.Join(",", new[]
                                {
                                    SignalRServiceHelper.Features.Messaging,
                                    SignalRServiceHelper.Features.GroupMessaging,
                                    SignalRServiceHelper.Features.PrivateMessaging,
                                    SignalRServiceHelper.Features.ConnectionTracking
                                }));

                            var info = new
                            {
                                service = "SignalR Service",
                                hubPath = hubPath,
                                port = _port,
                                version = version,
                                features = featuresStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                endpoints = new[]
                                {
                                    $"ws://{{host}}:{_port}{hubPath}",
                                    $"http://{{host}}:{_port}/health",
                                    $"http://{{host}}:{_port}/info"
                                }
                            };
                            var json = System.Text.Json.JsonSerializer.Serialize(info);
                            var bytes = Encoding.UTF8.GetBytes(json);
                            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                        }
                    });
                })
                .Build();

            await _webHost.StartAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SignalR server started successfully on port {Port}", _port);
            _logger.LogInformation("Hub endpoint: ws://localhost:{Port}{HubPath}", _port, hubPath);
            _logger.LogInformation("Health check: http://localhost:{Port}/health", _port);
            _logger.LogInformation("Info endpoint: http://localhost:{Port}/info", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR listener on port {Port}", _port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_webHost != null)
        {
            _logger.LogInformation("Stopping SignalR server on port {Port}", _port);
            await _webHost.StopAsync().ConfigureAwait(false);
            _webHost.Dispose();
            _webHost = null;
            _logger.LogInformation("SignalR server on port {Port} stopped", _port);
        }
    }

    public void Dispose()
    {
        _webHost?.Dispose();
        GC.SuppressFinalize(this);
    }
}
