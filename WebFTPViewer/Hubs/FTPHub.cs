using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Collections.Concurrent;
using System.Net;

namespace WebFTPViewer.Hubs
{
    public class FTPHub : Hub
    {
        // Store FTP clients per connection
        private static readonly ConcurrentDictionary<string, FtpClient> _ftpClients = new();

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine("connected");
            await base.OnConnectedAsync();
        }

        public async Task UploadFile(string localPath, string remotePath)
        {
            if (_ftpClients.TryGetValue(Context.ConnectionId, out var ftpClient))
            {
                await Task.Run(() => ftpClient.UploadFile(localPath, remotePath));
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
        public async Task<bool> InitFtp([FromBody] LoginJson info)
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

                await Task.Run(() => ftpClient.Connect());

                _ftpClients[Context.ConnectionId] = ftpClient;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FTP client: {ex.Message}");
                return false;
            }
        }
    }
}
