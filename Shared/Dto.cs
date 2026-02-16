using System.ComponentModel.DataAnnotations;
using System.Security;

namespace Shared
{
    public class Pair
    {
        public string Name { get; set; }
        public string Value { get; set; }
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
}
