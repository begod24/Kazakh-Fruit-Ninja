using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The two mode cards, opened by a swipe on the main page; each shows its record.</summary>
    public sealed class ModeSelectScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] UiRoot root;
        [SerializeField] UnityEngine.UI.Button classicButton;
        [SerializeField] UnityEngine.UI.Button arcadeButton;
        [SerializeField] TMP_Text classicBest;
        [SerializeField] TMP_Text arcadeBest;
        [SerializeField] UnityEngine.UI.Button backButton;

        protected override void Awake()
        {
            base.Awake();
            classicButton.onClick.AddListener(() => game.StartGame(game.ClassicMode));
            arcadeButton.onClick.AddListener(() => game.StartGame(game.ArcadeMode));
            backButton.onClick.AddListener(() => root.ShowPage(MenuPage.Home));
        }

        protected override void OnShow()
        {
            classicBest.SetText(UiText.BestFormat.Format(game.GetBest(GameModeType.Classic)));
            arcadeBest.SetText(UiText.BestFormat.Format(game.GetBest(GameModeType.Arcade)));
        }
    }
}
