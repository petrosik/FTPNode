namespace Shared
{
    public class AppState
    {
        public event Action? BackClicked;
        public event Action? ForwardClicked;

        public void TriggerBack() => BackClicked?.Invoke();
        public void TriggerForward() => ForwardClicked?.Invoke();

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
