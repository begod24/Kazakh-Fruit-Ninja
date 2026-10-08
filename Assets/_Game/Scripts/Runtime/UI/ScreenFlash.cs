using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Full-screen flash, e.g. white on a bomb.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public sealed class ScreenFlash : MonoBehaviour
    {
        UnityEngine.UI.Image image;
        Color color;
        float intensity;
        float fadeSpeed;

        void Awake()
        {
            image = GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.enabled = false;
        }

        public void Flash(Color flashColor, float duration)
        {
            color = flashColor;
            intensity = 1f;
            fadeSpeed = 1f / Mathf.Max(duration, 0.01f);
            image.enabled = true;
        }

        void Update()
        {
            if (!image.enabled) return;
            intensity = Mathf.MoveTowards(intensity, 0f, fadeSpeed * Time.unscaledDeltaTime);
            Color c = color;
            c.a *= intensity * intensity;
            image.color = c;
            if (intensity <= 0f) image.enabled = false;
        }
    }
}
