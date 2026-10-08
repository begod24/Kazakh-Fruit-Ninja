using UnityEngine;
using UnityEngine.Rendering;

namespace KazakhNinja
{
    /// <summary>One half of a sliced food. Owns a reusable mesh the slicer writes into.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SlicedPiece : MonoBehaviour
    {
        const float MaxLifetime = 6f;

        MeshRenderer meshRenderer;
        SliceSystem owner;
        Vector3 center;
        Vector3 pivotOffset;
        Vector3 velocity;
        Vector3 angularVelocity;
        float gravity;
        float killY;
        float age;

        public Mesh Mesh { get; private set; }

        internal void Init(SliceSystem system)
        {
            owner = system;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            Mesh = new Mesh { name = "Sliced Piece" };
            Mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = Mesh;
        }

        /// <summary>Starts flying from where <paramref name="source"/> is; call after the slicer filled <see cref="Mesh"/>.</summary>
        internal void Launch(Transform source, Material[] materials, Vector3 launchVelocity, Vector3 spin,
            float gravityAcceleration, float despawnY)
        {
            transform.SetPositionAndRotation(source.position, source.rotation);
            transform.localScale = source.localScale;
            meshRenderer.sharedMaterials = materials;

            // Spin around the half's own centre rather than the whole food's pivot.
            Vector3 localCenter = Mesh.bounds.center;
            pivotOffset = Vector3.Scale(localCenter, source.localScale);
            center = source.TransformPoint(localCenter);

            velocity = launchVelocity;
            angularVelocity = spin;
            gravity = gravityAcceleration;
            killY = despawnY;
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            velocity.y -= gravity * dt;
            center += velocity * dt;

            Quaternion rotation = transform.rotation;
            float speed = angularVelocity.magnitude;
            if (speed > 0f) rotation = Quaternion.AngleAxis(speed * dt, angularVelocity / speed) * rotation;
            transform.SetPositionAndRotation(center - rotation * pivotOffset, rotation);

            if ((velocity.y < 0f && center.y < killY) || age > MaxLifetime) owner.Release(this);
        }

        void OnDestroy()
        {
            if (Mesh) Destroy(Mesh);
        }
    }
}
