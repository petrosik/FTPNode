using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FTPNode.Services
{
    public interface ISharedStorage
    {
        ConcurrentDictionary<string, FTPStorage> _ftpClients { get; }
        string PublicKey { get; }
        byte[] Decrypt(byte[] data);
        void SetArg(string key, object value);
        T GetArg<T>(string key);
        bool TryGetArg<T>(string key, out T value);
    }
}
