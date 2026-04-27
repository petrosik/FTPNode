using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json.Serialization;

namespace FTPNode.Client
{
    [JsonSerializable(typeof(TrustedCertDto))]
    public class TrustedCertDto
    {
        public string Name { get; set; }
        public string Thumbprint { get; set; }
        public DateTime Added { get; set; }
        public TrustedCertDto(string name, string thumbprint, DateTime added)
        {
            Name = name;
            Thumbprint = thumbprint;
            Added = added;
        }
    }
    public class InMemoryBrowserFile : IBrowserFile
    {
        private readonly byte[] _data;

        public InMemoryBrowserFile(byte[] data, string name, string contentType)
        {
            _data = data;
            Name = name;
            ContentType = contentType;
            Size = data.Length;
            LastModified = DateTimeOffset.Now;
        }

        public string Name { get; }

        public DateTimeOffset LastModified { get; }

        public long Size { get; }

        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = long.MaxValue, CancellationToken cancellationToken = default)
        {
            return new MemoryStream(_data);
        }
    }
    public class KeyInfo
    {
        public string Key { get; set; }
        public bool Ctrl { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Meta { get; set; }
    }
}
