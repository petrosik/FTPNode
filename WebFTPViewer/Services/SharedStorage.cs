namespace WebFTPViewer.Services
{
    public class SharedStorage : ISharedStorage
    {
        private readonly Dictionary<string, object> _args = new();

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
    }
}
