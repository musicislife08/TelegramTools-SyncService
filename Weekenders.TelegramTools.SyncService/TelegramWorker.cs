using FFMpegCore;
using Microsoft.Extensions.Options;
using MimeTypes;
using TL;
using WTelegram;

namespace Weekenders.TelegramTools.SyncService;

public class TelegramWorker : BackgroundService
{
    private readonly ILogger<TelegramWorker> _logger;
    private readonly TelegramConfiguration _configuration;
    private Client _client = null!;
    private Channel? _source;
    private Channel? _destination;
    private long _destinationAccessHash;

    public TelegramWorker(ILogger<TelegramWorker> logger, IOptions<TelegramConfiguration> options)
    {
        _logger = logger;
        ArgumentNullException.ThrowIfNull(options.Value);
        _configuration = options.Value;
        ArgumentNullException.ThrowIfNull(_configuration.PhoneNumber);
        ArgumentNullException.ThrowIfNull(_configuration.SourceGroupName);
        ArgumentNullException.ThrowIfNull(_configuration.DestinationGroupName);
        _logger.LogDebug("Phone: {Phone}", _configuration.PhoneNumber);
        _logger.LogDebug("Source: {Source}", _configuration.SourceGroupName);
        _logger.LogDebug("Destination: {Destination}", _configuration.DestinationGroupName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitialSetup();
        _logger.LogInformation("{Name} running at: {Time}", nameof(TelegramWorker), DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task Source_OnUpdate(IObject arg)
    {
        if (arg is not UpdatesBase updates) return;
        if (_source is null || _destination is null)
            return;
        foreach (var update in updates.UpdateList)
        {
            if (update is not UpdateNewMessage { message: Message message })
                continue;
            if (message.peer_id.ID != _source.ID)
                continue;
            switch (message.media)
            {
                case MessageMediaDocument { document: Document document }:
                {
                    var slash = document.mime_type.IndexOf('/');
                    var filename = slash > 0 ? $"{document.id}.{document.mime_type[(slash + 1)..]}" : $"{document.id}.bin";
                    _logger.LogInformation("Downloading: {FileName} to /tmp/{TempFilename}", document.Filename, filename);
                    var path = $"/tmp/{filename}";
                    await using var fileStream = File.Create(path);
                    await _client.DownloadFileAsync(document, fileStream);
                    await fileStream.DisposeAsync();
                    await SendToDestination(path, document.Filename, document.attributes, document.thumbs is not null);
                    continue;
                }
                case MessageMediaPhoto { photo: Photo photo }:
                {
                    var filename = $"{photo.ID}.jpg";
                    var path = Path.Combine("/tmp", filename);
                    await using var file = File.Create(path);
                    var type = await _client.DownloadFileAsync(photo, file);
                    file.Close();
                    var newPath = $"/tmp/{photo.ID}.{type}";
                    if (type is not Storage_FileType.unknown and not Storage_FileType.partial)
                        File.Move(path, newPath, true);
                    await SendPhotoToDestination(newPath);
                    continue;
                }
                default:
                    continue;
            }
        }
    }

    private async Task SendPhotoToDestination(string path)
    {
        try
        {
            var file = await _client.UploadFileAsync(path);
            await _client.SendMediaAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash), null,
                file);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private async Task SendToDestination(string path, string filename, DocumentAttribute[] attributes, bool hasThumbs)
    {
        var thumb = string.Empty;
        try
        {
            await using var stream = File.Open(path, FileMode.Open);
            var file = await _client.UploadFileAsync(stream, filename);
            await stream.DisposeAsync();
            var uploadDoc = new InputMediaUploadedDocument()
            {
                file = file,
                attributes = attributes,
                mime_type = GetMimeType(filename),
            };

            if (hasThumbs)
            {
                thumb = GetThumbnail(path);
                var thumbUpload = await _client.UploadFileAsync(thumb);
                uploadDoc.flags = InputMediaUploadedDocument.Flags.has_thumb;
                uploadDoc.thumb = thumbUpload;
            }

            var msg = await _client.SendMessageAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash),
                null, uploadDoc);
            //await _client.SendMediaAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash), null, file);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);
        }
        finally
        {
            File.Delete(path);
            if (hasThumbs)
                File.Delete(thumb);
        }
    }

    private static string GetMimeType(string path)
    {
        var info = new FileInfo(path);
        return MimeTypeMap.GetMimeType(info.Extension);
    }

    private async Task InitialSetup()
    {
        Helpers.Log = (lvl, str) => _logger.Log((LogLevel)lvl, "{String}", str);
        _client = new Client(Config);
        _client.CollectAccessHash = true;
        _client.PingInterval = 60;
        _client.MaxAutoReconnects = 30;
        await _client.LoginUserIfNeeded();
        var chats = await _client.Messages_GetAllChats();
        _source = (Channel)chats.chats.First(x => x.Value.Title == _configuration.SourceGroupName && x.Value.IsActive)
            .Value;
        _destination = (Channel)chats.chats
            .First(x => x.Value.Title == _configuration.DestinationGroupName && x.Value.IsActive).Value;
        _destinationAccessHash = _client.GetAccessHashFor<Channel>(_destination.ID);
        _client.OnUpdate += Source_OnUpdate;
    }

    private static string GetThumbnail(string path)
    {
        var info = new FileInfo(path);
        var output = Path.ChangeExtension(info.FullName, ".jpg");
        //var success = FFMpeg.Snapshot(info.FullName, output, new(){Height = 300, Width = 300}, TimeSpan.FromMilliseconds(1));
        var f = FFMpegArguments
            .FromFileInput(info)
            .OutputToFile(output, true, options => options
                .WithDuration(TimeSpan.FromMilliseconds(1))
                .WithFrameOutputCount(1)
                .WithFastStart())
            .ProcessSynchronously();
        return output;
    }

    private string Config(string what)
    {
        return (what switch
        {
            "api_id" => "2252206",
            "api_hash" => "4dcf9af0c05042ca938a0a44bfb522dd",
            "phone_number" => _configuration.PhoneNumber,
            "verification_code" => throw new ApplicationException(
                "Must have existing telegram session file mounted at ${_configuration.SessionPath}/tg.session"),
            "first_name" => throw new ApplicationException("Please sign up for an account before you run this program"),
            "last_name" => throw new ApplicationException("Please sign up for an account before you run this program"),
            "password" => throw new ApplicationException(
                $"Must have existing telegram session file mounted at ${_configuration.SessionPath}/tg.session"),
            "session_pathname" => Path.Combine(_configuration.SessionPath, "tg.session"),
            _ => null!
        })!;
    }
}