using System;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// The splash, played once per launch instead of an engine splash: the opening theme starts and the name shows
    /// big in the middle of a dark screen, a glint runs across it, then the camera pulls back — the name rises to the
    /// top of the main page, the world fades in behind it, "slice to play" appears in the middle and the round buttons
    /// come up from below. A tap skips to the pull-back.
    /// </summary>
    public sealed class IntroSequence : MonoBehaviour
    {
        [Serializable]
        public struct Reveal
        {
            public CanvasGroup target;
            [Tooltip("Where it slides in from, relative to its place (canvas units).")]
            public Vector2 offset;
            [Tooltip("Seconds after the pull-back starts.")]
            public float delay;
        }

        [SerializeField] GameManager game;
        [SerializeField] AudioManager audioManager;
        [Tooltip("Parent of the camera; pulled back along Z.")]
        [SerializeField] Transform cameraRig;
        [SerializeField] float dolly = 4f;

        [Header("Close-up")]
        [Tooltip("Dark cover over the world, under the name.")]
        [SerializeField] CanvasGroup curtain;
        [Tooltip("The name block; its pivot is its middle.")]
        [SerializeField] RectTransform titleBlock;
        [SerializeField] CanvasGroup titleGroup;
        [Tooltip("The name itself; its width decides how big the close-up can be.")]
        [SerializeField] TMPro.TMP_Text nameLabel;
        [Tooltip("A glint that runs across the name.")]
        [SerializeField] RectTransform shine;
        [SerializeField] float closeUpScale = 1.6f;
        [Tooltip("Widest the name may be in the close-up, as a share of the screen width.")]
        [SerializeField, Range(0.3f, 1f)] float closeUpMaxWidth = 0.84f;

        [Header("Main page")]
        [SerializeField] Reveal[] reveals = new Reveal[0];

        [Header("Timing (seconds)")]
        [SerializeField] float fadeInEnd = 1f;
        [SerializeField] float shineStart = 1.05f;
        [SerializeField] float shineDuration = 0.45f;
        [SerializeField] float pullStart = 1.9f;
        [SerializeField] float pullDuration = 1.1f;
        [SerializeField] float revealDuration = 0.55f;

        static bool played;

        /// <summary>The splash is on screen; the main page ignores swipes until it is done.</summary>
        public static bool IsPlaying { get; private set; }

        float time;
        bool running;
        bool wasPressed;
        bool shineSounded;
        Vector3 rigHome;
        Vector2 titleHome;
        Vector2 titleCentre;
        float closeUp;
        Vector2[] revealHome = new Vector2[0];
        float shineWidth;

        float End
        {
            get
            {
                float end = pullStart + pullDuration;
                foreach (Reveal reveal in reveals) end = Mathf.Max(end, pullStart + reveal.delay + revealDuration);
                return end;
            }
        }

        void Awake()
        {
            if (played) return;
            // No food behind the name; it starts flying once the main page is up.
            game.HoldAttract(true);
            running = true;
            IsPlaying = true;
        }

        void Start()
        {
            if (!running)
            {
                if (curtain) curtain.gameObject.SetActive(false);
                if (shine) shine.gameObject.SetActive(false);
                enabled = false;
                return;
            }
            played = true;
            if (audioManager) audioManager.PlayOpener();

            // Everything must be laid out before the resting places are read.
            Canvas.ForceUpdateCanvases();
            rigHome = cameraRig ? cameraRig.position : Vector3.zero;
            titleHome = titleBlock.anchoredPosition;
            revealHome = new Vector2[reveals.Length];
            for (int i = 0; i < reveals.Length; i++)
                if (reveals[i].target) revealHome[i] = ((RectTransform)reveals[i].target.transform).anchoredPosition;

            // Big, but never wider than the screen (iPad, narrow phones).
            Canvas canvas = titleBlock.GetComponentInParent<Canvas>();
            float canvasScale = canvas ? canvas.scaleFactor : 1f;
            float nameWidth = (nameLabel ? nameLabel.preferredWidth : titleBlock.rect.width) * canvasScale;
            closeUp = Mathf.Min(closeUpScale, Screen.width * closeUpMaxWidth / Mathf.Max(nameWidth, 1f));
            // The block scales about its middle, so putting its pivot on the screen's middle centres it.
            var screenCentre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, titleBlock.position.z);
            Vector3 shift = screenCentre - titleBlock.position;
            titleCentre = titleHome + (Vector2)titleBlock.parent.InverseTransformVector(shift);
            if (shine) shineWidth = nameLabel ? nameLabel.preferredWidth : titleBlock.rect.width;

            Apply(0f);
        }

        void Update()
        {
            if (!running) return;
            time += Time.unscaledDeltaTime;

            // A tap skips straight into the pull-back.
            bool pressed = PointerInput.TryGetPressedPosition(out _);
            if (pressed && !wasPressed && time > 0.3f && time < pullStart)
            {
                time = pullStart + pullDuration * 0.3f;
                shineSounded = true;
            }
            wasPressed = pressed;

            if (!shineSounded && time >= shineStart)
            {
                shineSounded = true;
                if (audioManager) audioManager.PlayAccent(0.9f);
            }

            Apply(time);
            if (time >= End) Finish();
        }

        void Apply(float t)
        {
            // 1. The name appears in the middle, pushing in slowly.
            float appear = Ease(Mathf.InverseLerp(0.15f, fadeInEnd, t));
            if (titleGroup) titleGroup.alpha = appear;

            // 2. A glint runs across it, left to right.
            float shineT = Mathf.InverseLerp(shineStart, shineStart + shineDuration, t);
            if (shine)
            {
                shine.gameObject.SetActive(shineT > 0f && shineT < 1f);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-0.6f, 0.6f, EaseInOut(shineT)) * shineWidth, 0f);
            }

            // 3. The camera pulls back: the name rises to the top, the world fades in.
            float pull = Ease(Mathf.InverseLerp(pullStart, pullStart + pullDuration, t));
            float scale = Mathf.Lerp(closeUp * (1.05f - 0.05f * appear), 1f, pull);
            titleBlock.localScale = new Vector3(scale, scale, 1f);
            titleBlock.anchoredPosition = Vector2.Lerp(titleCentre, titleHome, pull);
            if (curtain) curtain.alpha = 1f - pull;
            if (cameraRig) cameraRig.position = rigHome + Vector3.forward * (dolly * (1f - EaseOut(Mathf.InverseLerp(pullStart, pullStart + pullDuration * 1.1f, t))));

            // 4. "Slice to play" and the buttons come in.
            for (int i = 0; i < reveals.Length; i++)
            {
                Reveal reveal = reveals[i];
                if (reveal.target == null) continue;
                float r = EaseOut(Mathf.InverseLerp(pullStart + reveal.delay, pullStart + reveal.delay + revealDuration, t));
                reveal.target.alpha = r;
                // Nothing can be pressed before it is in place.
                reveal.target.blocksRaycasts = r >= 1f;
                ((RectTransform)reveal.target.transform).anchoredPosition = revealHome[i] + reveal.offset * (1f - r);
            }
        }

        void Finish()
        {
            running = false;
            IsPlaying = false;
            Apply(End);
            if (curtain) curtain.gameObject.SetActive(false);
            if (shine) shine.gameObject.SetActive(false);
            titleBlock.localScale = Vector3.one;
            titleBlock.anchoredPosition = titleHome;
            if (titleGroup) titleGroup.alpha = 1f;
            if (cameraRig) cameraRig.position = rigHome;
            game.HoldAttract(false);
            enabled = false;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        static float EaseInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            played = false;
            IsPlaying = false;
        }
    }
}
