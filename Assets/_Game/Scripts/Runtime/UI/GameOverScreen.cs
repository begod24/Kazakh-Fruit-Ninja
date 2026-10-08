using System.Text;
using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Round results: why it ended, the score (counted up), the record, a couple of stats and new blades.</summary>
    public sealed class GameOverScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] CollectionManager collection;
        [SerializeField] LocalizedLabel title;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] GameObject newBestBadge;
        [SerializeField] TMP_Text slicedText;
        [SerializeField] TMP_Text comboText;
        [Tooltip("Shown when an achievement opened a blade during the round.")]
        [SerializeField] GameObject unlockGroup;
        [SerializeField] TMP_Text unlockText;
        [SerializeField] UnityEngine.UI.Button againButton;
        [SerializeField] UnityEngine.UI.Button menuButton;
        [SerializeField] float countUpDuration = 0.9f;

        readonly StringBuilder names = new();
        float shownTime;
        int finalScore;
        int shownScore;

        protected override void Awake()
        {
            base.Awake();
            againButton.onClick.AddListener(game.Restart);
            menuButton.onClick.AddListener(game.EnterMenu);
        }

        void OnEnable() => Loc.Changed += ShowTexts;
        void OnDisable() => Loc.Changed -= ShowTexts;

        protected override void OnShow()
        {
            GameSession session = game.Session;
            if (session == null) return;
            finalScore = session.Score;
            shownScore = -1;
            shownTime = Time.unscaledTime;
            // A new record equals the score, so the badge takes the record line's place.
            bestText.gameObject.SetActive(!game.IsNewBest);
            newBestBadge.SetActive(game.IsNewBest);
            ShowTexts();
        }

        void ShowTexts()
        {
            GameSession session = game.Session;
            if (session == null || game.Mode == null) return;
            title.SetKey(UiText.GameOverTitle(session.EndReason).Key);
            bestText.SetText(UiText.BestFormat.Format(game.GetBest(game.Mode.type)));
            slicedText.SetText(UiText.SlicedFormat.Format(session.SlicedCount));
            comboText.SetText(UiText.BestComboFormat.Format(session.BestCombo));

            names.Clear();
            if (collection)
                foreach (BladeDefinition blade in collection.UnlockedThisRound)
                    names.Append(names.Length > 0 ? ", " : "").Append(UiText.Name(blade));
            unlockGroup.SetActive(names.Length > 0);
            if (names.Length > 0) unlockText.SetText(UiText.NewBladeFormat.Format(names.ToString()));
        }

        protected override void Update()
        {
            base.Update();
            if (!IsVisible) return;
            float t = Mathf.Clamp01((Time.unscaledTime - shownTime) / countUpDuration);
            int score = Mathf.RoundToInt(finalScore * (1f - (1f - t) * (1f - t)));
            if (score == shownScore) return;
            shownScore = score;
            scoreText.SetText("{0}", score);
        }
    }
}
