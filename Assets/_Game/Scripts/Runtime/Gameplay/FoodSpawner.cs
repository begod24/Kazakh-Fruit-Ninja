using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace KazakhNinja
{
    /// <summary>
    /// Throws waves of food up from below the screen (and from the sides during Наурыз)
    /// and reports the ones that fall back uncut.
    /// </summary>
    public sealed class FoodSpawner : MonoBehaviour
    {
        /// <summary>How far past the screen edge a food starts and ends, beyond its own size.</summary>
        const float EdgeGap = 0.15f;

        [SerializeField] FoodDatabase database;
        [SerializeField] Sliceable foodPrefab;
        [SerializeField] Camera gameCamera;
        [SerializeField] bool spawnOnStart;

        [Header("Waves")]
        [Tooltip("Used until a game mode sets its own pacing.")]
        [SerializeField] SpawnPacing pacing = SpawnPacing.Default;
        [Tooltip("Chance that a whole wave is thrown at once instead of one after another.")]
        [SerializeField, Range(0f, 1f)] float burstChance = 0.35f;
        [SerializeField] Vector2 staggerDelay = new(0.08f, 0.3f);
        [Tooltip("Seconds between side throws while the Наурыз frenzy is on.")]
        [SerializeField] float frenzyInterval = 0.14f;

        [Header("Size")]
        [Tooltip("Every food flies, and is hit, this much bigger than its model: a phone screen is small, and so is a finger's aim.")]
        [SerializeField, Min(0.1f)] float sizeMultiplier = 1.4f;
        [Tooltip("Smallest blade hit radius of anything worth cutting (world units), so a little құрт is as easy to hit as an apple.")]
        [SerializeField, Min(0f)] float minHitRadius = 0.55f;
        [Tooltip("The pepper's hit radius relative to its size; below 1, a near miss is forgiven.")]
        [SerializeField, Range(0.5f, 1f)] float bombHitRadius01 = 0.8f;

        [Header("Trajectory")]
        [SerializeField] float gravity = 12f;
        [Tooltip("Keep launch points this fraction of the screen width away from the sides.")]
        [SerializeField, Range(0f, 0.5f)] float launchEdgeMargin01 = 0.15f;
        [Tooltip("Apex height as a fraction of the screen height (lowered for big food, so all of it stays on screen).")]
        [SerializeField] Vector2 apexHeight01 = new(0.55f, 0.92f);
        [Tooltip("How far from the centre (fraction of the screen width) the apex may be.")]
        [SerializeField, Range(0f, 0.5f)] float apexSpread01 = 0.25f;
        [Tooltip("At least this far below the screen food starts (deeper for big food, so it never pops into view).")]
        [SerializeField] float launchDepthBelowScreen = 0.9f;
        [SerializeField] float despawnDepthBelowScreen = 1.8f;
        [SerializeField] float maxTilt = 25f;
        [SerializeField] Vector2 spinSpeed = new(60f, 240f);

        [Header("Pool")]
        [Tooltip("Foods created up front, so none is instantiated in the middle of a round.")]
        [SerializeField, Min(0)] int prewarmCount = 24;

        struct PendingLaunch
        {
            public float time;
            /// <summary>Null means a random regular food.</summary>
            public FoodDefinition food;
        }

        ObjectPool<Sliceable> pool;
        readonly List<PendingLaunch> pendingLaunches = new();
        readonly Dictionary<GameObject, Stack<GameObject>> spareAttachments = new();
        float nextWaveTime;
        float nextFrenzyTime;
        bool frenzyFromLeft;

        public bool IsSpawning { get; set; }
        /// <summary>While true, extra food is thrown in from the sides.</summary>
        public bool IsFrenzy { get; set; }

        public SpawnPacing Pacing
        {
            get => pacing;
            set => pacing = value;
        }

        /// <summary>The pacing's limit on food in the air is reached: new throws wait for room.</summary>
        bool IsFull => pacing.maxInFlight > 0 && Sliceable.Active.Count >= pacing.maxInFlight;

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            pool = new ObjectPool<Sliceable>(CreateFood,
                actionOnRelease: food => food.gameObject.SetActive(false),
                actionOnDestroy: food => Destroy(food.gameObject),
                defaultCapacity: 16, maxSize: 64);
            Prewarm();
        }

        void Start()
        {
            if (spawnOnStart) IsSpawning = true;
            nextWaveTime = Time.time + 0.5f;
            StartCoroutine(WarmUpMaterials());
        }

        void Update()
        {
            // Queued throws go first; while some of them wait for room in the air, no new wave piles up behind them.
            bool waiting = LaunchDue();
            if (IsSpawning && !waiting && Time.time >= nextWaveTime && !IsFull)
            {
                ScheduleWave();
                nextWaveTime = Time.time + Random.Range(pacing.waveInterval.x, pacing.waveInterval.y);
            }

            if (IsFrenzy && Time.time >= nextFrenzyTime && !IsFull)
            {
                LaunchFromSide(frenzyFromLeft);
                frenzyFromLeft = !frenzyFromLeft;
                nextFrenzyTime = Time.time + frenzyInterval;
            }
        }

        /// <summary>Starts the next wave soon, e.g. at the beginning of a round.</summary>
        public void RestartWaves(float delay = 0.6f) => nextWaveTime = Time.time + delay;

        /// <summary>Throws the queued food whose time has come; returns true when some of it has to wait for room.</summary>
        bool LaunchDue()
        {
            for (int i = pendingLaunches.Count - 1; i >= 0; i--)
            {
                PendingLaunch pending = pendingLaunches[i];
                if (Time.time < pending.time) continue;
                if (IsFull) return true;
                pendingLaunches.RemoveAt(i);
                Launch(pending.food);
            }
            return false;
        }

        void ScheduleWave()
        {
            int count = Random.Range(pacing.foodPerWave.x, pacing.foodPerWave.y + 1);
            bool burst = Random.value < burstChance;
            float start = Time.time, time = start;
            for (int i = 0; i < count; i++)
            {
                pendingLaunches.Add(new PendingLaunch { time = time });
                if (!burst) time += Random.Range(staggerDelay.x, staggerDelay.y);
            }

            if (database == null) return;
            if (database.bomb != null && Random.value < pacing.bombChance)
                pendingLaunches.Add(new PendingLaunch { time = Random.Range(start, time), food = database.bomb });
            if (Random.value < pacing.powerUpChance)
            {
                FoodDefinition powerUp = database.PickPowerUp();
                if (powerUp != null) pendingLaunches.Add(new PendingLaunch { time = Random.Range(start, time), food = powerUp });
            }
            if (database.golden != null && Random.value < pacing.goldenChance && !IsInFlightOrPending(database.golden))
                pendingLaunches.Add(new PendingLaunch { time = time + 0.4f, food = database.golden });
        }

        bool IsInFlightOrPending(FoodDefinition food)
        {
            foreach (PendingLaunch pending in pendingLaunches)
                if (pending.food == food) return true;
            IReadOnlyList<Sliceable> flying = Sliceable.Active;
            for (int i = 0; i < flying.Count; i++)
                if (flying[i].Definition == food) return true;
            return false;
        }

        /// <summary>Throws one food up from below; a random regular one when <paramref name="food"/> is null.</summary>
        public Sliceable Launch(FoodDefinition food = null)
        {
            food = Resolve(food);
            if (food == null) return null;
            Rect view = Playfield.GetVisibleRect(gameCamera);
            (float half, float bound) = Reach(food);
            float margin = view.width * launchEdgeMargin01;
            var start = new Vector2(Random.Range(view.xMin + margin, view.xMax - margin),
                view.yMin - Mathf.Max(launchDepthBelowScreen, bound + EdgeGap));
            var apex = new Vector2(
                view.center.x + view.width * Random.Range(-apexSpread01, apexSpread01),
                Mathf.Min(view.yMin + view.height * Random.Range(apexHeight01.x, apexHeight01.y), view.yMax - half - EdgeGap));
            return LaunchAlong(food, start, apex, view, bound);
        }

        /// <summary>Throws a regular food in from the left or right edge (Наурыз frenzy).</summary>
        public Sliceable LaunchFromSide(bool fromLeft)
        {
            FoodDefinition food = Resolve(null);
            if (food == null) return null;
            Rect view = Playfield.GetVisibleRect(gameCamera);
            (float half, float bound) = Reach(food);
            float side = fromLeft ? -1f : 1f;
            var start = new Vector2(view.center.x + side * (view.width * 0.5f + Mathf.Max(1f, bound + EdgeGap)),
                view.yMin + view.height * Random.Range(0.1f, 0.45f));
            var apex = new Vector2(
                view.center.x - side * view.width * Random.Range(0f, 0.3f),
                Mathf.Min(view.yMin + view.height * Random.Range(0.6f, 0.9f), view.yMax - half - EdgeGap));
            return LaunchAlong(food, start, apex, view, bound);
        }

        /// <param name="bound">Radius around the whole food, so it disappears only once it is out of sight.</param>
        Sliceable LaunchAlong(FoodDefinition food, Vector2 start, Vector2 apex, Rect view, float bound)
        {
            float verticalSpeed = Mathf.Sqrt(2f * gravity * Mathf.Max(0.5f, apex.y - start.y));
            float timeToApex = verticalSpeed / gravity;
            var launchVelocity = new Vector3((apex.x - start.x) / timeToApex, verticalSpeed, 0f);

            // Mostly face the camera and spin in the screen plane, with a little 3D wobble.
            Quaternion rotation = Quaternion.Euler(Random.Range(-maxTilt, maxTilt), Random.Range(-maxTilt, maxTilt), Random.Range(0f, 360f));
            float spinDirection = Random.value < 0.5f ? -1f : 1f;
            var spin = new Vector3(Random.Range(-40f, 40f), Random.Range(-40f, 40f), Random.Range(spinSpeed.x, spinSpeed.y) * spinDirection);

            Sliceable item = pool.Get();
            item.Launch(this, food, new Vector3(start.x, start.y, 0f), rotation, food.scale * sizeMultiplier, HitRadius(food),
                launchVelocity, spin, gravity, view.yMin - Mathf.Max(despawnDepthBelowScreen, bound + EdgeGap));
            return item;
        }

        /// <summary><paramref name="food"/>, or a random regular food when it is null; null if there is nothing to throw.</summary>
        FoodDefinition Resolve(FoodDefinition food)
        {
            if (food == null && database != null) food = database.PickRandom();
            return food != null && food.mesh != null ? food : null;
        }

        /// <summary>Half the food's longest side and the radius around all of it, at the size it flies.</summary>
        (float half, float bound) Reach(FoodDefinition food)
        {
            Bounds bounds = food.mesh.bounds;
            Vector3 extents = Vector3.Scale(bounds.extents, food.scale) * sizeMultiplier;
            float offset = Vector3.Scale(bounds.center, food.scale).magnitude * sizeMultiplier;
            return (Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) + offset, extents.magnitude + offset);
        }

        /// <summary>How close the blade must pass: forgiving on small food, a little strict on the pepper.</summary>
        float HitRadius(FoodDefinition food)
        {
            float radius = food.hitRadius > 0f
                ? food.hitRadius * sizeMultiplier
                : EstimateHitRadius(food.mesh.bounds.extents, food.scale * sizeMultiplier);
            return food.kind == FoodKind.Bomb ? radius * bombHitRadius01 : Mathf.Max(radius, minHitRadius);
        }

        // Average of the two largest extents: generous on round food, fair on long шұжық.
        static float EstimateHitRadius(Vector3 extents, Vector3 scale)
        {
            Vector3 e = Vector3.Scale(extents, scale);
            float max = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            float min = Mathf.Min(e.x, Mathf.Min(e.y, e.z));
            float mid = e.x + e.y + e.z - max - min;
            return (max + mid) * 0.5f;
        }

        /// <summary>Removes every food in flight and cancels queued launches.</summary>
        public void Clear()
        {
            pendingLaunches.Clear();
            IReadOnlyList<Sliceable> flying = Sliceable.Active;
            for (int i = flying.Count - 1; i >= 0; i--) Release(flying[i]);
        }

        internal void HandleMissed(Sliceable item)
        {
            // Only regular food costs a life; letting the bomb, a bonus or the golden aport fall is fine.
            if (item.Definition.kind == FoodKind.Food)
                GameEvents.RaiseFoodMissed(new FoodHitEvent(item.Definition, item.Center, Vector3.zero));
            Release(item);
        }

        internal void Release(Sliceable item)
        {
            item.ReturnAttachment();
            pool.Release(item);
        }

        Sliceable CreateFood()
        {
            Sliceable food = Instantiate(foodPrefab, transform);
            food.gameObject.SetActive(false);
            return food;
        }

        // ---------- Attachments ----------

        /// <summary>The sparkles that fly with a special food (the pepper's fuse, the golden glitter…), from the pool.</summary>
        internal GameObject RentAttachment(GameObject prefab) =>
            spareAttachments.TryGetValue(prefab, out Stack<GameObject> spare) && spare.Count > 0
                ? spare.Pop()
                : Instantiate(prefab, transform, false);

        internal void ReturnAttachment(GameObject prefab, GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            if (!spareAttachments.TryGetValue(prefab, out Stack<GameObject> spare))
                spareAttachments.Add(prefab, spare = new Stack<GameObject>());
            spare.Push(instance);
        }

        // ---------- Warm-up ----------

        /// <summary>Creates the pooled foods and the special foods' sparkles now, so a round never waits on Instantiate.</summary>
        void Prewarm()
        {
            var created = new List<Sliceable>(prewarmCount);
            for (int i = 0; i < prewarmCount; i++) created.Add(pool.Get());
            foreach (Sliceable item in created) pool.Release(item);

            if (database == null) return;
            PrewarmAttachment(database.bomb, 3);
            PrewarmAttachment(database.golden, 1);
            if (database.powerUps != null)
                foreach (FoodDefinition powerUp in database.powerUps) PrewarmAttachment(powerUp, 2);
        }

        void PrewarmAttachment(FoodDefinition food, int count)
        {
            if (food == null || food.attachmentPrefab == null) return;
            for (int i = 0; i < count; i++) ReturnAttachment(food.attachmentPrefab, Instantiate(food.attachmentPrefab, transform, false));
        }

        /// <summary>
        /// Draws every food, whole and cut, for a couple of frames at startup: far too small to see, and behind the
        /// intro's curtain. Each material's GPU program is built then, instead of stalling a frame the first time
        /// the pepper, the golden aport or a cut half shows up.
        /// </summary>
        IEnumerator WarmUpMaterials()
        {
            if (database == null || gameCamera == null) yield break;
            var foods = new List<FoodDefinition>(database.foods) { database.bomb, database.golden };
            if (database.powerUps != null) foods.AddRange(database.powerUps);

            var draws = new List<(Mesh mesh, Material[] materials)>();
            var halves = new List<Mesh>();
            foreach (FoodDefinition food in foods)
            {
                if (food == null || food.mesh == null || food.skinMaterials == null || food.skinMaterials.Length == 0) continue;
                draws.Add((food.mesh, food.skinMaterials));
                // A cut half has a vertex layout of its own, and the cut face its own material.
                var front = new Mesh();
                var back = new Mesh();
                halves.Add(front);
                halves.Add(back);
                if (!MeshSlicer.Slice(food.mesh, new Plane(Vector3.right, food.mesh.bounds.center), front, back)) continue;
                var materials = new Material[front.subMeshCount];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = i < food.mesh.subMeshCount || food.insideMaterial == null
                        ? food.skinMaterials[Mathf.Min(i, food.skinMaterials.Length - 1)]
                        : food.insideMaterial;
                draws.Add((front, materials));
            }

            for (int frame = 0; frame < 2; frame++)
            {
                Transform eye = gameCamera.transform;
                Vector3 position = eye.position + eye.forward * 2f;
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * 0.001f);
                foreach ((Mesh mesh, Material[] materials) in draws)
                {
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        Material material = materials[Mathf.Min(submesh, materials.Length - 1)];
                        if (material == null) continue;
                        // Drawn like the pooled food (see the FoodItem prefab), so the same GPU state is prepared.
                        var parameters = new RenderParams(material)
                        {
                            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                            receiveShadows = false,
                            lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off,
                            reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off,
                            worldBounds = new Bounds(position, Vector3.one),
                        };
                        Graphics.RenderMesh(parameters, mesh, submesh, matrix);
                    }
                }
                yield return null;
            }
            foreach (Mesh half in halves) Destroy(half);
        }
    }
}
