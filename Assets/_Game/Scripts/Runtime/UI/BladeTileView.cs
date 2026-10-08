using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>One blade on the blades panel: its trail, name and either its state or the achievement that opens it.</summary>
    public sealed class BladeTileView : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Button button;
        [SerializeField] UnityEngine.UI.Image frame;
        [Tooltip("Shown around the chosen blade.")]
        [SerializeField] GameObject glow;
        [SerializeField] UnityEngine.UI.Image preview;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] GameObject checkIcon;
        [SerializeField] GameObject lockIcon;
        [Tooltip("Achievement progress; hidden once the blade is open.")]
        [SerializeField] GameObject progressGroup;
        [SerializeField] UnityEngine.UI.Image progressFill;
        [SerializeField] TMP_Text progressText;

        [Header("Style")]
        [SerializeField] Color selectedFrame = new(0.99f, 0.85f, 0.5f);
        [SerializeField] Color unlockedFrame = new(0.91f, 0.78f, 0.47f, 0.75f);
        [SerializeField] Color lockedFrame = new(0.91f, 0.78f, 0.47f, 0.25f);
        [SerializeField] Color lockedPreview = new(0.35f, 0.35f, 0.35f, 0.7f);
        [SerializeField] Color selectedStatus = new(0.99f, 0.85f, 0.5f);
        [SerializeField] Color otherStatus = new(1f, 0.96f, 0.88f, 0.8f);

        public UnityEngine.UI.Button Button => button;

        public void Bind(BladeDefinition blade, bool unlocked, bool selected, string requirement, int current, int goal)
        {
            nameText.SetText(UiText.Name(blade));
            preview.sprite = blade.preview;
            preview.color = unlocked ? Color.white : lockedPreview;
            frame.color = selected ? selectedFrame : unlocked ? unlockedFrame : lockedFrame;
            glow.SetActive(selected);
            checkIcon.SetActive(selected);
            lockIcon.SetActive(!unlocked);

            statusText.SetText(selected ? UiText.Selected.Text : unlocked ? UiText.Select.Text : requirement);
            statusText.color = selected ? selectedStatus : otherStatus;

            progressGroup.SetActive(!unlocked && goal > 0);
            if (!unlocked && goal > 0)
            {
                progressFill.fillAmount = Mathf.Clamp01(current / (float)goal);
                progressText.SetText(UiText.ProgressFormat.Format(Mathf.Min(current, goal), goal));
            }
            button.interactable = unlocked && !selected;
        }
    }
}
