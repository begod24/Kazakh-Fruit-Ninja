using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>In-round HUD: score, record, lives (hearts), timer, active power-ups and the pause button.</summary>
    public sealed class HudScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] GameObject timerGroup;
        [SerializeField] TMP_Text timerText;
        [SerializeField] GameObject livesGroup;
        [Tooltip("Left to right; the rightmost is lost first.")]
        [SerializeField] UnityEngine.UI.Image[] lifeIcons;
        [SerializeField] PowerUpIndicator[] powerUpIndicators;
        [SerializeField] UnityEngine.UI.Button pauseButton;

        [Header("Style")]
        [Tooltip("Tint of a heart still in play (the sprite carries its own red).")]
        [SerializeField] Color lifeColor = Color.white;
        [SerializeField] Color lostLifeColor = new(0.3f, 0.3f, 0.3f, 0.55f);
        [SerializeField] Color timerColor = Color.white;
        [SerializeField] Color timerWarningColor = new(1f, 0.35f, 0.3f);
        [Tooltip("Seconds left when the timer turns red.")]
        [SerializeField] float timerWarning = 10f;

        GameSession boundSession;
        float displayedScore;
        int shownScore = -1;
        int shownLives = -1;
        int shownSeconds = -1;
        float scorePunch;
        float[] lifePunch = new float[0];

        protected override void Awake()
        {
            base.Awake();
            pauseButton.onClick.AddListener(game.Pause);
            lifePunch = new float[lifeIcons.Length];
        }

        protected override void OnShow() => Bind(game.Session);

        void OnEnable() => Loc.Changed += ShowBest;
        void OnDisable() => Loc.Changed -= ShowBest;

        void ShowBest()
        {
            if (game.Mode != null) bestText.SetText(UiText.BestShortFormat.Format(game.GetBest(game.Mode.type)));
        }

        /// <summary>Resets the HUD for a round; also runs when a round restarts while the HUD stays up.</summary>
        void Bind(GameSession session)
        {
            boundSession = session;
            if (session == null) return;
            displayedScore = session.Score;
            shownScore = shownLives = shownSeconds = -1;
            timerGroup.SetActive(session.HasTimer);
            livesGroup.SetActive(session.HasLives);
            ShowBest();
            for (int i = 0; i < lifeIcons.Length; i++) lifeIcons[i].gameObject.SetActive(i < session.MaxLives);
        }

        protected override void Update()
        {
            base.Update();
            GameSession session = game.Session;
            if (session != boundSession) Bind(session);
            if (session == null) return;
            float dt = Time.unscaledDeltaTime;

            // Roll the score up quickly instead of jumping.
            displayedScore = Mathf.MoveTowards(displayedScore, session.Score, Mathf.Max(20f, Mathf.Abs(session.Score - displayedScore) * 10f) * dt);
            int score = Mathf.RoundToInt(displayedScore);
            if (score != shownScore)
            {
                if (shownScore >= 0 && score > shownScore) scorePunch = 1f;
                shownScore = score;
                scoreText.SetText("{0}", score);
            }
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, dt * 5f);
            scoreText.rectTransform.localScale = Vector3.one * (1f + 0.15f * scorePunch);

            if (session.HasLives && session.Lives != shownLives)
            {
                for (int i = 0; i < lifeIcons.Length; i++)
                {
                    bool alive = i < session.Lives;
                    if (!alive && shownLives > i) lifePunch[i] = 1f;
                    lifeIcons[i].color = alive ? lifeColor : lostLifeColor;
                    lifeIcons[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, alive ? 0f : -12f);
                }
                shownLives = session.Lives;
            }
            for (int i = 0; i < lifePunch.Length; i++)
            {
                lifePunch[i] = Mathf.MoveTowards(lifePunch[i], 0f, dt * 3f);
                lifeIcons[i].rectTransform.localScale = Vector3.one * (1f + 0.5f * lifePunch[i]);
            }

            if (session.HasTimer)
            {
                int seconds = Mathf.CeilToInt(session.TimeLeft);
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    timerText.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
                    timerText.color = session.TimeLeft <= timerWarning ? timerWarningColor : timerColor;
                }
                float warn = session.TimeLeft <= timerWarning ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
                timerText.rectTransform.localScale = new Vector3(warn, warn, 1f);
            }

            foreach (PowerUpIndicator indicator in powerUpIndicators)
                indicator.Show(game.PowerUps.Fraction(indicator.Type));
        }
    }
}
