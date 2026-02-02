using FluentFTP;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using WebFTPViewer.Services;

namespace WebFTPViewer.Hubs
{
    public class FTPHub : Hub
    {
        // Store FTP clients per connection
        private static readonly ConcurrentDictionary<string, Pair<FtpClient, Dictionary<string, Stream>>> _ftpClients = new();
        private readonly ISharedStorage _sharedStorage;

        public FTPHub(ISharedStorage sharedService)
        {
            _sharedStorage = sharedService;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public async Task<string> UploadChunk(UploadMetadataDto metadata, byte[] chunk, long offset)
        {
            var clientPair = _ftpClients[Context.ConnectionId];

            // Dictionary: name -> IFtpStream
            var fileStreams = clientPair.Second;

            if (!fileStreams.ContainsKey(metadata.Name))
            {
                if (offset != 0)
                    return "Error: Offset mismatch. Upload not initialized properly.";

                // Open FTP stream for writing
                try
                {
                    var ftpStream = clientPair.First.OpenWrite(metadata.UploadPath.EndsWith('/') ? metadata.UploadPath + metadata.Name : metadata.UploadPath + "/" + metadata.Name);
                    fileStreams[metadata.Name] = ftpStream;
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            var stream = fileStreams[metadata.Name];

            // Seek if the FTP stream supports it (some libraries do, some don't)
            if (offset != stream.Position)
            {
                stream.Position = offset;
            }

            await stream.WriteAsync(chunk, 0, chunk.Length);
            await stream.FlushAsync();

            return (stream.Position).ToString(); // next expected offset
        }

        public async Task UploadFinish(string name)
        {
            var clientPair = _ftpClients[Context.ConnectionId];
            var fileStreams = clientPair.Second;

            if (!fileStreams.TryGetValue(name, out var stream))
                return;
            await stream.FlushAsync();
            stream.Close();

            clientPair.First.GetReply();
            fileStreams.Remove(name);
        }
        public async Task UploadCancel(string name, bool autoDelete)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
            {
                Console.WriteLine("Upload Cancel | Error ftpClients don't contain connection ID");
                return;
            }
            var fileStreams = clientPair.Second;

            if (!fileStreams.TryGetValue(name, out var stream))
                return;

            try
            {
                stream.Close();
                stream.Dispose();
                if (autoDelete)
                {
                    clientPair.First.DeleteFile(name);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                fileStreams.Remove(name);
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_ftpClients.TryRemove(Context.ConnectionId, out var ftpClient))
            {
                ftpClient.First.Disconnect();
                ftpClient.First.Dispose();
                if (ftpClient.Second.Count != 0)
                {
                    foreach (var item in ftpClient.Second)
                    {
                        item.Value.Close();
                        item.Value.Dispose();
                        //need to add settings to keep unfinished files later
                        //if (_sharedStorage.TryGetArg("autodelete"))
                        ftpClient.First.DeleteFile(item.Key);
                    }
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
        public async Task<string> InitFtp(LoginJson info)
        {
            try
            {
                // Create and connect FTP client when SignalR client connects
                var ftpClient = new FtpClient(info.Host, new NetworkCredential(info.Username, info.Password), info.Port)
                {
                    Config = new FtpConfig
                    {
                        EncryptionMode = FtpEncryptionMode.Auto,
                        ValidateAnyCertificate = true,//change to frontend ask
                    }
                };

                ftpClient.Connect();

                _ftpClients[Context.ConnectionId] = new(ftpClient, new());
                return true.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FTP client: {ex.Message}");
                return $"{ex.Message} | {ex.StackTrace}";
            }
        }
        public async Task<string> GetCurrentDirectory()
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return null;
            var wd = _ftpClients[Context.ConnectionId].First.GetWorkingDirectory();
            FtpListItem[] items = _ftpClients[Context.ConnectionId].First.GetListing(
                    wd,
                    FtpListOption.Modify |
                    FtpListOption.Size |
                    FtpListOption.NoPath | FtpListOption.IncludeSelfAndParent);
            return JsonSerializer.Serialize(new KeyValuePair<string, List<FtpItemDto>>(wd, items.Select(i => new FtpItemDto
            {
                Name = i.Name,
                Type = Enum.TryParse<FileType>(i.Type.ToString(), out var en) ? en : FileType.Unknown,
                Size = i.Size,
                Modified = i.Modified,
                Permissions = Utils.GetUnixPermissions(i.Chmod)
            }).ToList()));
        }
        public async Task<bool> Goto(string targetPath)
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return false;
            if (_ftpClients[Context.ConnectionId].First.DirectoryExists(targetPath))
            {
                _ftpClients[Context.ConnectionId].First.SetWorkingDirectory(targetPath);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
