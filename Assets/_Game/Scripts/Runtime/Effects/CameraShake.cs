using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Trauma-based screen shake. Sits on the camera, which is a child of the camera rig, and only
    /// touches its local offset, so whatever moves the rig (the intro's dolly) is left alone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Largest offset, in world units, at full trauma.")]
        [SerializeField] float maxOffset = 0.28f;
        [Tooltip("Largest roll, in degrees, at full trauma.")]
        [SerializeField] float maxRoll = 1.4f;
        [SerializeField] float frequency = 22f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField] float recovery = 1.7f;

        static CameraShake current;

        float trauma;
        float seed;

        /// <summary>Adds trauma (0..1). The shake grows with its square, so small hits stay subtle.</summary>
        public static void Add(float amount)
        {
            if (current == null || !GameSettings.ScreenShake) return;
            current.trauma = Mathf.Clamp01(current.trauma + amount);
        }

        void Awake() => seed = Random.value * 100f;

        void OnEnable() => current = this;

        void OnDisable()
        {
            if (current == this) current = null;
            trauma = 0f;
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        void LateUpdate()
        {
            // Real time, so it keeps shaking through a hit-stop freeze.
            trauma = Mathf.MoveTowards(trauma, 0f, recovery * Time.unscaledDeltaTime);
            float shake = trauma * trauma;
            if (shake <= 0f)
            {
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }

            float t = Time.unscaledTime * frequency;
            var offset = new Vector3(Noise(t, 0), Noise(t, 1), 0f) * (maxOffset * shake);
            transform.SetLocalPositionAndRotation(offset, Quaternion.Euler(0f, 0f, Noise(t, 2) * maxRoll * shake));
        }

        float Noise(float t, int channel) => Mathf.PerlinNoise(seed + channel * 17.3f, t) * 2f - 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;
    }
}
