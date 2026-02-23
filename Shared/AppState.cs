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
        public event Action? PermChangeClicked;
        public event Action? EditClicked;
        public event Action? ViewClicked;

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
        public void TriggerPermChange() => PermChangeClicked?.Invoke();
        public void TriggerEdit() => EditClicked?.Invoke();
        public void TriggerView() => ViewClicked?.Invoke();

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
    }
}
