using UnityEngine;

namespace KazakhNinja
{
    /// <summary>A full-screen UI panel that fades in and out (in real time, so it works while paused).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiScreen : MonoBehaviour
    {
        [SerializeField] float fadeDuration = 0.2f;
        [Tooltip("Optional: grows from a little smaller as the screen fades in, so panels arrive rather than blink.")]
        [SerializeField] RectTransform pop;
        [SerializeField] float popFrom = 0.94f;

        CanvasGroup group;
        float targetAlpha;

        public bool IsVisible => targetAlpha > 0.5f;
        /// <summary>Fully faded in, so it can take gestures.</summary>
        public bool IsSettled => IsVisible && group.alpha >= 0.99f;

        protected virtual void Awake() => group = GetComponent<CanvasGroup>();

        public void SetVisible(bool visible, bool instant = false)
        {
            bool wasVisible = IsVisible;
            targetAlpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (instant)
            {
                group.alpha = targetAlpha;
                ApplyPop();
            }
            if (visible && !wasVisible) OnShow();
        }

        /// <summary>Called when the screen becomes visible; refresh its contents here.</summary>
        protected virtual void OnShow() { }

        protected virtual void Update()
        {
            if (Mathf.Approximately(group.alpha, targetAlpha)) return;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime / Mathf.Max(fadeDuration, 1e-3f));
            ApplyPop();
        }

        void ApplyPop()
        {
            if (pop == null) return;
            float t = 1f - (1f - group.alpha) * (1f - group.alpha);
            float scale = Mathf.Lerp(popFrom, 1f, t);
            pop.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
