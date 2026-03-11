using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json.Serialization;

namespace WebFTPViewer.Client
{
    [JsonSerializable(typeof(RememberMeDto))]
    public class RememberMeDto
    {
        public string Username { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 21;
        public bool RememberMe { get; set; } = false;
        public bool PassiveMode { get; set; } = true;
    }
    public class UploadQueItemDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public double Proggress { get; set; } = 0;
        public string? Problem { get; set; } = null;
        public UploadQueItemDto(string name, double proggress)
        {
            Name = name;
            Proggress = proggress;
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
}
