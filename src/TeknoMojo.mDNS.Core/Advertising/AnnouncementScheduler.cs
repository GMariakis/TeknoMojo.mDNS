namespace TeknoMojo.mDNS.Core.Advertising;

/// <summary>
/// Schedules service announcements according to RFC 6762
/// Initial announcements at t=0s, t=1s, then exponential backoff
/// </summary>
public class AnnouncementScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _scheduledAnnouncements = new();
    private readonly ILogger? _logger;
    private bool _disposed;

    public event EventHandler<string>? AnnouncementDue;

    public AnnouncementScheduler(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Schedule announcements for a service
    /// </summary>
    public void ScheduleAnnouncements(string serviceName, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AnnouncementScheduler));

        // Cancel existing announcements if any
        CancelAnnouncements(serviceName);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _scheduledAnnouncements[serviceName] = cts;

        // Start announcement task
        _ = Task.Run(() => RunAnnouncementScheduleAsync(serviceName, cts.Token), cts.Token);

        _logger?.LogDebug("Scheduled announcements for {ServiceName}", serviceName);
    }

    /// <summary>
    /// Cancel announcements for a service
    /// </summary>
    public void CancelAnnouncements(string serviceName)
    {
        if (_scheduledAnnouncements.TryRemove(serviceName, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger?.LogDebug("Cancelled announcements for {ServiceName}", serviceName);
        }
    }

    private async Task RunAnnouncementScheduleAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            // RFC 6762: Initial announcements
            // Send immediately
            RaiseAnnouncementDue(serviceName);

            // Wait 1 second
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            RaiseAnnouncementDue(serviceName);

            // Wait 2 seconds
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            RaiseAnnouncementDue(serviceName);

            // Continue with periodic announcements to keep service visible
            // Send unsolicited announcements every 60 seconds to ensure clients can discover the service
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                RaiseAnnouncementDue(serviceName);
                _logger?.LogDebug("Sent periodic announcement for {ServiceName}", serviceName);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
            _logger?.LogDebug("Announcement schedule cancelled for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error in announcement schedule for {ServiceName}", serviceName);
        }
    }

    private void RaiseAnnouncementDue(string serviceName)
    {
        try
        {
            AnnouncementDue?.Invoke(this, serviceName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error raising announcement event for {ServiceName}", serviceName);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var cts in _scheduledAnnouncements.Values)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        _scheduledAnnouncements.Clear();
        GC.SuppressFinalize(this);
    }
}
