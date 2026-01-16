using System.ComponentModel.DataAnnotations;

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
        public string Type { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string Permissions { get; set; }
    }
}
