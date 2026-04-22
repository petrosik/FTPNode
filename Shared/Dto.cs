using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Shared
{
    public class Pair
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public Pair(string name, string value)
        {
            Name = name;
            Value = value;
        }
        public Pair() { }
    }
    public class LoginJson
    {
        [Required]
        public string Host { get; set; }
        [Required]
        public int Port { get; set; } = 21;
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        public bool PassiveMode { get; set; } = true;
        public bool AcceptCert { get; set; } = false;
        public string OriginalCertThumbprint { get; set; }

        public LoginJson(string host, int port, string username, string password, bool passivemode)
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
            PassiveMode = passivemode;
        }
        public LoginJson() { }
    }
    public class FtpItemDto
    {
        public string Name { get; set; }
        public FileType Type { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public int Permissions { get; set; }
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj is FtpItemDto dto)
            {
                if (ReferenceEquals(this, dto))
                    return true;
                else if (dto.Name == Name && dto.Type == Type && dto.Size == Size && dto.Modified == Modified && dto.Permissions == Permissions)
                    return true;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Type.GetHashCode() ^ Size.GetHashCode() ^ Permissions.GetHashCode();
        }
    }
    public class UploadMetadataDto
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public int Permissions { get; set; } = 700;
        public string UploadPath { get; set; }
    }
    public class Pair<T1, T2>
    {
        public T1 First { get; set; }
        public T2 Second { get; set; }
        public Pair(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }
    }

    public class CertificateDto
    {
        // Basic fields
        public string Subject { get; set; }               // "Issued To"
        public string Issuer { get; set; }                // "Issued By"
        public string Thumbprint { get; set; }
        public string SerialNumber { get; set; }
        public DateTime NotBefore { get; set; }          // Valid from
        public DateTime NotAfter { get; set; }           // Valid to
        public string SignatureAlgorithm { get; set; }
        public string PublicKeyAlgorithm { get; set; }
        public int PublicKeyLength { get; set; }         // e.g., 2048

        // Extensions (key usages, SANs, basic constraints, etc.)
        public List<Pair<string, string>> Extensions { get; set; } = new();

        // Chain elements (intermediate and root certificates)
        public List<CertificateChainElementDto> Chain { get; set; } = new();

        // Optional: raw base64 if you want to allow download/export
        public string RawDataBase64 { get; set; }
    }

    public class CertificateChainElementDto
    {
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public string Thumbprint { get; set; }
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

    [JsonSerializable(typeof(RememberSettingsDto))]
    public class RememberSettingsDto
    {
        public string Username { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 21;
        public bool RememberMe { get; set; } = false;
        public bool PassiveMode { get; set; } = true;
        public string Theme { get; set; } = "";
    }
}
