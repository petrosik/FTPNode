using System.Collections.Concurrent;

namespace WebFTPViewer.Services
{
    public interface ISharedStorage
    {
        ConcurrentDictionary<string, FTPStorage> _ftpClients { get; }
        void SetArg(string key, object value);
        T GetArg<T>(string key);
        bool TryGetArg<T>(string key, out T value);
    }
}
