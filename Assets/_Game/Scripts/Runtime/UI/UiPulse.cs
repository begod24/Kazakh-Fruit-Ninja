using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Gentle breathing scale that draws the eye to a button.</summary>
    public sealed class UiPulse : MonoBehaviour
    {
        [SerializeField] float amount = 0.03f;
        [SerializeField] float speed = 2f;
        [SerializeField] float phase;

        void Update()
        {
            float s = 1f + Mathf.Sin((Time.unscaledTime + phase) * speed) * amount;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
