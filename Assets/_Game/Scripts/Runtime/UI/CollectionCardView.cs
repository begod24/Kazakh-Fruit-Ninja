using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>One dish in the records panel: icon, name, how many were sliced and the card level.</summary>
    public sealed class CollectionCardView : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Button button;
        [SerializeField] UnityEngine.UI.Image frame;
        [SerializeField] UnityEngine.UI.Image icon;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text countText;
        [SerializeField] UnityEngine.UI.Image progressFill;
        [Tooltip("One per card level, left to right.")]
        [SerializeField] UnityEngine.UI.Image[] pips;
        [SerializeField] GameObject selection;

        [Header("Style")]
        [Tooltip("Frame colour per level: locked, then each level.")]
        [SerializeField] Color[] levelColors =
        {
            new(0.91f, 0.78f, 0.47f, 0.3f), new(0.8f, 0.5f, 0.25f), new(0.82f, 0.86f, 0.92f), new(0.99f, 0.85f, 0.5f),
        };
        [SerializeField] Color lockedIcon = new(0f, 0f, 0f, 0.55f);
        [SerializeField] Color lockedText = new(1f, 0.957f, 0.863f, 0.5f);
        [SerializeField] Color unlockedText = new(1f, 0.957f, 0.863f);
        [SerializeField] Color emptyPip = new(1f, 1f, 1f, 0.15f);

        Sprite fallbackIcon;

        public FoodDefinition Food { get; private set; }
        public UnityEngine.UI.Button Button => button;

        void Awake() => fallbackIcon = icon.sprite;

        public void Bind(FoodDefinition food, int count, int level, int nextThreshold, bool selected)
        {
            if (fallbackIcon == null) fallbackIcon = icon.sprite;
            Food = food;
            bool unlocked = level > 0;

            frame.color = levelColors[Mathf.Clamp(level, 0, levelColors.Length - 1)];
            icon.sprite = food.icon != null ? food.icon : fallbackIcon;
            // Locked dishes show as a dark silhouette: a hint of what to slice.
            icon.color = unlocked ? (food.icon != null ? Color.white : food.juiceColor) : lockedIcon;
            nameText.SetText(UiText.Name(food));
            nameText.color = unlocked ? unlockedText : lockedText;

            countText.SetText("{0}", count);
            progressFill.fillAmount = nextThreshold < 0 ? 1f : Mathf.Clamp01(count / (float)nextThreshold);

            for (int i = 0; i < pips.Length; i++)
                pips[i].color = i < level ? levelColors[Mathf.Min(i + 1, levelColors.Length - 1)] : emptyPip;
            selection.SetActive(selected);
        }
    }
}
