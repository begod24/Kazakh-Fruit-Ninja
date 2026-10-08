using System.Collections.Generic;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Follows the finger (or mouse), draws the trail and slices every food the blade
    /// sweeps over while it moves fast enough.
    /// </summary>
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class BladeController : MonoBehaviour
    {
        [SerializeField] Camera gameCamera;
        [SerializeField] SliceSystem sliceSystem;
        [SerializeField] TrailRenderer trail;
        [Tooltip("Minimum blade speed (world units per second) needed to cut.")]
        [SerializeField] float minCutSpeed = 5f;
        [SerializeField] float bladeRadius = 0.12f;
        [Tooltip("Depth of the trail; negative is in front of the food.")]
        [SerializeField] float trailDepth = -2f;

        Vector3 lastPoint;
        bool isSwiping;
        ParticleSystem emitter;
        bool emitting;
        readonly Dictionary<ParticleSystem, ParticleSystem> emitters = new();

        public bool IsCutting { get; private set; }
        /// <summary>The blade from the collection that is in use; null until one is applied.</summary>
        public BladeDefinition Skin { get; private set; }

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            if (!trail) trail = GetComponent<TrailRenderer>();
            trail.emitting = false;
        }

        void Update()
        {
            if (!PointerInput.TryGetPressedPosition(out Vector2 screenPosition))
            {
                if (isSwiping) EndSwipe();
                return;
            }

            Vector3 point = Playfield.ScreenToPlane(gameCamera, screenPosition);
            transform.position = Playfield.ScreenToPlane(gameCamera, screenPosition, trailDepth);
            if (!isSwiping)
            {
                BeginSwipe(point);
                return;
            }

            // Unscaled time: slow-motion bonuses must not make the blade feel sluggish.
            float speed = (point - lastPoint).magnitude / Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            IsCutting = speed >= minCutSpeed && Time.timeScale > 0f;
            SetEmitting(IsCutting);
            if (IsCutting) CutAlong(lastPoint, point);
            lastPoint = point;
        }

        /// <summary>Changes the trail's look and particles (a blade from the collection).</summary>
        public void ApplySkin(BladeDefinition skin)
        {
            if (!trail) trail = GetComponent<TrailRenderer>();
            Skin = skin;
            if (skin.trailMaterial) trail.sharedMaterial = skin.trailMaterial;
            trail.colorGradient = skin.trailColors;
            trail.widthMultiplier = skin.width;
            trail.time = skin.trailTime;

            SetEmitting(false);
            emitter = null;
            if (skin.emitterPrefab == null) return;
            if (!emitters.TryGetValue(skin.emitterPrefab, out emitter))
            {
                emitter = Instantiate(skin.emitterPrefab, transform, false);
                emitters.Add(skin.emitterPrefab, emitter);
            }
            ParticleSystem.EmissionModule emission = emitter.emission;
            emission.enabled = false;
            emitter.Play(true);
        }

        /// <summary>The blade's particles only stream while it is actually cutting.</summary>
        void SetEmitting(bool on)
        {
            if (emitter == null)
            {
                emitting = false;
                return;
            }
            if (emitting == on) return;
            emitting = on;
            ParticleSystem.EmissionModule emission = emitter.emission;
            emission.enabled = on;
        }

        void BeginSwipe(Vector3 point)
        {
            isSwiping = true;
            lastPoint = point;
            trail.Clear();
            trail.emitting = true;
        }

        void EndSwipe()
        {
            isSwiping = false;
            IsCutting = false;
            trail.emitting = false;
            SetEmitting(false);
        }

        void CutAlong(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 1e-5f) return;
            direction /= length;

            // Sweep the segment covered this frame, so fast swipes cannot skip food.
            IReadOnlyList<Sliceable> flying = Sliceable.Active;
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                if (i >= flying.Count) continue;
                Sliceable item = flying[i];
                Vector3 center = item.Center;
                Vector3 closest = from + direction * Mathf.Clamp(Vector3.Dot(center - from, direction), 0f, length);
                float reach = item.HitRadius + bladeRadius;
                float dx = center.x - closest.x, dy = center.y - closest.y;
                if (dx * dx + dy * dy <= reach * reach) sliceSystem.Hit(item, closest, direction);
            }
        }
    }
}
