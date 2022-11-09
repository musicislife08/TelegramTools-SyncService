using Dapper;
using Dapper.FluentMap;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Weekenders.TelegramTools.Data.Migrations;
using Weekenders.TelegramTools.Data.Models;

namespace Weekenders.TelegramTools.Data;

public static class Extensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
    {
        FluentMapper.Initialize(config =>
        {
            config.AddMap(new MessageMapper());
        });
        var data = new DataConfig()
        {
            Hostname = Environment.GetEnvironmentVariable("DATABASE_HOST"),
            Port = int.Parse(Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432"),
            DatabaseName = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "wttss",
            Username = Environment.GetEnvironmentVariable("DATABASE_USERNAME"),
            Password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD"),
        };
        services.AddFluentMigratorCore()
            .ConfigureRunner(options =>
            {
                options.AddPostgres()
                    .WithGlobalConnectionString(GetConnectionString(data))
                    .ScanIn(typeof(Initial).Assembly).For.Migrations();
            }).AddLogging(lb => lb.AddFluentMigratorConsole());
        services.AddOptions();
        services.Configure<DataConfig>(x =>
        {
            x.Hostname = data.Hostname;
            x.Port = data.Port;
            x.DatabaseName = data.DatabaseName;
            x.Username = data.Username;
            x.Password = data.Password;
        });
        return services;
    }

    public static string GetConnectionString(DataConfig config)
    {
        return new NpgsqlConnectionStringBuilder()
        {
            Port = config.Port,
            Username = config.Username,
            Password = config.Password,
            Host = config.Hostname,
            Database = config.DatabaseName
        }.ConnectionString;
    }
}