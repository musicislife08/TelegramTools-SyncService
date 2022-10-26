using Weekenders.TelegramTools.SyncService;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Add configuration
        services.AddOptions()
            .Configure<TelegramConfiguration>(x => config.GetSection("TELEGRAM_CONFIG").Bind(x));

        // Configure logging
        services.AddLogging(options =>
        {
            options
                .ClearProviders()
                .AddDebug()
                .AddConsole();
        });

        services.AddHostedService<TelegramWorker>();
    })
    .Build();

await host.RunAsync();