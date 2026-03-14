using FluentFTP;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using WebFTPViewer.Services;

namespace WebFTPViewer.Hubs
{
    public class FTPHub : Hub
    {
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
            if (_sharedStorage.TryGetArg("maxeditsize", out val))
                settings.Add(new() { Name = "maxeditsize", Value = val });
            if (_sharedStorage.TryGetArg("disablepermchange", out val))
                settings.Add(new() { Name = "disablepermchange", Value = val });
            if (_sharedStorage.TryGetArg("enabledebug", out val))
                settings.Add(new() { Name = "enabledebug", Value = val });

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
                    var ftpClient = await SetupClient(clientPair.LoginJson, true);
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
            clientPair.UploadQue.TryRemove(name, out _);
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
                if (_sharedStorage.TryGetArg("enabledebug", out string endebug) && bool.TryParse(endebug, out var val) && val)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }
            finally
            {
                clientPair.UploadQue.TryRemove(name, out _);
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
                var ftpClient = await SetupClient(clientPair.LoginJson, true);
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
                    clientPair.DownloadQue.TryRemove(name, out _);
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
                clientPair.DownloadQue.TryRemove(name, out _);
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
                            if (!_sharedStorage.TryGetArg<string>("autodelete", out var v) || !bool.TryParse(v, out var val) || val)
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
                var ftpClient = await SetupClient(info);
                if (ftpClient.First == null)
                {
                    return ftpClient.Second;
                }
                _ftpClients[Context.ConnectionId] = new() { MainClient = ftpClient.First, LoginJson = info };
                return true.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FTP client: {ex.Message}");
                return $"{false} | {ex.Message} | {ex.StackTrace}";
            }
        }
        public async Task<string> GetCurrentDirectory()
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return null;
            try
            {
                var wd = _ftpClients[Context.ConnectionId].MainClient.GetWorkingDirectory();
                FtpListItem[] items = _ftpClients[Context.ConnectionId].MainClient.GetListing(
                        wd,
                        FtpListOption.Modify |
                        FtpListOption.Size |
                        FtpListOption.NoPath | FtpListOption.IncludeSelfAndParent);
                return JsonSerializer.Serialize(new Pair<string, List<FtpItemDto>>(wd, items.Select(i => new FtpItemDto
                {
                    Name = i.Name,
                    Type = Enum.TryParse<FileType>(i.Type.ToString(), out var en) ? en : FileType.Unknown,
                    Size = i.Size,
                    Modified = i.Modified,
                    Permissions = i.Chmod
                }).ToList()));
            }
            catch (Exception e)
            {
                return $"{e.Message} | {e.StackTrace}";
            }
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

            if (_ftpClients.Any(x => x.Value.DownloadQue.ContainsKey(target) && x.Value.LoginJson.Host == _ftpClients[Context.ConnectionId].LoginJson.Host && x.Value.LoginJson.Port == _ftpClients[Context.ConnectionId].LoginJson.Port))
            {
                var cancel = _ftpClients.Where(x => x.Value.DownloadQue.ContainsKey(target) && x.Value.LoginJson.Host == _ftpClients[Context.ConnectionId].LoginJson.Host && x.Value.LoginJson.Port == _ftpClients[Context.ConnectionId].LoginJson.Port).Select(x => x.Key);
                foreach (var item in cancel)
                {
                    await Clients.Client(item).SendAsync("ForceDownloadCancel", new Pair(target,"Other user has deleted the file"));
                }
            }
            if (_ftpClients.Any(x => x.Value.UploadQue.ContainsKey(target) && x.Value.LoginJson.Host == _ftpClients[Context.ConnectionId].LoginJson.Host && x.Value.LoginJson.Port == _ftpClients[Context.ConnectionId].LoginJson.Port))
            {
                var cancel = _ftpClients.Where(x => x.Value.UploadQue.ContainsKey(target) && x.Value.LoginJson.Host == _ftpClients[Context.ConnectionId].LoginJson.Host && x.Value.LoginJson.Port == _ftpClients[Context.ConnectionId].LoginJson.Port).Select(x => x.Key);
                foreach (var item in cancel)
                {
                    await Clients.Client(item).SendAsync("ForceUploadCancel", new Pair(target, "Other user has deleted the file"));
                }
            }


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
        public async Task<string> Rename(string from, string dest)
        {
            try
            {
                if (!_ftpClients.ContainsKey(Context.ConnectionId)) return "false | Error Connection Id Missing 404";
                if (string.IsNullOrWhiteSpace(dest)) return "false | Empty New Name";

                _ftpClients[Context.ConnectionId].MainClient.Rename(from, dest);
                return "true";
            }
            catch (Exception e)
            {
                return $"false | {e.Message} | {e.StackTrace}";
            }
        }
        public async Task<string> CreateFolder(string name)
        {
            try
            {
                if (!_ftpClients.ContainsKey(Context.ConnectionId)) return "false | Error Connection Id Missing 404";
                if (string.IsNullOrWhiteSpace(name)) return "false | Empty New Name";

                return _ftpClients[Context.ConnectionId].MainClient.CreateDirectory(name).ToString();
            }
            catch (Exception e)
            {
                return $"false | {e.Message} | {e.StackTrace}";
            }
        }
        public async Task<string> ChangePermisson(string name, int permissons)
        {
            if (!_ftpClients.ContainsKey(Context.ConnectionId)) return "false | Error Connection Id Missing 404";
            try
            {
                _ftpClients[Context.ConnectionId].MainClient.Chmod(name, permissons);
            }
            catch (Exception e)
            {
                return $"false | {e.Message} | {e.StackTrace}";
            }
            return "";
        }
        public async Task<CertificateDto?> GetCert(string host, int port)
        {
            var cert = await GetFtpServerCertificate(host, port);
            if (cert == null) return null;
            var chain = new X509Chain();
            chain.Build(cert);
            var certDto = new CertificateDto
            {
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                Thumbprint = cert.Thumbprint,
                SerialNumber = cert.SerialNumber,
                NotBefore = cert.NotBefore,
                NotAfter = cert.NotAfter,
                SignatureAlgorithm = cert.SignatureAlgorithm.FriendlyName,
                PublicKeyAlgorithm = cert.PublicKey.Oid.FriendlyName,
                PublicKeyLength = cert.PublicKey.Key.KeySize,
                RawDataBase64 = Convert.ToBase64String(cert.RawData),
                Extensions = cert.Extensions.Cast<X509Extension>()
                               .Select(x => new Pair<string, string>(
                                   x.Oid.FriendlyName,
                                   x.Format(true)))
                               .ToList(),
                Chain = chain.ChainElements.Cast<X509ChainElement>()
                               .Select(e => new CertificateChainElementDto
                               {
                                   Subject = e.Certificate.Subject,
                                   Issuer = e.Certificate.Issuer,
                                   Thumbprint = e.Certificate.Thumbprint
                               }).ToList()
            };
            return certDto;
        }
        private async Task<Pair<FtpClient, string>> SetupClient(LoginJson info, bool skipCertVerf = false)
        {
            try
            {
                var autovalid = _sharedStorage.TryGetArg<string>("validateanycertificate", out var v) && bool.TryParse(v, out var val) ? val : false;

                // Create and connect FTP client when SignalR client connects
                var ftpClient = new FtpClient(info.Host, new NetworkCredential(info.Username, info.Password), info.Port)
                {
                    Config = new FtpConfig
                    {
                        EncryptionMode = FtpEncryptionMode.Auto,
                        ValidateAnyCertificate = autovalid,
                    }
                };
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    try
                    {
                        var cert1 = new X509Certificate2(e.Certificate);

                        e.Accept = skipCertVerf || info.AcceptCert && info.OriginalCertThumbprint != null && cert1.Thumbprint == info.OriginalCertThumbprint;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during certificate validation: {ex.Message}");
                        e.Accept = autovalid;
                    }
                };
                ftpClient.Connect();
                return new(ftpClient, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing FTP client: {ex.Message}");
                return new(null, $"false | {ex.Message}");
            }
        }
        public async Task<X509Certificate2?> GetFtpServerCertificate(string host, int port)
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port);

            var stream = tcp.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            // read welcome message
            await reader.ReadLineAsync();

            // request TLS upgrade
            await writer.WriteLineAsync("AUTH TLS");
            await reader.ReadLineAsync();

            var ssl = new SslStream(stream, false, (sender, cert, chain, errors) => true);

            await ssl.AuthenticateAsClientAsync(host);

            if (ssl.RemoteCertificate == null)
                return null;

            return new X509Certificate2(ssl.RemoteCertificate);
        }
    }
}
