using System;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The language choice in the settings. Stored as a number in the save: only add new values at the end.</summary>
    public enum Language
    {
        /// <summary>The default: titles in English with Kazakh under them, the rest of the text in Kazakh.</summary>
        EnglishKazakh,
        English,
        Kazakh,
        Russian,
    }

    /// <summary>A player-facing text in the three languages; see <see cref="Loc"/> for which one shows where.</summary>
    public sealed class Phrase
    {
        public readonly string Key;
        public readonly string En;
        public readonly string Kk;
        public readonly string Ru;

        public Phrase(string key, string en, string kk, string ru)
        {
            Key = key;
            En = en;
            Kk = kk;
            Ru = ru;
        }

        /// <summary>A title's big line.</summary>
        public string Main => Loc.Main(En, Kk, Ru);
        /// <summary>A title's small second line, or null when only one language is shown.</summary>
        public string Sub => Loc.Sub(En, Kk, Ru);
        /// <summary>Running text: labels, stats, messages.</summary>
        public string Text => Loc.Text(En, Kk, Ru);

        public string Format(object arg) => string.Format(Text, arg);
        public string Format(object arg0, object arg1) => string.Format(Text, arg0, arg1);

        public override string ToString() => Text;
    }

    /// <summary>
    /// Which language each kind of text is shown in. Titles have a big line and, in the bilingual default,
    /// a small Kazakh line under it (like the menu mockups); running text uses one language.
    /// Bound to the save by the <see cref="GameManager"/>; whoever changes the language saves the file.
    /// </summary>
    public static class Loc
    {
        static SaveData save;

        /// <summary>The language changed (or a save was bound); visible texts refresh.</summary>
        public static event Action Changed;

        public static void Bind(SaveData data)
        {
            save = data;
            Changed?.Invoke();
        }

        public static Language Language
        {
            get => save != null && Enum.IsDefined(typeof(Language), save.language) ? (Language)save.language : Language.EnglishKazakh;
            set
            {
                if (save == null || save.language == (int)value) return;
                save.language = (int)value;
                Changed?.Invoke();
            }
        }

        public static bool Bilingual => Language == Language.EnglishKazakh;

        public static string Main(string en, string kk, string ru) => Language switch
        {
            Language.Kazakh => Or(kk, en),
            Language.Russian => Or(ru, en),
            _ => Or(en, kk),
        };

        public static string Sub(string en, string kk, string ru) => Bilingual ? Or(kk, null) : null;

        public static string Text(string en, string kk, string ru) => Language switch
        {
            Language.English => Or(en, kk),
            Language.Russian => Or(ru, en),
            _ => Or(kk, en),
        };

        /// <summary>The name of a language option, written in that language.</summary>
        public static string OptionName(Language language) => language switch
        {
            Language.English => "English",
            Language.Kazakh => "Қазақша",
            Language.Russian => "Русский",
            _ => "EN + ҚАЗ",
        };

        static string Or(string text, string fallback) => string.IsNullOrEmpty(text) ? fallback : text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            save = null;
            Changed = null;
        }
    }
}
