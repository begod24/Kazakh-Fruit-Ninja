using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>A HUD pill showing how long a power-up has left.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PowerUpIndicator : MonoBehaviour
    {
        [SerializeField] PowerUpType type;
        [Tooltip("Horizontal filled image that empties as the power-up runs out.")]
        [SerializeField] UnityEngine.UI.Image fill;
        [SerializeField] TMP_Text label;

        CanvasGroup group;
        Language labelLanguage = (Language)(-1);

        public PowerUpType Type => type;

        public void Show(float fraction)
        {
            if (!group) group = GetComponent<CanvasGroup>();
            bool active = fraction > 0f;
            float alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            group.alpha = alpha;
            gameObject.SetActive(active || alpha > 0f);
            if (!active) return;
            fill.fillAmount = fraction;
            if (label && labelLanguage != Loc.Language)
            {
                labelLanguage = Loc.Language;
                label.SetText(UiText.PowerUpName(type));
            }
        }
    }
}
