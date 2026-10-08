using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// One scene of the backdrop's day (steppe at sunset → Алатау at dusk → Astana at night): a painted picture and
    /// the light that goes with it. The <see cref="BackgroundDirector"/> blends from one to the next.
    /// </summary>
    [CreateAssetMenu(fileName = "Background_", menuName = "Kazakh Ninja/Background")]
    public sealed class BackgroundDefinition : ScriptableObject
    {
        public string id;
        public string nameKk;
        public string nameRu;

        [Tooltip("The painting, any landscape shape: it is cropped to the screen around its middle.")]
        public Texture2D picture;

        [Header("Light on the food")]
        public Color lightColor = Color.white;
        [Min(0f)] public float lightIntensity = 1.2f;
        public Color ambientColor = new(0.5f, 0.46f, 0.42f);
        [Tooltip("Rim light on the food, so it stands out from this backdrop.")]
        public Color rimColor = new(1f, 0.8f, 0.55f);
        [Tooltip("What shiny food (coins, the golden aport) reflects from above.")]
        public Color reflectTop = new(0.6f, 0.55f, 0.5f);
        [Tooltip("What shiny food reflects from below.")]
        public Color reflectBottom = new(0.2f, 0.15f, 0.1f);

        [Header("Atmosphere")]
        [Tooltip("Looping particles in front of the backdrop: dust, snow, fireflies…")]
        public ParticleSystem ambientPrefab;
    }
}
