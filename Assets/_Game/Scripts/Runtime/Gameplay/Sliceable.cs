using System.Collections.Generic;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// A whole food flying across the screen. Pooled by <see cref="FoodSpawner"/>; moves ballistically
    /// on its own (no physics engine), so it stays in sync with Time.timeScale slow-motion.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Sliceable : MonoBehaviour
    {
        const float HitPunchScale = 0.18f;
        const float HitPunchRecovery = 6f;
        const float HeldDamping = 4f;

        static readonly List<Sliceable> active = new();

        /// <summary>Every food currently in flight.</summary>
        public static IReadOnlyList<Sliceable> Active => active;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        FoodSpawner owner;
        GameObject attachment;
        GameObject attachmentPrefab;
        Vector3 localCenter;
        Vector3 baseScale;
        Vector3 velocity;
        Vector3 angularVelocity;
        float gravity;
        float killY;
        float nextHitTime;
        float punch;

        public FoodDefinition Definition { get; private set; }
        public float HitRadius { get; private set; }
        public Mesh Mesh => meshFilter.sharedMesh;
        public Vector3 Center => transform.TransformPoint(localCenter);
        public Vector3 Velocity => velocity;
        /// <summary>World-space spin axis scaled by degrees per second.</summary>
        public Vector3 AngularVelocity => angularVelocity;
        public float Gravity => gravity;
        public float KillY => killY;
        /// <summary>A golden food that was hit hangs in the air instead of falling.</summary>
        public bool IsHeld { get; private set; }
        public int HitCount { get; private set; }

        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        /// <param name="scale">The size it flies at (the spawner makes food bigger than its model).</param>
        /// <param name="hitRadius">How close to its centre the blade must pass to cut it.</param>
        internal void Launch(FoodSpawner spawner, FoodDefinition food, Vector3 position, Quaternion rotation, Vector3 scale,
            float hitRadius, Vector3 launchVelocity, Vector3 spin, float gravityAcceleration, float despawnY)
        {
            owner = spawner;
            Definition = food;
            meshFilter.sharedMesh = food.mesh;
            meshRenderer.sharedMaterials = food.skinMaterials;
            baseScale = scale;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = baseScale;
            localCenter = food.mesh.bounds.center;
            HitRadius = hitRadius;

            velocity = launchVelocity;
            angularVelocity = spin;
            gravity = gravityAcceleration;
            killY = despawnY;
            IsHeld = false;
            HitCount = 0;
            nextHitTime = 0f;
            punch = 0f;

            gameObject.SetActive(true);
            SetAttachment(food.attachmentPrefab);
        }

        internal void Despawn() => owner.Release(this);

        /// <summary>Counts a hit unless the previous one was less than <paramref name="cooldown"/> seconds ago.</summary>
        internal bool TryRegisterHit(float cooldown)
        {
            if (Time.time < nextHitTime) return false;
            nextHitTime = Time.time + cooldown;
            HitCount++;
            punch = 1f;
            angularVelocity += Random.onUnitSphere * 90f;
            return true;
        }

        /// <summary>Stops the food in mid-air, e.g. the golden aport after its first hit.</summary>
        public void Hold() => IsHeld = true;

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsHeld) velocity *= Mathf.Exp(-HeldDamping * dt);
            else velocity.y -= gravity * dt;
            transform.position += velocity * dt;

            float speed = angularVelocity.magnitude;
            if (speed > 0f)
                transform.rotation = Quaternion.AngleAxis(speed * dt, angularVelocity / speed) * transform.rotation;

            if (punch > 0f)
            {
                punch = Mathf.MoveTowards(punch, 0f, HitPunchRecovery * dt);
                transform.localScale = baseScale * (1f + HitPunchScale * punch);
            }

            if (velocity.y < 0f && transform.position.y < killY) owner.HandleMissed(this);
        }

        /// <summary>Borrows the special food's sparkles from the spawner's pool and puts them on this food.</summary>
        void SetAttachment(GameObject prefab)
        {
            ReturnAttachment();
            if (prefab == null) return;

            attachmentPrefab = prefab;
            attachment = owner.RentAttachment(prefab);
            attachment.transform.SetParent(transform, false);
            attachment.SetActive(true);
            // World-space particles would otherwise streak from where the pooled effect last flew.
            if (attachment.TryGetComponent(out ParticleSystem particles))
            {
                particles.Clear(true);
                particles.Play(true);
            }
        }

        /// <summary>Hands the sparkles back to the spawner; called before this food goes back to its pool.</summary>
        internal void ReturnAttachment()
        {
            if (attachment == null) return;
            owner.ReturnAttachment(attachmentPrefab, attachment);
            attachment = null;
            attachmentPrefab = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();
    }
}
