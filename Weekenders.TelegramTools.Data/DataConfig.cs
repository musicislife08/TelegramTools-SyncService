using System.Text.Json.Serialization;
using Npgsql;

// ReSharper disable MemberCanBePrivate.Global

namespace Weekenders.TelegramTools.Data;

public class DataConfig
{
    public string? Hostname;
    public int Port;
    public string? Username;
    public string? Password;
    public string? DatabaseName;
}