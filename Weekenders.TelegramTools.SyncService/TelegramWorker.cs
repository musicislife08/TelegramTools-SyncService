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
    private readonly IMessageQueueService _queue;
    private Client _client = null!;
    private Channel? _source;
    private Channel? _destination;
    private long _destinationAccessHash;
    private long _sourceAccessHash;

    public TelegramWorker(ILogger<TelegramWorker> logger, IOptions<TelegramConfiguration> options, IMessageQueueService queue)
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
        _queue = queue;
        _logger.LogInformation("{Name} Constructed", nameof(TelegramWorker));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Beginning Initial Setup");
        await InitialSetup();
        _logger.LogInformation("{Name} running at: {Time}", nameof(TelegramWorker), DateTimeOffset.Now);
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessMessages();
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessMessages()
    {
        var msg = await _queue.PullAsync();
        if (msg is null)
            return;
        if (_source is null || _destination is null)
        {
            _logger.LogDebug("Source or Destination is null.  Skipping");
            return;
        }

        var messages = await _client.GetMessages(new InputPeerChannel(_source.ID, _sourceAccessHash),
            new InputMessageID() { id = (int)msg.TelegramId });
        foreach (var message in messages.Messages)
        {
            if (message is Message tgMessage)
            {
                switch (tgMessage.media)
                {
                    case MessageMediaDocument { document: Document document }:
                    {
                        _logger.LogInformation("Adding Document {Name} To Queue", document.Filename);
                        var slash = document.mime_type.IndexOf('/');
                        var filename = slash > 0 ? $"{document.id}.{document.mime_type[(slash + 1)..]}" : $"{document.id}.bin";
                        _logger.LogInformation("Downloading: {FileName} to /tmp/{TempFilename}", document.Filename, filename);
                        var path = $"/tmp/{filename}";
                        await using var fileStream = File.Create(path);
                        await _client.DownloadFileAsync(document, fileStream);
                        await fileStream.DisposeAsync();
                        _logger.LogInformation("Uploading {Name} to {Destination}", document.Filename, _configuration.DestinationGroupName);
                        await SendToDestination(path, document.Filename, document.attributes, document.thumbs is not null);
                        continue;
                    }
                    case MessageMediaPhoto { photo: Photo photo }:
                    {
                        _logger.LogInformation("Adding Photo {Name} To Queue", photo.ID);
                        var filename = $"{photo.ID}.jpg";
                        var path = Path.Combine("/tmp", filename);
                        _logger.LogInformation("Downloading photo to: {Path}", path);
                        await using var file = File.Create(path);
                        var type = await _client.DownloadFileAsync(photo, file);
                        file.Close();
                        var newPath = $"/tmp/{photo.ID}.{type}";
                        if (type is not Storage_FileType.unknown and not Storage_FileType.partial)
                            File.Move(path, newPath, true);
                        _logger.LogInformation("Uploading Photo {Id} to {Destination}", photo.ID, _configuration.DestinationGroupName);
                        await SendPhotoToDestination(newPath);
                        continue;
                    }
                    default:
                        continue;
                }
            }
        }
    }

    private async Task Source_OnUpdate(IObject arg)
    {
        _logger.LogDebug("{Name} Called", nameof(Source_OnUpdate));
        if (arg is not UpdatesBase updates) return;
        if (_source is null || _destination is null)
        {
            _logger.LogDebug("Source or Destination is null.  Skipping");
            return;
        }

        _logger.LogDebug("Processing Updates");
        foreach (var update in updates.UpdateList)
        {
            if (update is not UpdateNewMessage { message: Message message })
            {
                _logger.LogDebug("Message is not an update.  Skipping");
                continue;
            }

            if (message.peer_id.ID != _source.ID)
            {
                _logger.LogDebug("Message is not from source: {Source}", _configuration.SourceGroupName);
                continue;
            }

            switch (message.media)
            {
                case MessageMediaDocument { document: Document document }:
                {
                    _logger.LogInformation("Adding Document {Name} To Queue", document.Filename);
                    var msg = new Data.Models.Message()
                    {
                        TelegramId = message.ID,
                        Name = document.Filename,
                        CreatedDateTimeOffset = DateTimeOffset.UtcNow
                    };
                    await _queue.PutAsync(msg);
                    continue;
                }
                case MessageMediaPhoto { photo: Photo photo }:
                {
                    _logger.LogInformation("Adding Photo {Name} To Queue", photo.ID);
                    var msg = new Data.Models.Message()
                    {
                        TelegramId = message.ID,
                        Name = "photo",
                        CreatedDateTimeOffset = DateTimeOffset.UtcNow
                    };
                    await _queue.PutAsync(msg);
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
            _logger.LogError(e, "{Message}", e.Message);
            throw;
        }
        finally
        {
            _logger.LogDebug("Cleaning up temporary photo: {Path}", path);
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
                _logger.LogDebug("Document {Document} has thumbnail", filename);
                thumb = GetThumbnail(path);
                if (thumb is not null)
                {
                    _logger.LogDebug("Uploading thumbnail for {Document}", filename);
                    var thumbUpload = await _client.UploadFileAsync(thumb);
                    uploadDoc.flags = InputMediaUploadedDocument.Flags.has_thumb;
                    _logger.LogDebug("Attaching thumbnail to document");
                    uploadDoc.thumb = thumbUpload;
                }
            }

            await _client.SendMessageAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash),
                null, uploadDoc);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);
        }
        finally
        {
            _logger.LogDebug("Cleaning up temporary document files");
            File.Delete(path);
            if (hasThumbs && thumb is not null)
                File.Delete(thumb);
        }
    }

    private string GetMimeType(string path)
    {
        var info = new FileInfo(path);
        var type = MimeTypeMap.GetMimeType(info.Extension);
        _logger.LogDebug("File {Name} has MimeType {Type}", info.Name, type);
        return type;
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
        _sourceAccessHash = _client.GetAccessHashFor<Channel>(_source.ID);
        _destinationAccessHash = _client.GetAccessHashFor<Channel>(_destination.ID);
        _client.OnUpdate += Source_OnUpdate;
        }

    private static string? GetThumbnail(string path)
    {
        var info = new FileInfo(path);
        var output = Path.ChangeExtension(info.FullName, ".jpg");
        if (!info.Exists)
            return null;
        try
        {
            FFMpegArguments
                .FromFileInput(info)
                .OutputToFile(output, true, options => options
                    .WithDuration(TimeSpan.FromMilliseconds(1))
                    .WithFrameOutputCount(1)
                    .WithFastStart())
                .ProcessSynchronously();
        }
        catch (Exception)
        {
            return null;
        }

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