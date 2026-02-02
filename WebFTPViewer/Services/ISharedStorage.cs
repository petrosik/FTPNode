namespace WebFTPViewer.Services
{
    public interface ISharedStorage
    {
        void SetArg(string key, object value);
        T GetArg<T>(string key);
        bool TryGetArg<T>(string key, out T value);
    }
}
