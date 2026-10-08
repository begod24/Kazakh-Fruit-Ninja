using System;
using UnityEngine;

namespace KazakhNinja
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        /// <summary>The round is over; the last food is still falling before the results show.</summary>
        Ending,
        GameOver,
    }

    public enum FeedbackKind
    {
        /// <summary>Value = foods in the stroke, Extra = bonus points.</summary>
        Combo,
        /// <summary>Value = points gained.</summary>
        GoldenHit,
        /// <summary>Value = points lost.</summary>
        BombPenalty,
        /// <summary>The bomb took a life; Value = lives left.</summary>
        BombLifeLost,
        BombGameOver,
        /// <summary>Value = lives left.</summary>
        LifeLost,
        PowerUp,
    }

    /// <summary>Something the player should see pop up: a combo, a penalty, a lost life…</summary>
    public readonly struct GameFeedback
    {
        public readonly FeedbackKind Kind;
        public readonly int Value;
        public readonly int Extra;
        public readonly Vector3 Position;
        public readonly PowerUpType PowerUp;

        public GameFeedback(FeedbackKind kind, Vector3 position, int value = 0, int extra = 0, PowerUpType powerUp = default)
        {
            Kind = kind;
            Position = position;
            Value = value;
            Extra = extra;
            PowerUp = powerUp;
        }
    }

    /// <summary>
    /// Runs rounds: turns slices, misses and bombs into score and lives, applies power-ups,
    /// drives the spawner's pacing and keeps the records.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] FoodSpawner spawner;
        [SerializeField] GameModeDefinition classicMode;
        [SerializeField] GameModeDefinition arcadeMode;

        [Header("Menu")]
        [Tooltip("Food keeps flying behind the main menu; slicing it scores nothing.")]
        [SerializeField] bool menuAttractMode = true;
        [SerializeField] SpawnPacing menuPacing = new()
        {
            waveInterval = new Vector2(2.2f, 3.4f),
            foodPerWave = new Vector2Int(1, 2),
        };

        [Header("Feel")]
        [Tooltip("Longest pause between two slices of one combo stroke, in seconds.")]
        [SerializeField] float comboWindow = 0.25f;
        [SerializeField, Range(0.1f, 1f)] float slowTimeScale = 0.5f;
        [Tooltip("Seconds between a bomb ending the round and the results screen.")]
        [SerializeField] float bombEndDelay = 1.4f;
        [SerializeField] float otherEndDelay = 0.8f;

        ComboCounter combo;
        float resultsTime;
        /// <summary>Time scale before hit-stop: 1, or slower during Қымыз.</summary>
        float baseTimeScale = 1f;
        bool attractHeld;

        public GameState State { get; private set; } = GameState.Menu;
        public GameSession Session { get; private set; }
        public GameModeDefinition Mode { get; private set; }
        public GameModeDefinition ClassicMode => classicMode;
        public GameModeDefinition ArcadeMode => arcadeMode;
        public PowerUpTimers PowerUps { get; } = new();
        SaveData save;

        /// <summary>Loaded on first use, so other components can read it from their own Awake.</summary>
        public SaveData Save => save ??= SaveSystem.Load();
        /// <summary>The last finished round set a new record.</summary>
        public bool IsNewBest { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<GameFeedback> Feedback;
        /// <summary>The round is over and its record, combo and count are stored (just before <see cref="GameState.Ending"/>).</summary>
        public event Action RoundRecorded;

        public int GetBest(GameModeType mode) => Save.GetBest(mode);

        void Awake()
        {
            combo = new ComboCounter(comboWindow);
            GameSettings.Bind(Save);
            Loc.Bind(Save);
        }

        void OnEnable()
        {
            GameEvents.FoodSliced += OnFoodSliced;
            GameEvents.GoldenHit += OnGoldenHit;
            GameEvents.BombHit += OnBombHit;
            GameEvents.FoodMissed += OnFoodMissed;
        }

        void OnDisable()
        {
            GameEvents.FoodSliced -= OnFoodSliced;
            GameEvents.GoldenHit -= OnGoldenHit;
            GameEvents.BombHit -= OnBombHit;
            GameEvents.FoodMissed -= OnFoodMissed;
            HitStop.Cancel();
            Time.timeScale = 1f;
        }

        void Start() => EnterMenu();

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        // ---------- Flow ----------

        public void StartGame(GameModeDefinition mode)
        {
            if (mode == null) return;
            if (Session != null) Session.Ended -= OnSessionEnded;

            Mode = mode;
            Session = new GameSession(mode.rules);
            Session.Ended += OnSessionEnded;
            PowerUps.Clear();
            combo.Reset();
            IsNewBest = false;

            spawner.Clear();
            spawner.IsFrenzy = false;
            spawner.Pacing = mode.Evaluate(0f);
            spawner.IsSpawning = true;
            spawner.RestartWaves();
            SetTimeScale(1f);
            SetState(GameState.Playing);
        }

        public void Restart() => StartGame(Mode);

        public void Pause()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            SetState(GameState.Playing);
            ApplyPowerUps();
        }

        public void EnterMenu()
        {
            if (Session != null) Session.Ended -= OnSessionEnded;
            Session = null;
            PowerUps.Clear();
            SetTimeScale(1f);

            spawner.Clear();
            spawner.IsFrenzy = false;
            spawner.Pacing = menuPacing;
            spawner.IsSpawning = menuAttractMode && !attractHeld;
            SetState(GameState.Menu);
        }

        /// <summary>Keeps food from flying behind the menu, e.g. while the intro plays.</summary>
        public void HoldAttract(bool hold)
        {
            attractHeld = hold;
            if (State == GameState.Menu) spawner.IsSpawning = menuAttractMode && !hold;
        }

        void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        void Update()
        {
            switch (State)
            {
                case GameState.Playing:
                    Session.Tick(Time.deltaTime);
                    if (State != GameState.Playing) return; // the clock just ran out
                    PowerUps.Tick(Time.unscaledDeltaTime);
                    ApplyPowerUps();
                    spawner.Pacing = Mode.Evaluate(Session.Elapsed);
                    if (combo.TryComplete(Time.unscaledTime, out int count, out Vector3 position))
                    {
                        int bonus = Session.AddPoints(count);
                        Session.RegisterCombo(count);
                        // Kept straight away (saved with the round), so a combo achievement opens mid-round.
                        Save.TrySetBestCombo(count);
                        Feedback?.Invoke(new GameFeedback(FeedbackKind.Combo, position, count, bonus));
                    }
                    break;
                case GameState.Ending when Time.unscaledTime >= resultsTime:
                    SetState(GameState.GameOver);
                    break;
            }
            // Hit-stop freezes the game for a moment on big hits; pausing owns the clock instead.
            if (State != GameState.Paused) Time.timeScale = baseTimeScale * HitStop.Factor;
        }

        void SetTimeScale(float scale)
        {
            baseTimeScale = scale;
            Time.timeScale = scale * HitStop.Factor;
        }

        void ApplyPowerUps()
        {
            SetTimeScale(PowerUps.IsActive(PowerUpType.SlowTime) ? slowTimeScale : 1f);
            Session.Multiplier = PowerUps.IsActive(PowerUpType.DoubleScore) ? 2 : 1;
            spawner.IsFrenzy = PowerUps.IsActive(PowerUpType.Frenzy);
        }

        void OnSessionEnded(GameOverReason reason)
        {
            spawner.IsSpawning = false;
            spawner.IsFrenzy = false;
            PowerUps.Clear();
            SetTimeScale(1f);

            IsNewBest = Save.TrySetBest(Mode.type, Session.Score);
            Save.AddGamePlayed(Mode.type);
            SaveSystem.Save(Save);

            RoundRecorded?.Invoke();

            resultsTime = Time.unscaledTime + (reason == GameOverReason.Bomb ? bombEndDelay : otherEndDelay);
            SetState(GameState.Ending);
        }

        // ---------- Gameplay events ----------

        void OnFoodSliced(FoodHitEvent e)
        {
            if (State != GameState.Playing) return;
            Session.RegisterSlice();
            Session.AddPoints(e.Food.points);
            combo.Register(Time.unscaledTime, e.Position);

            if (e.Food.kind == FoodKind.PowerUp && Mode.rules.powerUpsEnabled)
            {
                PowerUps.Activate(e.Food.powerUp, e.Food.powerUpDuration);
                ApplyPowerUps();
                Feedback?.Invoke(new GameFeedback(FeedbackKind.PowerUp, e.Position, powerUp: e.Food.powerUp));
            }
        }

        void OnGoldenHit(FoodHitEvent e)
        {
            if (State != GameState.Playing) return;
            int gained = Session.AddPoints(e.Food.points);
            Feedback?.Invoke(new GameFeedback(FeedbackKind.GoldenHit, e.Position, gained));
        }

        void OnBombHit(FoodHitEvent e)
        {
            if (State != GameState.Playing) return;
            int livesBefore = Session.Lives;
            int lost = Session.Bomb();
            if (Session.IsOver) Feedback?.Invoke(new GameFeedback(FeedbackKind.BombGameOver, e.Position));
            else if (Session.Lives < livesBefore) Feedback?.Invoke(new GameFeedback(FeedbackKind.BombLifeLost, e.Position, Session.Lives));
            else Feedback?.Invoke(new GameFeedback(FeedbackKind.BombPenalty, e.Position, lost));
        }

        void OnFoodMissed(FoodHitEvent e)
        {
            if (State != GameState.Playing || !Session.HasLives) return;
            Session.Miss();
            Feedback?.Invoke(new GameFeedback(FeedbackKind.LifeLost, e.Position, Session.Lives));
        }
    }
}
