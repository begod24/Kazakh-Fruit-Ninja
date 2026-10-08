using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>Pops short texts where things happen: combos, golden hits, penalties, lost lives, bonuses.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] Camera gameCamera;
        [Tooltip("Inactive template that gets cloned.")]
        [SerializeField] TMP_Text template;
        [SerializeField] ScreenFlash flash;
        [Tooltip("Optional: announces new collection cards and unlocks.")]
        [SerializeField] CollectionManager collection;

        [Header("Style")]
        [SerializeField] Color comboColor = new(0.996f, 0.773f, 0.047f);
        [SerializeField] Color goldColor = new(1f, 0.88f, 0.45f);
        [SerializeField] Color penaltyColor = new(1f, 0.35f, 0.3f);
        [SerializeField] Color powerUpColor = new(0.4f, 0.9f, 1f);
        [SerializeField] float rise = 110f;
        [Tooltip("Keep popups this far (canvas units) inside the screen edges.")]
        [SerializeField] float edgeMargin = 140f;

        struct Popup
        {
            public TMP_Text text;
            public Vector2 start;
            public float age;
            public float lifetime;
            public float size;
        }

        RectTransform area;
        readonly List<Popup> popups = new();
        readonly Stack<TMP_Text> pool = new();

        void Awake()
        {
            area = (RectTransform)transform;
            if (!gameCamera) gameCamera = Camera.main;
            template.gameObject.SetActive(false);
            PrepareGlyphs();
            // A few popups made up front, so the first combo does not wait on Instantiate.
            for (int i = 0; i < 4; i++) pool.Push(Instantiate(template, area));
        }

        /// <summary>
        /// The fonts are baked with every letter the game uses, but a letter missing from them comes from a dynamic
        /// fallback atlas, which draws it the first time it is shown and stalls that frame. Anything a popup can say,
        /// in every language, is checked now, at startup, instead of in the middle of a round.
        /// </summary>
        void PrepareGlyphs()
        {
            TMP_FontAsset font = template.font;
            if (font == null) return;
            var text = new StringBuilder("+-×0123456789");
            foreach (Phrase phrase in UiText.All) text.Append(phrase.En).Append(phrase.Kk).Append(phrase.Ru);
            if (collection && collection.Database)
            {
                foreach (FoodDefinition card in collection.Database.cards)
                    if (card) text.Append(card.nameKk).Append(card.nameRu).Append(card.nameEn);
                foreach (BladeDefinition blade in collection.Database.blades)
                    if (blade) text.Append(blade.nameKk).Append(blade.nameRu).Append(blade.nameEn);
            }
            for (int i = 0; i < text.Length; i++)
                if (!char.IsWhiteSpace(text[i])) font.HasCharacter(text[i], true, true);
        }

        void OnEnable()
        {
            game.Feedback += OnFeedback;
            if (collection)
            {
                collection.CardProgressed += OnCardProgressed;
                collection.BladeUnlocked += OnBladeUnlocked;
            }
        }

        void OnDisable()
        {
            game.Feedback -= OnFeedback;
            if (collection)
            {
                collection.CardProgressed -= OnCardProgressed;
                collection.BladeUnlocked -= OnBladeUnlocked;
            }
        }

        void OnCardProgressed(CardProgressEvent e) => Spawn(UiText.CardLevelUp(e.Level, UiText.Name(e.Food)), goldColor, e.Position, 80f, 1.8f);

        // Unlocks are not tied to a spot on screen: show them in the middle, a little high.
        void OnBladeUnlocked(BladeDefinition blade) =>
            Spawn(UiText.NewBladeFormat.Format(UiText.Name(blade)), powerUpColor, new Vector3(0f, 2f, 0f), 72f, 2.2f);

        void OnFeedback(GameFeedback f)
        {
            switch (f.Kind)
            {
                case FeedbackKind.Combo:
                    Spawn($"{UiText.ComboPraise(f.Value)}\n<size=55%>{UiText.ComboFormat.Format(f.Value, f.Extra)}</size>",
                        comboColor, f.Position, 96f, 1.3f);
                    break;
                case FeedbackKind.GoldenHit:
                    Spawn($"+{f.Value}", goldColor, f.Position, 60f, 0.6f);
                    break;
                case FeedbackKind.BombPenalty:
                    Spawn($"-{f.Value}", penaltyColor, f.Position, 90f, 1f);
                    if (flash) flash.Flash(new Color(1f, 0.55f, 0.3f, 0.6f), 0.4f);
                    break;
                case FeedbackKind.BombLifeLost:
                    Spawn(UiText.BombLifeLost.Text, penaltyColor, f.Position, 90f, 1.1f);
                    if (flash) flash.Flash(new Color(1f, 0.45f, 0.2f, 0.7f), 0.5f);
                    break;
                case FeedbackKind.BombGameOver:
                    if (flash) flash.Flash(Color.white, 1.1f);
                    break;
                case FeedbackKind.LifeLost:
                    Spawn(UiText.LostLife, penaltyColor, f.Position, 110f, 0.9f);
                    break;
                case FeedbackKind.PowerUp:
                    Spawn(UiText.PowerUpAnnouncement(f.PowerUp), powerUpColor, f.Position, 72f, 1.4f);
                    break;
            }
        }

        void Spawn(string message, Color color, Vector3 worldPosition, float size, float lifetime)
        {
            TMP_Text text = pool.Count > 0 ? pool.Pop() : Instantiate(template, area);
            text.gameObject.SetActive(true);
            text.SetText(message);
            text.color = color;
            text.fontSize = size;
            text.rectTransform.SetAsLastSibling();

            Vector2 screen = gameCamera.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local);
            Rect bounds = area.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + edgeMargin, bounds.xMax - edgeMargin);
            local.y = Mathf.Clamp(local.y, bounds.yMin + edgeMargin * 0.6f, bounds.yMax - edgeMargin);
            text.rectTransform.anchoredPosition = local;

            popups.Add(new Popup { text = text, start = local, age = 0f, lifetime = lifetime, size = size });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup p = popups[i];
                p.age += dt;
                float t = p.age / p.lifetime;
                if (t >= 1f)
                {
                    p.text.gameObject.SetActive(false);
                    pool.Push(p.text);
                    popups.RemoveAt(i);
                    continue;
                }

                // Pop in with a little overshoot, drift up, fade out at the end.
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.15f));
                p.text.rectTransform.localScale = new Vector3(pop, pop, 1f);
                p.text.rectTransform.anchoredPosition = p.start + Vector2.up * (rise * p.age);
                p.text.alpha = t > 0.65f ? 1f - (t - 0.65f) / 0.35f : 1f;
                popups[i] = p;
            }
        }
    }
}
