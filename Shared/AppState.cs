namespace Shared
{
    public class AppState
    {
        public event Action? BackClicked;
        public event Action? ForwardClicked;
        public event Action? LogoutClicked;
        public event Action? UploadClicked;
        public event Action? DeleteClicked;

        public void TriggerBack() => BackClicked?.Invoke();
        public void TriggerForward() => ForwardClicked?.Invoke();
        public void TriggerLogout() => LogoutClicked?.Invoke();
        public void TriggerUpload() => UploadClicked?.Invoke();
        public void TriggerDelete() => DeleteClicked?.Invoke();

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
