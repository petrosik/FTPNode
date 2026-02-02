namespace Shared
{
    public class AppState
    {
        public event Action? BackClicked;
        public event Action? ForwardClicked;
        public event Action? LogoutClicked;
        public event Action? UploadClicked;

        public void TriggerBack() => BackClicked?.Invoke();
        public void TriggerForward() => ForwardClicked?.Invoke();
        public void TriggerLogout() => LogoutClicked?.Invoke();
        public void TriggerUpload() => UploadClicked?.Invoke();

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

        private bool _FullPage = true;
        public bool FullPage
        {
            get => _FullPage;
            set
            {
                if (_FullPage != value)
                {
                    _FullPage = value;
                    NotifyStateChanged();
                }
            }
        }
        /// <summary>
        /// A trigger for when fullpage flag is changed
        /// </summary>
        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
