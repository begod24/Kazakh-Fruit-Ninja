using UnityEngine;
using UnityEngine.EventSystems;

namespace KazakhNinja
{
    /// <summary>Shrinks a button slightly while it is held down.</summary>
    public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float pressedScale = 0.94f;
        [Tooltip("Scaled element; defaults to this one. Use a child when this object also pulses.")]
        [SerializeField] RectTransform target;

        float current = 1f;
        float goal = 1f;

        void Awake()
        {
            if (!target) target = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData) => goal = pressedScale;
        public void OnPointerUp(PointerEventData eventData) => goal = 1f;
        public void OnPointerExit(PointerEventData eventData) => goal = 1f;

        void Update()
        {
            if (Mathf.Approximately(current, goal)) return;
            current = Mathf.MoveTowards(current, goal, Time.unscaledDeltaTime * 3f);
            target.localScale = new Vector3(current, current, 1f);
        }
    }
}
