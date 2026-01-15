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
}
