using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The pages of the main menu; one shows at a time.</summary>
    public enum MenuPage
    {
        /// <summary>The name, "slice to play" and the three round buttons.</summary>
        Home,
        Modes,
        Blades,
        Records,
        Settings,
    }

    /// <summary>Shows the screen that matches the game state and, in the menu, the open page.</summary>
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] MainMenuScreen home;
        [SerializeField] ModeSelectScreen modes;
        [SerializeField] BladesScreen blades;
        [SerializeField] RecordsScreen records;
        [SerializeField] SettingsScreen settings;
        [SerializeField] HudScreen hud;
        [SerializeField] PauseScreen pause;
        [SerializeField] GameOverScreen gameOver;

        MenuPage page;
        bool settingsOverPause;

        public MenuPage Page => page;

        void OnEnable() => game.StateChanged += OnStateChanged;
        void OnDisable() => game.StateChanged -= OnStateChanged;
        void Start() => Apply(game.State, true);

        void OnStateChanged(GameState state)
        {
            // Back from a round: the main page, not whichever panel was open before it.
            if (state != GameState.Menu) page = MenuPage.Home;
            settingsOverPause = false;
            Apply(state, false);
        }

        public void ShowPage(MenuPage next)
        {
            page = next;
            Apply(game.State, false);
        }

        /// <summary>Opens the settings over the pause card during a round, or as a menu page.</summary>
        public void ShowSettings(bool open)
        {
            if (game.State == GameState.Paused) settingsOverPause = open;
            else page = open ? MenuPage.Settings : MenuPage.Home;
            Apply(game.State, false);
        }

        void Apply(GameState state, bool instant)
        {
            bool menu = state == GameState.Menu;
            home.SetVisible(menu && page == MenuPage.Home, instant);
            modes.SetVisible(menu && page == MenuPage.Modes, instant);
            blades.SetVisible(menu && page == MenuPage.Blades, instant);
            records.SetVisible(menu && page == MenuPage.Records, instant);
            settings.SetVisible((menu && page == MenuPage.Settings) || (state == GameState.Paused && settingsOverPause), instant);
            hud.SetVisible(state is GameState.Playing or GameState.Paused or GameState.Ending, instant);
            pause.SetVisible(state == GameState.Paused && !settingsOverPause, instant);
            gameOver.SetVisible(state == GameState.GameOver, instant);
        }
    }
}
