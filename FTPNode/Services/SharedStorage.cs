using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FTPNode.Services
{
    public class SharedStorage : ISharedStorage, IDisposable
    {
        public ConcurrentDictionary<string, FTPStorage> _ftpClients { get; } = new();
        private readonly Dictionary<string, object> _args = new();
        private RSA _rsa = RSA.Create(2048);
        public string PublicKey => Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
        private readonly object _lock = new();
        public byte[] Decrypt(byte[] data)
        {
            lock (_lock)
            {
                return _rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
            }
        }
        public void SetArg(string key, object value)
        {
            _args[key] = value; // overwrites if key exists
        }

        public T GetArg<T>(string key)
        {
            if (!_args.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found in shared service.");
            return (T)value;
        }

        public bool TryGetArg<T>(string key, out T value)
        {
            if (_args.TryGetValue(key, out var obj) && obj is T castValue)
            {
                value = castValue;
                return true;
            }

            value = default!;
            return false;
        }

        public void Dispose()
        {
            _rsa.Dispose();
        }
    }
}
