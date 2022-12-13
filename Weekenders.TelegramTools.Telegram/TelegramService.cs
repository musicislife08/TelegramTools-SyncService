using FFMpegCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TL;
using WTelegram;
using Message = TL.Message;
using MimeTypes;
using Weekenders.TelegramTools.Data;
using Weekenders.TelegramTools.Data.Models;
using Weekenders.TelegramTools.Telegram.Exceptions;

namespace Weekenders.TelegramTools.Telegram;

public class TelegramService : ITelegramService
{
    private Client? _client;
    private readonly TelegramConfiguration _configuration;
    private readonly IDataService _dataService;
    private readonly ILogger<TelegramService> _logger;
    private Channel? _source;
    private Channel? _destination;
    private long _destinationAccessHash;
    private long _sourceAccessHash;
    private bool _isReady;

    public TelegramService(ILogger<TelegramService> logger, IOptions<TelegramConfiguration> options,
        IDataService dataService)
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
        _dataService = dataService;
        _logger.LogInformation("{Name} Constructed", nameof(TelegramService));
    }

    public async Task<TelegramProcessResult> ProcessMessage(long id)
    {
        if (!_isReady || _client is null)
            throw new Exception("Cannot processes messages without initialization");
        if (_client.Disconnected)
            await _client.LoginUserIfNeeded();
        if (_source is null || _destination is null)
        {
            _logger.LogDebug("Source or Destination is null.  Skipping");
            throw new NullReferenceException("Either Source or Destination group was null");
        }

        var messages = await _client.GetMessages(new InputPeerChannel(_source.ID, _sourceAccessHash),
            new InputMessageID() { id = (int)id });
        switch (messages.Messages[0])
        {
            case MessageEmpty:
                return new() { ProcessStatus = ProcessStatus.DeletedFromSource};
            case Message tgMessage:
                switch (tgMessage.media)
                {
                    case MessageMediaDocument { document: Document document }:
                    {
                        _logger.LogInformation("Adding Document {Name} To Queue", document.Filename);
                        var slash = document.mime_type.IndexOf('/');
                        var filename = slash > 0
                            ? $"{document.id}.{document.mime_type[(slash + 1)..]}"
                            : $"{document.id}.bin";
                        _logger.LogInformation("Downloading: {FileName} to /tmp/{TempFilename}", document.Filename,
                            filename);
                        var path = $"/tmp/{filename}";
                        await using var fileStream = File.Create(path);
                        await _client.DownloadFileAsync(document, fileStream);
                        await fileStream.DisposeAsync();
                        _logger.LogInformation("Uploading {Name} to {Destination}", document.Filename,
                            _configuration.DestinationGroupName);
                        var result = await SendToDestination(path, document.Filename, document.attributes,
                            document.thumbs is not null);
                        return new() { ProcessStatus = ProcessStatus.Processed, TelegramId = result?.ID};
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
                        _logger.LogInformation("Uploading Photo {Id} to {Destination}", photo.ID,
                            _configuration.DestinationGroupName);
                        var result = await SendPhotoToDestination(newPath);
                        return new() { ProcessStatus = ProcessStatus.DeletedFromSource, TelegramId = result?.ID};
                    }
                    default:
                        return new() { ProcessStatus = ProcessStatus.OtherError};
                }
            default:
                return new() { ProcessStatus = ProcessStatus.OtherError};
        }
    }

    private async Task<Message?> SendPhotoToDestination(string path)
    {
        if (!_isReady || _client is null)
            throw new ClientInitializationException();
        if (_client.Disconnected)
            await _client.LoginUserIfNeeded();
        try
        {
            var file = await _client.UploadFileAsync(path);
            return await _client.SendMediaAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash), null,
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

    private async Task<Message?> SendToDestination(string path, string filename, DocumentAttribute[] attributes, bool hasThumbs)
    {
        if (!_isReady || _client is null)
            throw new ClientInitializationException();
        if (_client.Disconnected)
            await _client.LoginUserIfNeeded();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));
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

            if (!hasThumbs)
                return await _client.SendMessageAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash),
                    null, uploadDoc);
            _logger.LogDebug("Document {Document} has thumbnail", filename);
            thumb = GetThumbnail(path);
            if (thumb is null)
                return await _client.SendMessageAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash),
                    null, uploadDoc);
            _logger.LogDebug("Uploading thumbnail for {Document}", filename);
            var thumbUpload = await _client.UploadFileAsync(thumb);
            uploadDoc.flags = InputMediaUploadedDocument.Flags.has_thumb;
            _logger.LogDebug("Attaching thumbnail to document");
            uploadDoc.thumb = thumbUpload;

            return await _client.SendMessageAsync(new InputPeerChannel(_destination!.ID, _destinationAccessHash),
                null, uploadDoc);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Message}", e.Message);
            throw;
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

    private async Task OnUpdate(IObject arg)
    {
        _logger.LogDebug("{Name} Called", nameof(OnUpdate));
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
                        SourceId = message.ID,
                        Name = document.Filename,
                        Created = DateTimeOffset.UtcNow
                    };
                    await _dataService.AddMessageAsync(msg);
                    continue;
                }
                case MessageMediaPhoto { photo: Photo photo }:
                {
                    _logger.LogInformation("Adding Photo {Name} To Queue", photo.ID);
                    var msg = new Data.Models.Message()
                    {
                        SourceId = message.ID,
                        Name = "photo",
                        Created = DateTimeOffset.UtcNow
                    };
                    await _dataService.AddMessageAsync(msg);
                    continue;
                }
                default:
                    continue;
            }
        }
    }

    public async Task InitialSetup(bool enableUpdate)
    {
        Helpers.Log = (lvl, str) => _logger.Log((LogLevel)lvl, "{String}", str);
        _client = new(Config);
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
        if (enableUpdate)
            _client.OnUpdate += OnUpdate;
        _isReady = true;
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