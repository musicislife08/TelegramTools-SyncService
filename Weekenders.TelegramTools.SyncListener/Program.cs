using FluentMigrator.Runner;
using Weekenders.TelegramTools.Data;
using Weekenders.TelegramTools.SyncListener;
using Weekenders.TelegramTools.Telegram;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Configure logging
        services.AddLogging(options =>
        {
            options
                .ClearProviders()
                .AddDebug()
                .AddConsole();
        });

        services.AddTelegramServices(config);
        services.AddDatabaseServices();

        services.AddSingleton<IDataService, DataService>();
        services.AddHostedService<TelegramWorker>();
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });
    })
    .Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    migrationRunner.MigrateUp();
}

await host.RunAsync();