// Configure Serilog for file logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/mdns-servicehost-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Console.Clear();
    Console.WriteLine("╔═══════════════════════════════════════════════╗");
    Console.WriteLine("║    mDNS Service Host - .NET 11                ║");
    Console.WriteLine("╚═══════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"Log file: logs/mdns-servicehost-{DateTime.Now:yyyyMMdd}.log");
    Console.WriteLine();

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog as logging provider
    builder.Services.AddSerilog();

    // Add configuration
    builder.Configuration.AddJsonFile("appsettings.json", optional: false);

    // Add services
    builder.Services.Configure<ServiceConfiguration>(
        builder.Configuration.GetSection("mDNS"));

    builder.Services.AddSingleton<IServiceAdvertiser>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<ServiceAdvertiser>>();
        return new ServiceAdvertiser(logger);
    });

    // Register the service listener factory
    builder.Services.AddSingleton<IServiceListenerFactory, ServiceListenerFactory>();

    // Register the hosted service manager
    builder.Services.AddHostedService<ServiceManager>();

    var host = builder.Build();

    Console.WriteLine("Starting service advertiser...");
    Console.WriteLine("Press Ctrl+C to shutdown");
    Console.WriteLine();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service host terminated unexpectedly");
    Console.WriteLine($"\nFatal error: {ex.Message}");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
