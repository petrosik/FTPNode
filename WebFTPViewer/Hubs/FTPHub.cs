using FluentFTP;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using WebFTPViewer.Services;

namespace WebFTPViewer.Hubs
{
    public class FTPHub : Hub
    {
        // Store FTP clients per connection
        // first is upload second is download streams
        private static readonly ConcurrentDictionary<string, Pair<FtpClient, Pair<Dictionary<string, Stream>, Dictionary<string, Stream>>>> _ftpClients = new();
        private readonly ISharedStorage _sharedStorage;

        public FTPHub(ISharedStorage sharedService)
        {
            _sharedStorage = sharedService;
        }

        public override async Task OnConnectedAsync()
        {
            var settings = new List<Pair>();
            if (_sharedStorage.TryGetArg("defaultconnection", out string defconn))
                settings.Add(new() {Name= "defaultconnection", Value = defconn });
            if (_sharedStorage.TryGetArg("uploadlimit", out string uploadlimit))
                settings.Add(new() { Name = "uploadlimit", Value = uploadlimit });

            await Clients.Caller.SendAsync("ReceiveInitData",settings);
            await base.OnConnectedAsync();
        }

        public async Task<string> UploadChunk(UploadMetadataDto metadata, byte[] chunk, long offset)
        {
            var clientPair = _ftpClients[Context.ConnectionId];

            // Dictionary: name -> IFtpStream
            var fileStreams = clientPair.Second;

            if (!fileStreams.First.ContainsKey(metadata.Name))
            {
                if (offset != 0)
                    return "Error: Offset mismatch. Upload not initialized properly.";

                // Open FTP stream for writing
                try
                {
                    var ftpStream = clientPair.First.OpenWrite(metadata.UploadPath.EndsWith('/') ? metadata.UploadPath + metadata.Name : metadata.UploadPath + "/" + metadata.Name);
                    fileStreams.First[metadata.Name] = ftpStream;
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            var stream = fileStreams.First[metadata.Name];

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

            if (!fileStreams.First.TryGetValue(name, out var stream))
                return;
            await stream.FlushAsync();
            stream.Close();

            clientPair.First.GetReply();
            fileStreams.First.Remove(name);
        }
        public async Task UploadCancel(string name, bool autoDelete)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
            {
                Console.WriteLine("Upload Cancel | Error ftpClients don't contain connection ID");
                return;
            }
            var fileStreams = clientPair.Second;

            if (!fileStreams.First.TryGetValue(name, out var stream))
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
                fileStreams.First.Remove(name);
            }
        }
        public async Task<string> DownloadStart(string filename)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
                return "false | Error: FTP client not found.";

            var fileStreams = clientPair.Second;

            if (fileStreams.Second.ContainsKey(filename))
                return "false | Error: Download already started.";

            try
            {
                var ftpStream = clientPair.First.OpenRead(filename);
                fileStreams.Second[filename] = ftpStream;
                return "true";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        public async Task<Pair<byte[]?, string>> DownloadChunk(string name, long offset)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
                return new(null, "false | FTP client not found");

            var fileStreams = clientPair.Second;

            if (!fileStreams.Second.TryGetValue(name, out var stream))
                return new(null, "false | Download not started");

            try
            {
                if (offset != stream.Position)
                    return new(null, $"false | Offset mismatch. Expected {stream.Position}");

                int chunkSize = 64*1024;
                if (_sharedStorage.TryGetArg("uploadlimit", out string downloadlim) && int.TryParse(downloadlim, out var downlim))
                    chunkSize = downlim;
                byte[] buffer = new byte[chunkSize];

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    try
                    {
                        stream.Close();
                        stream.Dispose();

                        fileStreams.Second.Remove(name);
                        return new(Array.Empty<byte>(), "true | EOF");
                    }
                    catch (Exception e)
                    {
                        return new(null,$"false | {e.Message}");
                    }
                }

                // Trim buffer if last chunk smaller
                if (bytesRead < buffer.Length)
                {
                    byte[] trimmed = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, trimmed, 0, bytesRead);
                    return new(trimmed, $"true | {bytesRead}");
                }

                return new(buffer, $"true | {bytesRead}");
            }
            catch (Exception e)
            {
                return new(null, $"false | {e.Message}");
            }
        }
        public async Task<string> DownloadCancel(string name)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
                return "false | Error: FTP client not found.";

            var fileStreams = clientPair.Second;

            if (!fileStreams.Second.TryGetValue(name, out var stream))
                return "false | Error: Stream not found.";

            try
            {
                stream.Close();
                stream.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                fileStreams.Second.Remove(name);
            }

            return "true";
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_ftpClients.TryRemove(Context.ConnectionId, out var ftpClient))
            {
                if (ftpClient.Second.First.Count != 0)
                {
                    foreach (var item in ftpClient.Second.First)
                    {
                        try
                        {
                            item.Value.Close();
                            item.Value.Dispose();
                            if (!_sharedStorage.TryGetArg<bool>("autodelete", out var v) || v)
                            {
                                ftpClient.First.DeleteFile(item.Key);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error cleaning up {item.Key}: {e.Message}");
                        }
                    }
                }
                if (ftpClient.Second.Second.Count != 0)
                {
                    foreach (var item in ftpClient.Second.Second)
                    {
                        item.Value.Close();
                        item.Value.Dispose();
                    }
                }
                ftpClient.First.Disconnect();
                ftpClient.First.Dispose();
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

                _ftpClients[Context.ConnectionId] = new(ftpClient, new(new(), new()));
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
                Permissions = i.Chmod
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
        public async Task<string> Delete(string target)
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return "false | Error Connection Id Missing 404";
            if (_ftpClients[Context.ConnectionId].First.FileExists(target))
            {
                _ftpClients[Context.ConnectionId].First.DeleteFile(target);
                return "true";
            }
            else if (_ftpClients[Context.ConnectionId].First.DirectoryExists(target))
            {
                _ftpClients[Context.ConnectionId].First.DeleteDirectory(target);
                return "true";
            }
            else
            {
                return "false | Error file or directory could not be found";
            }
        }
    }
}
