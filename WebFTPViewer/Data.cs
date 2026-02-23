using FluentFTP;
using Shared;

namespace WebFTPViewer
{
    public class FTPStorage
    {
        public FtpClient MainClient { get; set; }
        public LoginJson LoginJson { get; set; }
        public Dictionary<string, Pair<FtpClient, Stream>> DownloadQue { get; set; } = new();
        public Dictionary<string, Pair<FtpClient, Stream>> UploadQue { get; set; } = new();
    }
}
