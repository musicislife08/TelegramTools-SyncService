using Microsoft.EntityFrameworkCore;
using TL;
using Weekenders.TelegramTools.Data;
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

        services.AddDbContextFactory<MessageDbContext>(options =>
        {
            var cs = Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING");
            options.UseSqlServer(cs);
        });
        services.AddSingleton<IDbService, DbService>();
        services.AddSingleton<IMessageQueueService, MessageQueueService>();
        services.AddHostedService<TelegramWorker>();
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });
    })
    .Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var ctxFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MessageDbContext>>();
    var ctx = await ctxFactory.CreateDbContextAsync();
    await ctx.Database.MigrateAsync();
};
await host.RunAsync();