using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Weekenders.TelegramTools.Telegram;

public static class Utilities
{
    public static IServiceCollection AddTelegramServices(this IServiceCollection services, IConfigurationRoot config)
    {
        services.AddOptions()
            .Configure<TelegramConfiguration>(x => config.GetSection("TELEGRAM_CONFIG").Bind(x));
        services.AddSingleton<ITelegramService, TelegramService>();

        return services;
    }
}