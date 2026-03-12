using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// Displays a brief popup on the left side of the screen when a monster is recruited.
    /// Shows "[MonsterTypeName] [Name] was recruited!" for 5 seconds.
    /// </summary>
    public class RecruitmentNotificationUI
    {
        private Stage _stage;
        private Window _notificationWindow;
        private Label _messageLabel;
        private Skin _skin;
        private float _displayTimer;
        private bool _isVisible;

        private const float DisplayDuration = 5f;
        private const float WindowWidth = 290f;
        private const float WindowHeight = 36f;
        private const float LeftPadding = 10f;

        /// <summary>Initializes the notification UI and registers it with the stage.</summary>
        public void InitializeUI(Stage stage, Skin skin)
        {
            _stage = stage;
            _skin = skin;
            CreateNotificationWindow();
        }

        private void CreateNotificationWindow()
        {
            _notificationWindow = new Window("", _skin);
            _notificationWindow.SetSize(WindowWidth, WindowHeight);
            // Override all background-derived padding so the label can fill the full window area
            _notificationWindow.Pad(0f);

            _messageLabel = new Label("", _skin);
            // Center text vertically within the label bounds
            _messageLabel.SetAlignment(Align.Center);
            _notificationWindow.Add(_messageLabel).Pad(4f, 8f, 4f, 8f).Expand().Fill();

            _notificationWindow.SetVisible(false);
        }

        /// <summary>Displays the notification with the given message for 5 seconds.</summary>
        public void Show(string message)
        {
            _messageLabel.SetText(message);
            _displayTimer = DisplayDuration;

            if (!_isVisible)
            {
                _stage.AddElement(_notificationWindow);
                _notificationWindow.SetVisible(true);
                _notificationWindow.ToFront();
                _isVisible = true;
            }

            PositionNotification();
            Debug.Log($"[RecruitmentNotificationUI] Showing: {message}");
        }

        private void PositionNotification()
        {
            if (_notificationWindow == null) return;
            float stageH = _stage.GetHeight();
            float winH = _notificationWindow.GetHeight();
            float y = (stageH / 2f) - (winH / 2f);
            _notificationWindow.SetPosition(LeftPadding, y);
        }

        /// <summary>Polls for pending recruitment notifications and advances the hide timer.</summary>
        public void Update()
        {
            // Poll the manager for new notifications
            var manager = Core.Services.GetService<AlliedMonsterManager>();
            if (manager != null && manager.HasPendingNotification)
            {
                string msg = manager.DequeueNotification();
                if (msg != null)
                    Show(msg);
            }

            if (_isVisible)
            {
                _displayTimer -= Nez.Time.DeltaTime;
                if (_displayTimer <= 0f)
                {
                    _notificationWindow.SetVisible(false);
                    _notificationWindow.Remove();
                    _isVisible = false;
                    Debug.Log("[RecruitmentNotificationUI] Notification hidden");
                }
            }
        }
    }
}
