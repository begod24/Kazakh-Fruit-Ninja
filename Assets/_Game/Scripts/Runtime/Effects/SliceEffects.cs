using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Turns <see cref="GameEvents"/> into effects: the flash along each cut, the food's own juice, crumbs or powder,
    /// the blade's burst, stains on the backdrop, the pepper's explosion, plus screen shake and hit-stop.
    /// Everything is emitted into the shared systems of one <see cref="FxLibrary"/>. Counts are kept modest:
    /// a stroke through a whole wave, or the pepper, must not cost the phone a frame.
    /// </summary>
    public sealed class SliceEffects : MonoBehaviour
    {
        [SerializeField] Camera gameCamera;
        [SerializeField] FxLibrary fxPrefab;
        [Tooltip("Source of the blade skin, whose flash and burst are added to every cut.")]
        [SerializeField] BladeController blade;

        [Header("Stains")]
        [Tooltip("Depth of the stains, just in front of the backdrop.")]
        [SerializeField] float stainDepth = 5.9f;
        [Tooltip("On-screen size range, measured on the gameplay plane.")]
        [SerializeField] Vector2 stainSize = new(1.3f, 2f);
        [Tooltip("Seconds a stain stays; it dries up over the last part.")]
        [SerializeField] float stainLifetime = 3.6f;
        [SerializeField] Color scorchColor = new(0.1f, 0.06f, 0.05f, 1f);

        [Header("Feel")]
        [Tooltip("Particles emitted in front of the gameplay plane, so the halves never hide them.")]
        [SerializeField] float frontOffset = 0.7f;
        [SerializeField] float slashLength = 3.2f;
        [Tooltip("Longest gap between slices of one stroke (seconds); from the third slice on each one shakes the screen a little.")]
        [SerializeField] float strokeWindow = 0.25f;

        static readonly Color GoldColor = new(1f, 0.82f, 0.3f);
        static readonly Color FlashFallback = new(1f, 0.92f, 0.7f);

        FxLibrary fx;
        int strokeCount;
        float lastSliceTime = float.NegativeInfinity;

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            fx = Instantiate(fxPrefab, transform);
            fx.name = "FX";
            ApplyDensity();
            // In the middle of the play area, where the camera sees it from the start of the intro on.
            fx.WarmUp(Vector3.back * frontOffset);
        }

        void OnEnable()
        {
            GameEvents.FoodSliced += OnFoodSliced;
            GameEvents.GoldenHit += OnGoldenHit;
            GameEvents.BombHit += OnBombHit;
            GameSettings.Changed += ApplyDensity;
        }

        void OnDisable()
        {
            GameEvents.FoodSliced -= OnFoodSliced;
            GameEvents.GoldenHit -= OnGoldenHit;
            GameEvents.BombHit -= OnBombHit;
            GameSettings.Changed -= ApplyDensity;
        }

        void ApplyDensity()
        {
            if (fx) fx.Density = GameSettings.HighGraphics ? 1f : 0.55f;
        }

        // ---------- Events ----------

        void OnFoodSliced(FoodHitEvent e)
        {
            FoodDefinition food = e.Food;
            Vector3 along = e.BladeDirection.sqrMagnitude > 0f ? e.BladeDirection.normalized : Vector3.right;
            Vector3 normal = new(-along.y, along.x, 0f);
            Vector3 front = e.Position + Vector3.back * frontOffset;
            BladeDefinition skin = blade ? blade.Skin : null;

            fx.Slash(front, along, skin ? skin.flashColor : FlashFallback, slashLength);
            FoodBurst(food, e.Position, front, along, normal);
            BladeBurst(skin, front, along);
            if (food.kind == FoodKind.PowerUp) fx.Ring(front, food.juiceColor2, 3.2f, 0.45f);

            if (food.kind == FoodKind.Golden)
            {
                // The golden aport finally splits: a shower of gold.
                fx.Glints(front, GoldColor, 8, 0.9f, 0.6f);
                fx.Festive(front, 0.7f);
                fx.Ring(front, GoldColor, 4f, 0.5f);
                CameraShake.Add(0.3f);
                HitStop.Trigger(0.08f);
            }
            else StrokeImpact();
        }

        void OnGoldenHit(FoodHitEvent e)
        {
            Vector3 front = e.Position + Vector3.back * frontOffset;
            Vector3 along = e.BladeDirection.sqrMagnitude > 0f ? e.BladeDirection.normalized : Vector3.right;
            BladeDefinition skin = blade ? blade.Skin : null;
            fx.Slash(front, along, skin ? skin.flashColor : GoldColor, slashLength * 0.7f);
            fx.Glints(front, GoldColor, 3, 0.6f, 0.45f);
            fx.Juice(front, along, new Vector3(-along.y, along.x, 0f), GoldColor, Color.white, 0.3f);
            fx.Ring(front, GoldColor, 1.6f, 0.25f);
            CameraShake.Add(0.08f);
        }

        void OnBombHit(FoodHitEvent e)
        {
            // Few, mid-sized puffs: big overlapping smoke and fire over half the screen is what stalls a phone's GPU.
            Vector3 front = e.Position + Vector3.back * frontOffset;
            var hot = new Color(1f, 0.55f, 0.15f);
            fx.Ring(front, hot, 4.5f, 0.4f);
            fx.Fire(front, 6, 2.6f);
            fx.Smoke(front, new Color(0.18f, 0.13f, 0.11f, 0.75f), 4, 1.4f);
            fx.Sparks(front, Vector3.up, new Color(1f, 0.8f, 0.4f), 16, 10f, 1f);
            fx.Embers(front, hot, 8, 2.5f);
            // Bits of pepper: red skin and green stalk.
            fx.Crumbs(front, Vector3.right, Vector3.up, new Color(0.85f, 0.1f, 0.06f), new Color(0.3f, 0.6f, 0.15f), 0.5f);
            AddStain(e.Position, Random.Range(0f, 360f), scorchColor, Random.Range(0, 5), 1.8f, 1f);
            CameraShake.Add(0.7f);
            HitStop.Trigger(0.12f);
        }

        // ---------- Pieces ----------

        /// <summary>What comes out of the food itself (in front of the halves), and the stain it leaves behind.</summary>
        void FoodBurst(FoodDefinition food, Vector3 position, Vector3 front, Vector3 along, Vector3 normal)
        {
            float angle = -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            switch (food.sliceStyle)
            {
                case SliceStyle.Crumbly:
                    fx.Crumbs(front, along, normal, food.juiceColor, food.juiceColor2);
                    AddStain(position, angle, Color.Lerp(food.juiceColor, food.juiceColor2, 0.25f), Random.Range(5, 7), 0.85f, 1f);
                    break;
                case SliceStyle.Powdery:
                    fx.Powder(front, food.juiceColor, food.juiceColor2);
                    AddStain(position, Random.Range(0f, 360f), food.juiceColor, 7, 1.1f, 1f);
                    break;
                case SliceStyle.Festive:
                    fx.Festive(front);
                    fx.Juice(front, along, normal, food.juiceColor, food.juiceColor2, 0.4f);
                    break;
                default:
                    fx.Juice(front, along, normal, food.juiceColor, food.juiceColor2);
                    // Splashes are thrown along the stroke, so the stain stretches that way.
                    AddStain(position, angle, food.juiceColor, Random.Range(0, 5), 1f, Random.Range(1.1f, 1.45f));
                    break;
            }
        }

        /// <summary>The blade's signature burst where it cut.</summary>
        void BladeBurst(BladeDefinition skin, Vector3 front, Vector3 along)
        {
            if (skin == null) return;
            Color color = skin.effectColor;
            switch (skin.cutEffect)
            {
                case BladeCutEffect.Sparks:
                    fx.Sparks(front, along, color, 8, 9f, 0.55f);
                    fx.Glints(front, Color.white, 1, 0f, 0.7f);
                    break;
                case BladeCutEffect.Wind:
                    fx.Sparks(front, along, color, 6, 6f, 0.25f);
                    fx.Ring(front, color, 2.4f, 0.3f);
                    break;
                case BladeCutEffect.Coins:
                    fx.Festive(front, 0.35f);
                    break;
                case BladeCutEffect.Ornaments:
                    fx.Ornaments(front, color, 5);
                    break;
                case BladeCutEffect.Lightning:
                    fx.Sparks(front, along, color, 9, 12f, 0.8f);
                    fx.Glints(front, color, 1, 0.3f, 0.6f);
                    break;
                case BladeCutEffect.Embers:
                    fx.Embers(front, color, 8, 2f);
                    fx.Fire(front, 1, 1f);
                    break;
                default:
                    fx.Glints(front, color, 2, 0.4f, 0.4f);
                    break;
            }
        }

        /// <summary>
        /// Slices that follow each other in one stroke shake the screen a little from the third on. No hit-stop here:
        /// one per slice turned a stroke through a wave into a string of stutters.
        /// </summary>
        void StrokeImpact()
        {
            float now = Time.unscaledTime;
            strokeCount = now - lastSliceTime <= strokeWindow ? strokeCount + 1 : 1;
            lastSliceTime = now;
            if (strokeCount >= ComboCounter.MinCombo) CameraShake.Add(0.12f);
        }

        // ---------- Stains ----------

        void AddStain(Vector3 position, float angle, Color color, int variant, float sizeMultiplier, float stretch)
        {
            // Project along the view ray, so the stain lands right behind the slice on screen.
            Vector3 eye = gameCamera.transform.position;
            Vector3 ray = position - eye;
            if (Mathf.Abs(ray.z) < 1e-4f) return;
            float distanceScale = (stainDepth - eye.z) / ray.z;
            float size = Random.Range(stainSize.x, stainSize.y) * sizeMultiplier * distanceScale;
            fx.Stain(eye + ray * distanceScale, angle, new Vector2(size * stretch, size), color, variant, stainLifetime);
        }
    }
}
