using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Screen shake, hit-stop, graphics level and language; opened from the main menu and the pause card.</summary>
    public sealed class SettingsScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] UiRoot root;
        [SerializeField] UnityEngine.UI.Button shakeButton;
        [SerializeField] TMP_Text shakeValue;
        [SerializeField] UnityEngine.UI.Button hitStopButton;
        [SerializeField] TMP_Text hitStopValue;
        [SerializeField] UnityEngine.UI.Button graphicsButton;
        [SerializeField] TMP_Text graphicsValue;
        [Tooltip("One per Language value, in its order.")]
        [SerializeField] UnityEngine.UI.Button[] languageButtons = new UnityEngine.UI.Button[0];
        [SerializeField] UnityEngine.UI.Button backButton;

        [Header("Style")]
        [SerializeField] Color onColor = new(0.99f, 0.85f, 0.5f);
        [SerializeField] Color offColor = new(1f, 0.957f, 0.863f, 0.6f);
        [Tooltip("Frame of the chosen language.")]
        [SerializeField] Color chosenFrame = new(0.99f, 0.85f, 0.5f);
        [SerializeField] Color otherFrame = new(0.91f, 0.78f, 0.47f, 0.3f);

        protected override void Awake()
        {
            base.Awake();
            shakeButton.onClick.AddListener(() => Change(() => GameSettings.ScreenShake = !GameSettings.ScreenShake));
            hitStopButton.onClick.AddListener(() => Change(() => GameSettings.HitStop = !GameSettings.HitStop));
            graphicsButton.onClick.AddListener(() => Change(() =>
                GameSettings.Graphics = GameSettings.HighGraphics ? GraphicsQuality.Low : GraphicsQuality.High));
            for (int i = 0; i < languageButtons.Length; i++)
            {
                var language = (Language)i;
                languageButtons[i].onClick.AddListener(() => Change(() => Loc.Language = language));
            }
            backButton.onClick.AddListener(() => root.ShowSettings(false));
        }

        void OnEnable() => Loc.Changed += Refresh;
        void OnDisable() => Loc.Changed -= Refresh;

        protected override void OnShow() => Refresh();

        void Change(System.Action change)
        {
            change();
            SaveSystem.Save(game.Save);
            Refresh();
        }

        void Refresh()
        {
            Show(shakeValue, GameSettings.ScreenShake, UiText.On, UiText.Off);
            Show(hitStopValue, GameSettings.HitStop, UiText.On, UiText.Off);
            Show(graphicsValue, GameSettings.HighGraphics, UiText.GraphicsHigh, UiText.GraphicsLow);

            for (int i = 0; i < languageButtons.Length; i++)
            {
                bool chosen = (int)Loc.Language == i;
                if (languageButtons[i].targetGraphic) languageButtons[i].targetGraphic.color = chosen ? chosenFrame : otherFrame;
                TMP_Text label = languageButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label)
                {
                    label.SetText(Loc.OptionName((Language)i));
                    label.color = chosen ? onColor : offColor;
                }
            }
        }

        void Show(TMP_Text label, bool on, Phrase onText, Phrase offText)
        {
            label.SetText(on ? onText.Text : offText.Text);
            label.color = on ? onColor : offColor;
        }
    }
}
