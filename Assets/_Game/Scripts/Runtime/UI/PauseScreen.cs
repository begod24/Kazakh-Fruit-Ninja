using UnityEngine;

namespace KazakhNinja
{
    public sealed class PauseScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] UiRoot root;
        [SerializeField] UnityEngine.UI.Button resumeButton;
        [SerializeField] UnityEngine.UI.Button restartButton;
        [SerializeField] UnityEngine.UI.Button settingsButton;
        [SerializeField] UnityEngine.UI.Button menuButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton.onClick.AddListener(game.Resume);
            restartButton.onClick.AddListener(game.Restart);
            settingsButton.onClick.AddListener(() => root.ShowSettings(true));
            menuButton.onClick.AddListener(game.EnterMenu);
        }
    }
}
