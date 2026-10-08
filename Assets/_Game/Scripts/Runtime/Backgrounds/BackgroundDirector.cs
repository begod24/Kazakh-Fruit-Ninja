using System.Collections;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Runs the backdrop's day. In the menu the scenes follow each other slowly; in a round the day starts
    /// in the sunset steppe and turns to night as the round gets harder (the mode's background times). Each change
    /// is a soft dissolve in the Parallax Background shader, and the light, ambient and rim colours on the food
    /// and the floating particles change with it. The picture sways gently and drifts away from the finger.
    /// </summary>
    public sealed class BackgroundDirector : MonoBehaviour
    {
        static readonly int PictureA = Shader.PropertyToID("_PictureA");
        static readonly int PictureB = Shader.PropertyToID("_PictureB");
        static readonly int UvRectA = Shader.PropertyToID("_UvRectA");
        static readonly int UvRectB = Shader.PropertyToID("_UvRectB");
        static readonly int BlendId = Shader.PropertyToID("_Blend");
        static readonly int OffsetId = Shader.PropertyToID("_Offset");
        static readonly int RimColorId = Shader.PropertyToID("_KN_RimColor");
        static readonly int ReflectTopId = Shader.PropertyToID("_KN_ReflectTop");
        static readonly int ReflectBottomId = Shader.PropertyToID("_KN_ReflectBottom");
        const string TransitionKeyword = "_TRANSITION";

        [SerializeField] GameManager game;
        [SerializeField] Camera gameCamera;
        [Tooltip("A quad facing the camera; it is resized to cover the view.")]
        [SerializeField] MeshRenderer backdrop;
        [SerializeField] Light mainLight;
        [Tooltip("The day in order: a round starts with the first scene.")]
        [SerializeField] BackgroundDefinition[] scenes = new BackgroundDefinition[0];

        [Header("Timing (seconds)")]
        [SerializeField] float menuHold = 9f;
        [SerializeField] float transitionTime = 3f;
        [Tooltip("Back to the first scene when a round starts.")]
        [SerializeField] float roundStartTransition = 1.2f;

        [Header("Parallax")]
        [Tooltip("How far the picture sways, as a fraction of its width.")]
        [SerializeField] float sway = 0.008f;
        [SerializeField] float swayPeriod = 38f;
        [Tooltip("How far the picture drifts away from the finger, as a fraction of its width.")]
        [SerializeField] float fingerParallax = 0.016f;
        [Tooltip("Spare picture on each side it can sway into, as a fraction of its width (more than sway + finger).")]
        [SerializeField, Range(0.02f, 0.2f)] float margin = 0.03f;
        [Tooltip("How much bigger than the view the backdrop is, so camera shake never shows its edge.")]
        [SerializeField] float overscan = 1.08f;

        Material material;
        ParticleSystem[] ambient;
        int current;
        int next = -1;
        int pending = -1;
        float pendingDuration;
        float blend;
        float transitionSpeed;
        float holdTimer;
        float finger;
        float fingerTarget;
        Vector2Int fittedScreen;
        /// <summary>Width over height of the backdrop quad's view.</summary>
        float viewAspect = 16f / 9f;

        /// <summary>The scene the backdrop is at or heading for.</summary>
        int Target => pending >= 0 ? pending : next >= 0 ? next : current;

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            // A per-scene instance: its textures change as the day goes on.
            material = backdrop.material;
            ambient = new ParticleSystem[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] == null || scenes[i].ambientPrefab == null) continue;
                // Not under the backdrop: it is scaled to cover the view, and the particles are placed in world units.
                ambient[i] = Instantiate(scenes[i].ambientPrefab);
                ambient[i].name = scenes[i].ambientPrefab.name;
                ambient[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Fit();
            holdTimer = menuHold;
            if (scenes.Length == 0) return;
            Show(0);
            // The first frame (under the intro's curtain) draws the transition variant, showing the first scene on
            // both sides: its GPU program is built now instead of stalling the first change of scene.
            SetPicture(scenes[0], PictureB, UvRectB);
            material.EnableKeyword(TransitionKeyword);
        }

        IEnumerator Start()
        {
            yield return null;
            if (next < 0) material.DisableKeyword(TransitionKeyword);
        }

        void OnEnable() => game.StateChanged += OnStateChanged;
        void OnDisable() => game.StateChanged -= OnStateChanged;

        void OnDestroy()
        {
            if (material) Destroy(material);
        }

        void OnStateChanged(GameState state)
        {
            // A new round (not a resume): morning again.
            if (state == GameState.Playing && game.Session != null && game.Session.Elapsed <= 0f)
                Request(0, roundStartTransition);
            if (state == GameState.Menu) holdTimer = menuHold;
        }

        void Update()
        {
            if (scenes.Length == 0) return;
            float dt = Time.unscaledDeltaTime;
            if (fittedScreen.x != Screen.width || fittedScreen.y != Screen.height) Fit();
            Schedule(dt);
            Advance(dt);
            Sway(dt);
        }

        // ---------- The day ----------

        void Schedule(float dt)
        {
            switch (game.State)
            {
                case GameState.Menu:
                    if (next < 0 && pending < 0 && (holdTimer -= dt) <= 0f)
                    {
                        Request((Target + 1) % scenes.Length, transitionTime);
                        holdTimer = menuHold + transitionTime;
                    }
                    break;
                case GameState.Playing when game.Mode != null && game.Session != null:
                    Request(Mathf.Min(game.Mode.BackgroundAt(game.Session.Elapsed), scenes.Length - 1), transitionTime);
                    break;
            }
        }

        /// <summary>Queues a change; one already under way finishes first (quickly), so nothing ever pops.</summary>
        void Request(int index, float duration)
        {
            if (index < 0 || index >= scenes.Length || index == Target) return;
            pending = index;
            pendingDuration = duration;
        }

        void Advance(float dt)
        {
            if (game.State == GameState.Paused) return;
            if (next < 0)
            {
                if (pending < 0) return;
                Begin(pending, pendingDuration);
                pending = -1;
            }
            if (next < 0) return;

            float speed = pending >= 0 ? Mathf.Max(transitionSpeed, 2.5f) : transitionSpeed;
            blend = Mathf.MoveTowards(blend, 1f, speed * dt);
            float eased = Mathf.SmoothStep(0f, 1f, blend);
            material.SetFloat(BlendId, eased);
            ApplyLight(scenes[current], scenes[next], eased);
            if (blend >= 1f) Show(next);
        }

        void Begin(int index, float duration)
        {
            if (index == current) return;
            next = index;
            blend = 0f;
            transitionSpeed = 1f / Mathf.Max(duration, 0.01f);
            SetPicture(scenes[next], PictureB, UvRectB);
            material.SetFloat(BlendId, 0f);
            material.EnableKeyword(TransitionKeyword);
            if (ambient[current]) ambient[current].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (ambient[next]) ambient[next].Play(true);
        }

        /// <summary>Settles on <paramref name="index"/> with no transition running.</summary>
        void Show(int index)
        {
            current = index;
            next = -1;
            blend = 0f;
            SetPicture(scenes[current], PictureA, UvRectA);
            material.SetFloat(BlendId, 0f);
            material.DisableKeyword(TransitionKeyword);
            ApplyLight(scenes[current], scenes[current], 0f);
            for (int i = 0; i < ambient.Length; i++)
            {
                if (ambient[i] == null) continue;
                if (i == current && !ambient[i].isEmitting) ambient[i].Play(true);
                else if (i != current && ambient[i].isEmitting) ambient[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void SetPicture(BackgroundDefinition scene, int picture, int uvRect)
        {
            material.SetTexture(picture, scene.picture);
            material.SetVector(uvRect, Window(scene.picture));
        }

        void ApplyLight(BackgroundDefinition from, BackgroundDefinition to, float t)
        {
            if (mainLight)
            {
                mainLight.color = Color.Lerp(from.lightColor, to.lightColor, t);
                mainLight.intensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
            }
            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, t);
            Shader.SetGlobalColor(RimColorId, Color.Lerp(from.rimColor, to.rimColor, t));
            Shader.SetGlobalColor(ReflectTopId, Color.Lerp(from.reflectTop, to.reflectTop, t));
            Shader.SetGlobalColor(ReflectBottomId, Color.Lerp(from.reflectBottom, to.reflectBottom, t));
        }

        // ---------- Framing and parallax ----------

        /// <summary>Sizes the quad to cover the view (plus overscan) and crops the pictures to its shape.</summary>
        void Fit()
        {
            fittedScreen = new Vector2Int(Screen.width, Screen.height);
            Transform quad = backdrop.transform;
            Rect view = Playfield.GetVisibleRect(gameCamera, quad.position.z);
            quad.position = new Vector3(view.center.x, view.center.y, quad.position.z);
            quad.localScale = new Vector3(view.width * overscan, view.height * overscan, 1f);
            viewAspect = view.width / view.height;
            if (scenes.Length == 0) return;
            material.SetVector(UvRectA, Window(scenes[current].picture));
            if (next >= 0) material.SetVector(UvRectB, Window(scenes[next].picture));
        }

        /// <summary>
        /// The part of <paramref name="picture"/> the quad shows (scale xy, offset zw): its width minus the margins
        /// the sway moves into, cropped top and bottom around the middle.
        /// </summary>
        Vector4 Window(Texture picture)
        {
            float pictureAspect = picture ? (float)picture.width / Mathf.Max(picture.height, 1) : 2f;
            float u = 1f - 2f * margin;
            float v = u * pictureAspect / viewAspect;
            const float verticalRoom = 0.97f;
            if (v > verticalRoom)
            {
                // Taller screens (iPad): fill the height and crop the sides instead.
                v = verticalRoom;
                u = v * viewAspect / pictureAspect;
            }
            // The quad reaches past the view by the overscan, and shows that much more of the picture.
            u *= overscan;
            v *= overscan;
            return new Vector4(u, v, (1f - u) * 0.5f, (1f - v) * 0.5f);
        }

        void Sway(float dt)
        {
            if (PointerInput.TryGetPressedPosition(out Vector2 pointer) && Screen.width > 0)
                fingerTarget = pointer.x / Screen.width * 2f - 1f;
            else
                fingerTarget = Mathf.MoveTowards(fingerTarget, 0f, 0.4f * dt);
            finger = Mathf.Lerp(finger, fingerTarget, 1f - Mathf.Exp(-2.5f * dt));

            float phase = Time.unscaledTime * (2f * Mathf.PI / Mathf.Max(swayPeriod, 1f));
            material.SetVector(OffsetId, new Vector4(
                sway * Mathf.Sin(phase) - finger * fingerParallax,
                0.004f * Mathf.Sin(phase * 1.37f + 1.1f)));
        }
    }
}
