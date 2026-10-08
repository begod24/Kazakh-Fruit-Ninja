using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// A fixed text from <see cref="UiText"/>, redrawn when the language changes. A title shows its big line and,
    /// if it has a second label, the small second-language line under it; with one language only the big line
    /// moves to the middle.
    /// </summary>
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [Tooltip("Key of a phrase in UiText.")]
        [SerializeField] string key;
        [SerializeField] TMP_Text main;
        [Tooltip("Optional small line under a title, in the second language.")]
        [SerializeField] TMP_Text sub;
        [Tooltip("Running text (labels, buttons under icons) rather than a title.")]
        [SerializeField] bool body;
        [Tooltip("Where the big line sits when there is no second line (anchored position).")]
        [SerializeField] Vector2 mainAlone;

        Vector2 mainWithSub;
        bool initialised;

        public string Key => key;

        void Init()
        {
            if (initialised || main == null) return;
            initialised = true;
            mainWithSub = main.rectTransform.anchoredPosition;
        }

        void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Loc.Changed -= Refresh;

        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        public void Refresh()
        {
            Phrase phrase = UiText.Find(key);
            if (phrase == null || main == null) return;
            Init();
            if (body)
            {
                main.SetText(phrase.Text);
                return;
            }
            main.SetText(phrase.Main);
            if (sub == null) return;
            string second = phrase.Sub;
            bool twoLines = !string.IsNullOrEmpty(second);
            sub.gameObject.SetActive(twoLines);
            if (twoLines) sub.SetText(second);
            main.rectTransform.anchoredPosition = twoLines ? mainWithSub : mainAlone;
        }
    }
}
