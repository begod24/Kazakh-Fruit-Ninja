using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Keeps its RectTransform inside Screen.safeArea, clear of notches and rounded corners.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int appliedScreen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || appliedScreen.x != Screen.width || appliedScreen.y != Screen.height) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rect.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
