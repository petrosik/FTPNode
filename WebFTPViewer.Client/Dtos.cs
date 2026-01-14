namespace WebFTPViewer.Client
{
    public class RememberMeDto
    {
        public string Username { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 21;
        public bool RememberMe { get; set; } = false;
    }
}
