namespace Shared
{
    public class AppState
    {
        public event Action? BackClicked;
        public event Action? ForwardClicked;
        public event Action? LogoutClicked;
        public event Action? UploadClicked;
        public event Func<Task>? DownloadClicked;
        public event Action? DeleteClicked;
        public event Func<Task>? PermChangeClicked;
        public event Action? EditClicked;
        public event Func<Task>? ViewClicked;
        public event Func<Task>? RenameClicked;
        public event Func<Task>? CreateFolderClicked;

        public void TriggerBack() => BackClicked?.Invoke();
        public void TriggerForward() => ForwardClicked?.Invoke();
        public void TriggerLogout() => LogoutClicked?.Invoke();
        public void TriggerUpload() => UploadClicked?.Invoke();
        public async Task TriggerDownload()
        {
            if (DownloadClicked is not null)
                await DownloadClicked.Invoke();
        }
        public void TriggerDelete() => DeleteClicked?.Invoke();
        public async Task TriggerPermChange()
        {
            if (PermChangeClicked is not null)
                await PermChangeClicked?.Invoke();
        }
        
        public void TriggerEdit() => EditClicked?.Invoke();
        public async Task TriggerView()
        {
            if (ViewClicked is not null)
                await ViewClicked.Invoke();
        }
        public async Task TriggerRename()
        {
            if (RenameClicked is not null)
            {
                await RenameClicked.Invoke();
                NotifyStateChanged();
            }
        }
        public async Task TriggerCreateFolder()
        {
            if (CreateFolderClicked is not null)
                await CreateFolderClicked.Invoke();
        }

        private FtpItemDto? _Selected = null;
        public FtpItemDto? Selected
        {
            get => _Selected;
            set
            {
                if (_Selected != value)
                {
                    _Selected = value;
                    NotifyStateChanged();
                }
            }
        }
        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();

        public Dictionary<string, string> Settings = new();
    }
}
