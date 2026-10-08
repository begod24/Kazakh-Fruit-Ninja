using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KazakhNinja
{
    /// <summary>
    /// Whole-screen mood: post-processing overlays blended in by weight (Қымыз slow motion, the pepper's blast,
    /// danger on the last life), the шашу rain during Той and the spring petals during Наурыз, and the
    /// graphics level (bloom on or off). All the overlays share URP's single post-processing pass.
    /// </summary>
    public sealed class ScreenFx : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] Camera gameCamera;

        [Header("Overlays (volumes, weight 0 when idle)")]
        [SerializeField] Volume slowTime;
        [SerializeField] Volume impact;
        [SerializeField] Volume danger;
        [SerializeField] float slowFadeSpeed = 3f;
        [SerializeField] float impactFadeSpeed = 1.8f;
        [SerializeField] float dangerFadeSpeed = 1.5f;

        [Header("Power-up weather")]
        [Tooltip("Coins and sweets falling while Той doubles the score.")]
        [SerializeField] ParticleSystem shashuRain;
        [Tooltip("Petals blowing across while Наурыз throws food from the sides.")]
        [SerializeField] ParticleSystem petalWind;
        [Tooltip("Depth of the weather: behind the food, in front of the backdrop.")]
        [SerializeField] float weatherDepth = 2f;

        UniversalAdditionalCameraData cameraData;
        float impactWeight;
        float dangerPulse;

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            cameraData = gameCamera.GetUniversalAdditionalCameraData();
            foreach (Volume volume in new[] { slowTime, impact, danger })
                if (volume) volume.weight = 0f;
            PlaceWeather();
            SetEmitting(shashuRain, false);
            SetEmitting(petalWind, false);
            ApplyGraphics();
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplyGraphics;
            GameEvents.BombHit += OnBombHit;
            game.Feedback += OnFeedback;
        }

        void OnDisable()
        {
            GameSettings.Changed -= ApplyGraphics;
            GameEvents.BombHit -= OnBombHit;
            game.Feedback -= OnFeedback;
        }

        void ApplyGraphics()
        {
            if (cameraData) cameraData.renderPostProcessing = GameSettings.HighGraphics;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool inRound = game.State is GameState.Playing or GameState.Paused;
            PowerUpTimers powerUps = game.PowerUps;

            if (slowTime)
                slowTime.weight = Mathf.MoveTowards(slowTime.weight, inRound && powerUps.IsActive(PowerUpType.SlowTime) ? 1f : 0f, slowFadeSpeed * dt);

            impactWeight = Mathf.MoveTowards(impactWeight, 0f, impactFadeSpeed * dt);
            if (impact) impact.weight = impactWeight;

            // The last bowl: a slow red heartbeat at the edges.
            GameSession session = game.Session;
            float heartbeat = inRound && session != null && session.HasLives && session.Lives == 1
                ? 0.3f + 0.18f * Mathf.Sin(Time.unscaledTime * 4.5f)
                : 0f;
            dangerPulse = Mathf.MoveTowards(dangerPulse, 0f, dangerFadeSpeed * dt);
            if (danger) danger.weight = Mathf.Max(heartbeat, dangerPulse);

            bool playing = game.State == GameState.Playing;
            SetEmitting(shashuRain, playing && powerUps.IsActive(PowerUpType.DoubleScore));
            SetEmitting(petalWind, playing && powerUps.IsActive(PowerUpType.Frenzy));
        }

        void OnBombHit(FoodHitEvent e)
        {
            if (game.State == GameState.Playing) impactWeight = 1f;
        }

        void OnFeedback(GameFeedback feedback)
        {
            if (feedback.Kind is FeedbackKind.LifeLost or FeedbackKind.BombLifeLost) dangerPulse = 0.85f;
        }

        /// <summary>The rain falls from above the top edge; the petals blow in from the left.</summary>
        void PlaceWeather()
        {
            Rect view = Playfield.GetVisibleRect(gameCamera, weatherDepth);
            if (shashuRain)
            {
                shashuRain.transform.position = new Vector3(view.center.x, view.yMax + 0.6f, weatherDepth);
                ParticleSystem.ShapeModule shape = shashuRain.shape;
                shape.scale = new Vector3(view.width, 0.1f, 0.1f);
            }
            if (petalWind)
            {
                petalWind.transform.position = new Vector3(view.xMin - 0.6f, view.center.y, weatherDepth);
                ParticleSystem.ShapeModule shape = petalWind.shape;
                shape.scale = new Vector3(0.1f, view.height, 0.1f);
            }
        }

        static void SetEmitting(ParticleSystem system, bool on)
        {
            if (system == null) return;
            ParticleSystem.EmissionModule emission = system.emission;
            if (emission.enabled == on) return;
            emission.enabled = on;
            if (on && !system.isPlaying) system.Play(true);
        }
    }
}
