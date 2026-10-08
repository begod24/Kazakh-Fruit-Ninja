using UnityEngine;

namespace KazakhNinja
{
    public enum FoodKind
    {
        /// <summary>Sliced in two; falling uncut counts as a miss.</summary>
        Food,
        /// <summary>Explodes when hit (the chili-pepper bomb).</summary>
        Bomb,
        /// <summary>Hangs in the air and can be hit many times before it splits (golden aport).</summary>
        Golden,
        /// <summary>Sliced like food and triggers a bonus (Arcade).</summary>
        PowerUp,
    }

    /// <summary>What flies out of a food when it is cut, and the stain it leaves.</summary>
    public enum SliceStyle
    {
        /// <summary>Juice droplets and a glossy splash: fruit, meat, drinks, soup.</summary>
        Juicy,
        /// <summary>Crumbs and a crumb scatter: pastry, dried cheese, sweets.</summary>
        Crumbly,
        /// <summary>A puff of powder (құрт).</summary>
        Powdery,
        /// <summary>Coins, sweets and confetti (the Той feast).</summary>
        Festive,
    }

    /// <summary>
    /// Everything the game needs to know about one sliceable food.
    /// Swapping placeholder art for real art only means changing the fields here.
    /// </summary>
    [CreateAssetMenu(fileName = "Food_", menuName = "Kazakh Ninja/Food Definition")]
    public sealed class FoodDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string nameKk;
        public string nameRu;
        public string nameEn;
        [TextArea(2, 4)] public string factRu;
        [TextArea(2, 4)] public string factKk;
        [TextArea(2, 4)] public string factEn;

        [Header("Visuals")]
        [Tooltip("Must be Read/Write enabled so it can be sliced at runtime.")]
        public Mesh mesh;
        [Tooltip("One material per submesh of the mesh.")]
        public Material[] skinMaterials;
        [Tooltip("Material of the cut surface.")]
        public Material insideMaterial;
        public Vector3 scale = Vector3.one;
        [Tooltip("What flies out when it is cut.")]
        public SliceStyle sliceStyle;
        [Tooltip("Main colour of the juice / crumbs and of the stain they leave.")]
        public Color juiceColor = Color.white;
        [Tooltip("Second colour mixed in, e.g. apple peel or fat.")]
        public Color juiceColor2 = Color.white;
        [Tooltip("Optional child effect that flies with the food, e.g. the fuse sparks of the bomb.")]
        public GameObject attachmentPrefab;
        [Tooltip("Picture for the collection card; baked from the mesh by Kazakh Ninja/Bake Food Icons.")]
        public Sprite icon;

        [Header("Gameplay")]
        public FoodKind kind = FoodKind.Food;
        [Tooltip("Points per slice; for Golden, points per hit.")]
        [Min(0)] public int points = 1;
        [Min(0f)] public float spawnWeight = 1f;
        [Tooltip("Blade hit radius in world units; 0 = derived from the mesh bounds.")]
        [Min(0f)] public float hitRadius;

        [Header("Golden only")]
        [Tooltip("Hits after which the golden food splits.")]
        [Min(1)] public int maxHits = 10;
        [Tooltip("Seconds it hangs in the air after the first hit.")]
        [Min(0f)] public float holdDuration = 2f;

        [Header("Power-up only")]
        public PowerUpType powerUp;
        [Min(0f)] public float powerUpDuration = 5f;
    }
}
