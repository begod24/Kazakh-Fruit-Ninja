using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// The opening theme under the intro, menu and in-game music (crossfaded on state changes, ducked while
    /// paused, pitched down in Қымыз slow motion) and slice sounds with variation. Everything runs in real time, so it
    /// keeps working while the game is paused or slowed.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] GameManager game;

        [Header("Music")]
        [Tooltip("Plays once under the intro; the menu music takes over when it ends.")]
        [SerializeField] AudioClip opener;
        [Tooltip("Seconds before the opener's end at which the menu music starts to come in.")]
        [SerializeField] float openerHandover = 3f;
        [SerializeField] AudioClip menuMusic;
        [SerializeField] AudioClip gameMusic;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.6f;
        [SerializeField] float crossfadeTime = 1.2f;
        [Tooltip("Music volume multiplier while paused.")]
        [SerializeField, Range(0f, 1f)] float pausedDuck = 0.4f;
        [Tooltip("Music pitch while time is slowed by Қымыз.")]
        [SerializeField, Range(0.5f, 1f)] float slowMotionPitch = 0.85f;

        [Header("Slicing")]
        [Tooltip("One is picked at random, never the same twice in a row.")]
        [SerializeField] AudioClip[] sliceClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] float sliceVolume = 0.8f;
        [SerializeField] Vector2 slicePitch = new(0.94f, 1.06f);
        [Tooltip("Pitch added for each further food in one quick stroke, so combos rise.")]
        [SerializeField] float strokePitchStep = 0.04f;
        [SerializeField] float strokePitchMax = 0.3f;
        [Tooltip("Longest gap between slices of one stroke, in seconds.")]
        [SerializeField] float strokeWindow = 0.25f;
        [Tooltip("Slices closer together than this share one sound, so big combos do not get too loud.")]
        [SerializeField] float minSliceInterval = 0.035f;
        [SerializeField, Range(1, 16)] int voices = 6;

        [Header("Golden aport hits")]
        [SerializeField, Range(0f, 1f)] float goldenHitVolume = 0.45f;
        [SerializeField] float goldenHitPitch = 1.35f;

        AudioSource[] music;
        int activeMusic;
        AudioSource[] sfx;
        int nextVoice;
        int lastSliceClip = -1;
        int strokeCount;
        float lastSliceTime = float.NegativeInfinity;
        float lastSlicePlayTime = float.NegativeInfinity;

        void Awake()
        {
            music = new[] { CreateSource(true), CreateSource(true) };
            sfx = new AudioSource[voices];
            for (int i = 0; i < voices; i++) sfx[i] = CreateSource(false);
        }

        void OnEnable()
        {
            GameEvents.FoodSliced += OnFoodSliced;
            GameEvents.GoldenHit += OnGoldenHit;
            if (game) game.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.FoodSliced -= OnFoodSliced;
            GameEvents.GoldenHit -= OnGoldenHit;
            if (game) game.StateChanged -= OnStateChanged;
        }

        void Start()
        {
            if (game) OnStateChanged(game.State);
        }

        // ---------- Music ----------

        bool OpenerPlaying => opener != null && music[activeMusic].clip == opener && music[activeMusic].isPlaying;

        void OnStateChanged(GameState state)
        {
            // The opener carries the title screen until it ends; a round cuts it short.
            if (state == GameState.Menu && OpenerPlaying) return;
            PlayMusic(state == GameState.Menu ? menuMusic : gameMusic);
        }

        /// <summary>Starts the opening theme; called by the intro once per launch.</summary>
        public void PlayOpener()
        {
            if (opener == null) return;
            PlayMusic(opener);
            // The theme swells in by itself, so it starts at full volume.
            music[activeMusic].volume = musicVolume;
        }

        /// <summary>Crossfades to <paramref name="clip"/>; keeps playing if it is already the current track.</summary>
        void PlayMusic(AudioClip clip)
        {
            if (clip == null || music[activeMusic].clip == clip && music[activeMusic].isPlaying) return;
            activeMusic = 1 - activeMusic;
            AudioSource next = music[activeMusic];
            if (next.clip == clip && next.isPlaying) return; // still fading out: just bring it back
            next.clip = clip;
            next.loop = clip != opener;
            next.volume = 0f;
            next.Play();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool paused = game && game.State == GameState.Paused;
            bool slowed = game && game.State == GameState.Playing && game.PowerUps.IsActive(PowerUpType.SlowTime);
            float target = musicVolume * (paused ? pausedDuck : 1f);
            float fadeStep = musicVolume * dt / Mathf.Max(crossfadeTime, 0.01f);

            AudioSource current = music[activeMusic];
            if (opener != null && current.clip == opener && (!current.isPlaying || current.time >= opener.length - openerHandover))
                PlayMusic(game && game.State != GameState.Menu ? gameMusic : menuMusic);

            for (int i = 0; i < music.Length; i++)
            {
                AudioSource source = music[i];
                if (!source.isPlaying) continue;
                source.volume = Mathf.MoveTowards(source.volume, i == activeMusic ? target : 0f, fadeStep);
                source.pitch = Mathf.MoveTowards(source.pitch, slowed ? slowMotionPitch : 1f, dt * 0.5f);
                if (i != activeMusic && source.volume <= 0f) source.Stop();
            }
        }

        // ---------- Slices ----------

        void OnFoodSliced(FoodHitEvent e)
        {
            float now = Time.unscaledTime;
            strokeCount = now - lastSliceTime <= strokeWindow ? strokeCount + 1 : 0;
            lastSliceTime = now;
            if (now - lastSlicePlayTime < minSliceInterval) return;
            lastSlicePlayTime = now;

            float strokeRise = 1f + Mathf.Min(strokeCount * strokePitchStep, strokePitchMax);
            PlaySlice(sliceVolume, Random.Range(slicePitch.x, slicePitch.y) * strokeRise);
        }

        void OnGoldenHit(FoodHitEvent e) => PlaySlice(goldenHitVolume, goldenHitPitch * Random.Range(slicePitch.x, slicePitch.y));

        /// <summary>A single blade swish outside gameplay, e.g. the stroke across the name in the intro.</summary>
        public void PlayAccent(float pitch) => PlaySlice(sliceVolume, pitch);

        void PlaySlice(float volume, float pitch)
        {
            AudioClip clip = PickSliceClip();
            if (clip == null) return;
            // Slow motion lowers the sound a little too, so it matches what is on screen.
            if (Time.timeScale > 0f && Time.timeScale < 1f) pitch *= Mathf.Lerp(0.8f, 1f, Time.timeScale);

            AudioSource voice = sfx[nextVoice];
            nextVoice = (nextVoice + 1) % sfx.Length;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.Play();
        }

        AudioClip PickSliceClip()
        {
            int count = sliceClips.Length;
            if (count == 0) return null;
            int index = Random.Range(0, count);
            if (count > 1 && index == lastSliceClip) index = (index + 1 + Random.Range(0, count - 1)) % count;
            lastSliceClip = index;
            return sliceClips[index];
        }

        AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D: the game is flat on screen
            return source;
        }
    }
}
