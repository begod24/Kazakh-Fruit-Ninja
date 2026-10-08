using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The burst a blade leaves where it cuts, on top of the food's own juice or crumbs.</summary>
    public enum BladeCutEffect
    {
        Glints,
        /// <summary>Hot metal sparks (steel).</summary>
        Sparks,
        /// <summary>Streaks of wind.</summary>
        Wind,
        /// <summary>Coins and sweets (шашу).</summary>
        Coins,
        /// <summary>Glowing қошқар мүйіз ornaments.</summary>
        Ornaments,
        Lightning,
        /// <summary>Flying embers (fire).</summary>
        Embers,
    }

    /// <summary>A blade skin from the collection: how the swipe trail and the cuts look.</summary>
    [CreateAssetMenu(fileName = "Blade_", menuName = "Kazakh Ninja/Blade")]
    public sealed class BladeDefinition : ScriptableObject
    {
        public string id;
        public string nameKk;
        public string nameRu;
        public string nameEn;

        [Header("Trail")]
        [Tooltip("A Kazakh Ninja/Blade Trail material; its style draws the pattern.")]
        public Material trailMaterial;
        [Tooltip("From the blade tip (left) to the end of the trail (right).")]
        public Gradient trailColors = new();
        [Min(0.01f)] public float width = 0.36f;
        [Min(0.01f)] public float trailTime = 0.15f;
        [Tooltip("Optional particles that stream off the blade while it cuts.")]
        public ParticleSystem emitterPrefab;

        [Header("Cut")]
        public BladeCutEffect cutEffect;
        [Tooltip("Colour of the flash along each cut.")]
        public Color flashColor = new(1f, 0.9f, 0.6f);
        [Tooltip("Colour of the cut burst.")]
        public Color effectColor = Color.white;

        [Header("Collection")]
        [Tooltip("Swatch shown in the collection screen.")]
        public Sprite preview;
        public UnlockRequirement unlock;
    }
}
