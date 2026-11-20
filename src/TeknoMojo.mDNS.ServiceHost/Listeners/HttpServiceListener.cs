using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace TeknoMojo.mDNS.ServiceHost.Listeners;

/// <summary>
/// Simple HTTP server that responds to requests using Kestrel
/// </summary>
public class HttpServiceListener(
    int port,
    Dictionary<string, string> properties,
    ILogger<HttpServiceListener> logger) : IServiceListener
{
    private readonly ILogger<HttpServiceListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly int _port = port;
    private readonly Dictionary<string, string> _properties = properties;
    private IWebHost? _webHost;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting HTTP server on port {Port}", _port);

            _webHost = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    // Listen on all network interfaces without requiring admin privileges
                    options.ListenAnyIP(_port);
                })
                .ConfigureServices(services =>
                {
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
                    app.Run(async context =>
                    {
                        var request = context.Request;

                        _logger.LogInformation("HTTP request: {Method} {Path} from {RemoteIp}",
                            request.Method, request.Path, context.Connection.RemoteIpAddress);

                        // Build response
                        var responseData = new StringBuilder();
                        responseData.AppendLine("<!DOCTYPE html>");
                        responseData.AppendLine("<html>");
                        responseData.AppendLine("<head><title>mDNS Service Host</title></head>");
                        responseData.AppendLine("<body>");
                        responseData.AppendLine("<h1>mDNS HTTP Service</h1>");
                        responseData.AppendLine($"<p>Server is running on port {_port}</p>");

                        if (_properties.Count > 0)
                        {
                            responseData.AppendLine("<h2>Service Properties</h2>");
                            responseData.AppendLine("<ul>");
                            foreach (var kvp in _properties)
                            {
                                responseData.AppendLine($"<li><strong>{kvp.Key}:</strong> {kvp.Value}</li>");
                            }
                            responseData.AppendLine("</ul>");
                        }

                        responseData.AppendLine("<h2>Request Information</h2>");
                        responseData.AppendLine($"<p><strong>Path:</strong> {request.Path}{request.QueryString}</p>");
                        responseData.AppendLine($"<p><strong>Method:</strong> {request.Method}</p>");
                        responseData.AppendLine($"<p><strong>Client:</strong> {context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}</p>");
                        responseData.AppendLine("</body>");
                        responseData.AppendLine("</html>");

                        context.Response.ContentType = "text/html";
                        var bytes = Encoding.UTF8.GetBytes(responseData.ToString());
                        await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);

                        _logger.LogDebug("Sent HTTP response to {RemoteIp}", context.Connection.RemoteIpAddress);
                    });
                })
                .Build();

            await _webHost.StartAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("HTTP server started successfully on port {Port}", _port);
            _logger.LogInformation("  Accessible from network at: http://{{your-ip}}:{Port}", _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start HTTP listener on port {Port}", _port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_webHost != null)
        {
            _logger.LogInformation("Stopping HTTP server on port {Port}", _port);
            await _webHost.StopAsync().ConfigureAwait(false);
            _webHost.Dispose();
            _webHost = null;
            _logger.LogInformation("HTTP server on port {Port} stopped", _port);
        }
    }

    public void Dispose()
    {
        _webHost?.Dispose();
        GC.SuppressFinalize(this);
    }
}
