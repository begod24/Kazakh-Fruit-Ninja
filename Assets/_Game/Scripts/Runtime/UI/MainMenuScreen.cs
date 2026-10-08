using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KazakhNinja
{
    /// <summary>
    /// The main page: the name, "slice to play" and the round buttons (blades, records, settings).
    /// A swipe anywhere but on a button opens the mode choice.
    /// </summary>
    public sealed class MainMenuScreen : UiScreen
    {
        [SerializeField] UiRoot root;
        [SerializeField] AudioManager audioManager;
        [SerializeField] UnityEngine.UI.Button bladesButton;
        [SerializeField] UnityEngine.UI.Button recordsButton;
        [SerializeField] UnityEngine.UI.Button settingsButton;

        [Header("Slice to play")]
        [Tooltip("Breathes gently while the page waits for a swipe.")]
        [SerializeField] CanvasGroup prompt;
        [Tooltip("A swipe this long, as a share of the screen height, opens the mode choice.")]
        [SerializeField, Range(0.03f, 0.5f)] float swipeLength = 0.12f;

        readonly List<RaycastResult> hits = new();
        PointerEventData pointerData;
        bool wasPressed;
        bool tracking;
        Vector2 lastPosition;
        float travelled;

        protected override void Awake()
        {
            base.Awake();
            bladesButton.onClick.AddListener(() => root.ShowPage(MenuPage.Blades));
            recordsButton.onClick.AddListener(() => root.ShowPage(MenuPage.Records));
            settingsButton.onClick.AddListener(() => root.ShowSettings(true));
        }

        protected override void OnShow()
        {
            // A finger still down from the previous page must lift before it can swipe here.
            tracking = false;
            wasPressed = PointerInput.TryGetPressedPosition(out _);
        }

        protected override void Update()
        {
            base.Update();
            if (prompt)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
                prompt.alpha = Mathf.Lerp(0.72f, 1f, breath);
            }
            if (!IsSettled || IntroSequence.IsPlaying)
            {
                tracking = false;
                wasPressed = PointerInput.TryGetPressedPosition(out _);
                return;
            }
            DetectSwipe();
        }

        void DetectSwipe()
        {
            bool pressed = PointerInput.TryGetPressedPosition(out Vector2 position);
            if (pressed && !wasPressed)
            {
                tracking = !IsOverButton(position);
                lastPosition = position;
                travelled = 0f;
            }
            else if (pressed && tracking)
            {
                travelled += (position - lastPosition).magnitude;
                lastPosition = position;
                if (travelled >= Screen.height * swipeLength)
                {
                    tracking = false;
                    if (audioManager) audioManager.PlayAccent(1.1f);
                    root.ShowPage(MenuPage.Modes);
                }
            }
            if (!pressed) tracking = false;
            wasPressed = pressed;
        }

        bool IsOverButton(Vector2 position)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            pointerData ??= new PointerEventData(events);
            pointerData.position = position;
            hits.Clear();
            events.RaycastAll(pointerData, hits);
            foreach (RaycastResult hit in hits)
                if (hit.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
            return false;
        }
    }
}
