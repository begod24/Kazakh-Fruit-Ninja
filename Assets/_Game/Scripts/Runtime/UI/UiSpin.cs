using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Slowly turns a UI element, e.g. the шаңырақ emblem.</summary>
    public sealed class UiSpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 8f;

        void Update() => transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
    }
}
