using FluentFTP;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace WebFTPViewer.Hubs
{
    public class FTPHub : Hub
    {
        // Store FTP clients per connection
        private static readonly ConcurrentDictionary<string, FtpClient> _ftpClients = new();

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public async Task UploadFile(string localPath, string remotePath)
        {
            if (_ftpClients.TryGetValue(Context.ConnectionId, out var ftpClient))
            {
                ftpClient.UploadFile(localPath, remotePath);
                await Clients.Caller.SendAsync("UploadResult", "Success");
            }
            else
            {
                await Clients.Caller.SendAsync("UploadResult", "FTP Client not found");
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_ftpClients.TryRemove(Context.ConnectionId, out var ftpClient))
            {
                ftpClient.Disconnect();
                ftpClient.Dispose();
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
                    //Config = new FtpConfig
                    //{
                    //    EncryptionMode = FtpEncryptionMode.Explicit,
                    //    ValidateAnyCertificate = true,
                    //}
                };

                ftpClient.Connect();

                _ftpClients[Context.ConnectionId] = ftpClient;
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
            var wd = _ftpClients[Context.ConnectionId].GetWorkingDirectory();
            FtpListItem[] items = _ftpClients[Context.ConnectionId].GetListing(
                    wd,
                    FtpListOption.Modify |
                    FtpListOption.Size |
                    FtpListOption.NoPath | FtpListOption.IncludeSelfAndParent);
            return JsonSerializer.Serialize(new KeyValuePair<string, List<FtpItemDto>>(wd, items.Select(i => new FtpItemDto
            {
                Name = i.Name,
                Type = Enum.TryParse<FileType>(i.Type.ToString(), out var en)? en:FileType.Unknown,
                Size = i.Size,
                Modified = i.Modified,
                Permissions = Utils.GetUnixPermissions(i.Chmod)
            }).ToList()));
        }
        public async Task<bool> Goto(string targetPath)
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return false;
            if (_ftpClients[Context.ConnectionId].DirectoryExists(targetPath))
            {
                _ftpClients[Context.ConnectionId].SetWorkingDirectory(targetPath);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
