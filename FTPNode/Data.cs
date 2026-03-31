using FluentFTP;
using Shared;
using System.Collections.Concurrent;

namespace FTPNode
{
    public class FTPStorage
    {
        public FtpClient MainClient { get; set; }
        public string CurrentPath { get; set; } = "/";
        public LoginJson LoginJson { get; set; }
        public ConcurrentDictionary<string, Pair<FtpClient, Stream>> DownloadQue { get; set; } = new();
        public ConcurrentDictionary<string, Pair<FtpClient, Stream>> UploadQue { get; set; } = new();
    }
}
