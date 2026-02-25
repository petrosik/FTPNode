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
        private static readonly ConcurrentDictionary<string, FTPStorage> _ftpClients = new();
        private readonly ISharedStorage _sharedStorage;

        public FTPHub(ISharedStorage sharedService)
        {
            _sharedStorage = sharedService;
        }

        public override async Task OnConnectedAsync()
        {
            var settings = new List<Pair>();
            if (_sharedStorage.TryGetArg("host", out string val))
                settings.Add(new() { Name = "host", Value = val });
            if (_sharedStorage.TryGetArg("port", out val))
                settings.Add(new() { Name = "port", Value = val });
            if (_sharedStorage.TryGetArg("passivemode", out val))
                settings.Add(new() { Name = "passivemode", Value = val });
            if (_sharedStorage.TryGetArg("uploadlimit", out val))
                settings.Add(new() { Name = "uploadlimit", Value = val });
            if (_sharedStorage.TryGetArg("maxfileuploadsize", out val))
                settings.Add(new() { Name = "maxfileuploadsize", Value = val });
            if (_sharedStorage.TryGetArg("simultaneousupdown", out val))
                settings.Add(new() { Name = "simultaneousupdown", Value = val });

            await Clients.Caller.SendAsync("ReceiveInitData", settings);
            await base.OnConnectedAsync();
        }

        public async Task<string> UploadChunk(UploadMetadataDto metadata, byte[] chunk, long offset)
        {
            var clientPair = _ftpClients[Context.ConnectionId];

            // Dictionary: name -> IFtpStream

            if (!clientPair.UploadQue.TryGetValue(metadata.Name, out var strim))
            {
                if (offset != 0)
                    return "Error: Offset mismatch. Upload not initialized properly.";

                // Open FTP stream for writing
                try
                {
                    var ftpClient = SetupCLient(clientPair.LoginJson);
                    ftpClient.First.SetWorkingDirectory(metadata.UploadPath);
                    var ftpStreams = ftpClient.First.OpenWrite(metadata.UploadPath.EndsWith('/') ? metadata.UploadPath + metadata.Name : metadata.UploadPath + "/" + metadata.Name);
                    clientPair.UploadQue[metadata.Name] = new(ftpClient.First, ftpStreams);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            var stream = clientPair.UploadQue[metadata.Name];

            if (offset != stream.Second.Position)
            {
                stream.Second.Position = offset;
            }

            await stream.Second.WriteAsync(chunk, 0, chunk.Length);
            await stream.Second.FlushAsync();

            return (stream.Second.Position).ToString();
        }

        public async Task UploadFinish(string name)
        {
            var clientPair = _ftpClients[Context.ConnectionId];

            if (!clientPair.UploadQue.TryGetValue(name, out var stream))
                return;
            await stream.Second.FlushAsync();
            stream.Second.Close();

            stream.First.GetReply();
            clientPair.UploadQue.Remove(name);
        }
        public async Task UploadCancel(string name, bool autoDelete)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
            {
                Console.WriteLine("Upload Cancel | Error ftpClients don't contain connection ID");
                return;
            }

            if (!clientPair.UploadQue.TryGetValue(name, out var stream))
                return;

            try
            {
                stream.Second.Close();
                stream.Second.Dispose();
                if (autoDelete)
                {
                    stream.First.DeleteFile(name);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                clientPair.UploadQue.Remove(name);
            }
        }
        public async Task<string> DownloadStart(string filename, string path)
        {
            if (!_ftpClients.TryGetValue(Context.ConnectionId, out var clientPair))
                return "false | Error: FTP client not found.";

            if (clientPair.DownloadQue.ContainsKey(filename))
                return "false | Error: Download already started.";

            try
            {
                var ftpClient = SetupCLient(clientPair.LoginJson);
                if (ftpClient.First == null)
                {
                    return ftpClient.Second;
                }
                ftpClient.First.SetWorkingDirectory(path);
                clientPair.DownloadQue[filename] = new(ftpClient.First, ftpClient.First.OpenRead(filename));
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

            if (!clientPair.DownloadQue.TryGetValue(name, out var stream))
                return new(null, "false | Download not started");

            try
            {
                if (offset != stream.Second.Position)
                    return new(null, $"false | Offset mismatch. Expected {stream.Second.Position}");

                // Determine chunk size
                int chunkSize = 128 * 1024; // default to 128KB
                if (_sharedStorage.TryGetArg("downloadlimit", out string downloadlim) &&
                    int.TryParse(downloadlim, out var downlim))
                {
                    chunkSize = downlim;
                }

                byte[] buffer = new byte[chunkSize];
                int totalBytesRead = 0;

                // Loop until buffer is full or EOF
                while (totalBytesRead < buffer.Length)
                {
                    int bytesRead = await stream.Second.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        break; // EOF
                    }
                    totalBytesRead += bytesRead;
                }

                if (totalBytesRead == 0)
                {
                    // End of file reached
                    stream.Second.Dispose();
                    clientPair.DownloadQue.Remove(name);
                    return new(Array.Empty<byte>(), "true | EOF");
                }

                byte[] finalBuffer = totalBytesRead < buffer.Length
                    ? buffer[..totalBytesRead] // slice only the valid portion
                    : buffer;

                return new(finalBuffer, $"true | {stream.Second.Position}");
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

            if (!clientPair.DownloadQue.TryGetValue(name, out var stream))
                return "false | Error: Stream not found.";

            try
            {
                stream.Second.Close();
                stream.Second.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                clientPair.DownloadQue.Remove(name);
            }

            return "true";
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_ftpClients.TryRemove(Context.ConnectionId, out var ftpClient))
            {
                if (ftpClient.UploadQue.Count != 0)
                {
                    foreach (var item in ftpClient.UploadQue)
                    {
                        try
                        {
                            item.Value.Second.Close();
                            item.Value.Second.Dispose();
                            if (!_sharedStorage.TryGetArg<bool>("autodelete", out var v) || v)
                            {
                                item.Value.First.DeleteFile(item.Key);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error cleaning up {item.Key}: {e.Message}");
                        }
                    }
                }
                if (ftpClient.DownloadQue.Count != 0)
                {
                    foreach (var item in ftpClient.DownloadQue)
                    {
                        item.Value.Second.Close();
                        item.Value.Second.Dispose();
                    }
                }
                ftpClient.MainClient.Disconnect();
                ftpClient.MainClient.Dispose();
            }
            await base.OnDisconnectedAsync(exception);
        }
        public async Task<string> InitFtp(LoginJson info)
        {
            try
            {
                var ftpClient = SetupCLient(info);
                if (ftpClient.First == null)
                {
                    return ftpClient.Second;
                }
                _ftpClients[Context.ConnectionId] = new() { MainClient = ftpClient.First, LoginJson=info };
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
            var wd = _ftpClients[Context.ConnectionId].MainClient.GetWorkingDirectory();
            FtpListItem[] items = _ftpClients[Context.ConnectionId].MainClient.GetListing(
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
            if (_ftpClients[Context.ConnectionId].MainClient.DirectoryExists(targetPath))
            {
                _ftpClients[Context.ConnectionId].MainClient.SetWorkingDirectory(targetPath);
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
            if (_ftpClients[Context.ConnectionId].MainClient.FileExists(target))
            {
                _ftpClients[Context.ConnectionId].MainClient.DeleteFile(target);
                return "true";
            }
            else if (_ftpClients[Context.ConnectionId].MainClient.DirectoryExists(target))
            {
                _ftpClients[Context.ConnectionId].MainClient.DeleteDirectory(target);
                return "true";
            }
            else
            {
                return "false | Error file or directory could not be found";
            }
        }
        private static Pair<FtpClient,string> SetupCLient(LoginJson info)
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
                return new(ftpClient,null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FTP client: {ex.Message}");
                return new(null, $"{ex.Message} | {ex.StackTrace}");
            }
        } 
    }
}
