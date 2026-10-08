using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace KazakhNinja
{
    /// <summary>
    /// Resolves blade hits: food splits into two flying halves, the bomb explodes,
    /// the golden aport hangs in the air collecting hits before it splits.
    /// </summary>
    public sealed class SliceSystem : MonoBehaviour
    {
        [SerializeField] Camera gameCamera;
        [Tooltip("Speed at which the halves move apart, world units per second.")]
        [SerializeField] float separationSpeed = 1.6f;
        [Tooltip("Degrees per second the halves turn to show their cut faces.")]
        [SerializeField] float openingSpin = 220f;
        [Tooltip("How far off-centre (fraction of the hit radius) the cut may land.")]
        [SerializeField, Range(0f, 1f)] float maxCutOffset01 = 0.5f;
        [SerializeField, Range(0f, 1f)] float inheritedSpin01 = 0.5f;

        [Header("Golden")]
        [Tooltip("Seconds between two counted hits on the same golden food.")]
        [SerializeField] float goldenHitCooldown = 0.07f;

        [Header("Pool")]
        [Tooltip("Halves created up front, so none is instantiated in the middle of a round.")]
        [SerializeField, Min(0)] int prewarmPieces = 32;

        struct HeldGolden
        {
            public Sliceable item;
            public float releaseTime;
        }

        ObjectPool<SlicedPiece> pool;
        readonly Dictionary<FoodDefinition, Material[]> pieceMaterials = new();
        readonly List<HeldGolden> heldGolden = new();

        void Awake()
        {
            if (!gameCamera) gameCamera = Camera.main;
            pool = new ObjectPool<SlicedPiece>(CreatePiece,
                actionOnRelease: piece => piece.gameObject.SetActive(false),
                actionOnDestroy: piece => Destroy(piece.gameObject),
                defaultCapacity: 16, maxSize: 64);

            var created = new List<SlicedPiece>(prewarmPieces);
            for (int i = 0; i < prewarmPieces; i++) created.Add(pool.Get());
            foreach (SlicedPiece piece in created) pool.Release(piece);
        }

        void Update()
        {
            for (int i = heldGolden.Count - 1; i >= 0; i--)
            {
                Sliceable item = heldGolden[i].item;
                if (!item.isActiveAndEnabled) heldGolden.RemoveAt(i); // cleared by the spawner
                else if (Time.time >= heldGolden[i].releaseTime)
                    FinishGolden(item, item.Center, Random.insideUnitCircle.normalized);
            }
        }

        /// <summary>The blade touched <paramref name="item"/> at <paramref name="bladePoint"/> moving along <paramref name="bladeDirection"/>.</summary>
        public void Hit(Sliceable item, Vector3 bladePoint, Vector3 bladeDirection)
        {
            switch (item.Definition.kind)
            {
                case FoodKind.Bomb:
                    GameEvents.RaiseBombHit(new FoodHitEvent(item.Definition, item.Center, ScreenDirection(bladeDirection)));
                    item.Despawn();
                    break;
                case FoodKind.Golden:
                    HitGolden(item, bladePoint, bladeDirection);
                    break;
                default:
                    Slice(item, bladePoint, bladeDirection);
                    break;
            }
        }

        /// <summary>Cuts <paramref name="item"/> in two along the blade. The item is always consumed.</summary>
        public void Slice(Sliceable item, Vector3 bladePoint, Vector3 bladeDirection)
        {
            Vector3 along = ScreenDirection(bladeDirection);
            // The cut plane contains the blade direction and the view direction.
            Vector3 cutNormal = Vector3.Cross(along, ViewDirection).normalized;

            Vector3 center = item.Center;
            float maxOffset = item.HitRadius * maxCutOffset01;
            float offset = Mathf.Clamp(Vector3.Dot(bladePoint - center, cutNormal), -maxOffset, maxOffset);

            SlicedPiece front = pool.Get();
            SlicedPiece back = pool.Get();
            bool cut = TryCut(item, center + cutNormal * offset, cutNormal, front, back)
                       || TryCut(item, center, cutNormal, front, back);
            if (cut)
            {
                Material[] materials = GetPieceMaterials(item.Definition, item.Mesh.subMeshCount);
                Vector3 inheritedSpin = item.AngularVelocity * inheritedSpin01;
                // Rotating the halves around the blade direction swings both cut faces toward the camera.
                front.Launch(item.transform, materials, item.Velocity + cutNormal * separationSpeed,
                    inheritedSpin - along * openingSpin, item.Gravity, item.KillY);
                back.Launch(item.transform, materials, item.Velocity - cutNormal * separationSpeed,
                    inheritedSpin + along * openingSpin, item.Gravity, item.KillY);
            }
            else
            {
                pool.Release(front);
                pool.Release(back);
            }

            GameEvents.RaiseFoodSliced(new FoodHitEvent(item.Definition, center, along, item.HitCount));
            item.Despawn();
        }

        internal void Release(SlicedPiece piece) => pool.Release(piece);

        void HitGolden(Sliceable item, Vector3 bladePoint, Vector3 bladeDirection)
        {
            if (!item.TryRegisterHit(goldenHitCooldown)) return;

            GameEvents.RaiseGoldenHit(new FoodHitEvent(item.Definition, item.Center, ScreenDirection(bladeDirection), item.HitCount));
            if (!item.IsHeld)
            {
                item.Hold();
                heldGolden.Add(new HeldGolden { item = item, releaseTime = Time.time + item.Definition.holdDuration });
            }
            if (item.HitCount >= item.Definition.maxHits) FinishGolden(item, bladePoint, bladeDirection);
        }

        void FinishGolden(Sliceable item, Vector3 bladePoint, Vector3 bladeDirection)
        {
            for (int i = heldGolden.Count - 1; i >= 0; i--)
                if (heldGolden[i].item == item) heldGolden.RemoveAt(i);
            Slice(item, bladePoint, bladeDirection);
        }

        Vector3 ViewDirection => gameCamera ? gameCamera.transform.forward : Vector3.forward;

        /// <summary>The blade direction flattened onto the screen plane.</summary>
        Vector3 ScreenDirection(Vector3 direction)
        {
            Vector3 view = ViewDirection;
            Vector3 flat = Vector3.ProjectOnPlane(direction, view);
            return flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.Cross(view, Vector3.up).normalized;
        }

        static bool TryCut(Sliceable item, Vector3 worldPoint, Vector3 worldNormal, SlicedPiece positive, SlicedPiece negative)
        {
            // Normals go to local space with the transpose of localToWorld, so non-uniform scale is handled.
            Transform t = item.transform;
            Vector3 localNormal = t.localToWorldMatrix.transpose.MultiplyVector(worldNormal);
            var plane = new Plane(localNormal, t.InverseTransformPoint(worldPoint));
            return MeshSlicer.Slice(item.Mesh, plane, positive.Mesh, negative.Mesh);
        }

        Material[] GetPieceMaterials(FoodDefinition food, int submeshCount)
        {
            if (pieceMaterials.TryGetValue(food, out Material[] materials) && materials.Length == submeshCount + 1)
                return materials;

            materials = new Material[submeshCount + 1];
            Material[] skins = food.skinMaterials;
            for (int i = 0; i < submeshCount; i++)
                materials[i] = skins != null && skins.Length > 0 ? skins[Mathf.Min(i, skins.Length - 1)] : null;
            materials[submeshCount] = food.insideMaterial != null ? food.insideMaterial : materials[0];
            pieceMaterials[food] = materials;
            return materials;
        }

        SlicedPiece CreatePiece()
        {
            var go = new GameObject("Piece", typeof(MeshFilter), typeof(MeshRenderer));
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var piece = go.AddComponent<SlicedPiece>();
            piece.Init(this);
            return piece;
        }
    }
}
