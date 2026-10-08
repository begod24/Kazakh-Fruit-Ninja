using TMPro;
using UnityEngine;

namespace KazakhNinja.EditorTools
{
    public static partial class UiBuilder
    {
        // ---------- Main page ----------

        const float TitleTop = 175f, MenuButtonSize = 128f, MenuButtonSpacing = 240f, MenuButtonsY = 178f;

        static MainMenuScreen BuildHome(Transform parent, UiRoot uiRoot, AudioManager audio)
        {
            RectTransform screen = ScreenPanel(parent, "MainMenu");
            // The painting stays bright, as on the mockup; only the edges darken a little for the text.
            Image(Stretch(screen, "Backdrop"), new Color(0f, 0f, 0f, 0.38f), vignette);
            RectTransform curtain = Stretch(screen, "IntroCurtain");
            Image(curtain, new Color(0.06f, 0.04f, 0.03f, 1f), null);
            Group(curtain).alpha = 0f; // raised by the splash on its first frame
            RectTransform safe = SafeArea(screen);

            // The name: big white serif with a soft shadow, the Kazakh spelling in gold under it (the mockup's top bar).
            RectTransform title = Place(safe, "TitleBlock", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -TitleTop), new Vector2(1200f, 210f));
            Group(title);
            TextMeshProUGUI name = Text(Box(title, "Title", new Vector2(0f, 18f), new Vector2(1200f, 150f)), UiText.Title, 112f, Palette.White,
                TextAlignmentOptions.Center, titleFont, titleGlow);
            name.characterSpacing = 4f;
            TextMeshProUGUI nameKk = Text(Box(title, "Subtitle", new Vector2(0f, -60f), new Vector2(800f, 40f)), UiText.TitleKk, 26f, Palette.Gold,
                TextAlignmentOptions.Center, bodyFont, bodyShadow);
            nameKk.characterSpacing = 14f;
            RectTransform shine = Box(title, "Shine", Vector2.zero, new Vector2(64f, 300f));
            shine.localRotation = Quaternion.Euler(0f, 0f, -18f);
            Image(shine, new Color(1f, 0.96f, 0.85f, 0.4f), glow);
            shine.gameObject.SetActive(false);

            // "Slice to play", breathing in the middle.
            RectTransform prompt = Box(safe, "Prompt", new Vector2(0f, -10f), new Vector2(1100f, 200f));
            Group(prompt);
            RectTransform breath = Stretch(prompt, "Breath");
            CanvasGroup breathGroup = Group(breath);
            TextMeshProUGUI promptMain = Text(Box(breath, "Title", new Vector2(0f, 32f), new Vector2(1100f, 84f)), "", 60f, Palette.Cream,
                TextAlignmentOptions.Center, titleFont, titleGlow);
            promptMain.characterSpacing = 8f;
            Image(Box(breath, "Line", new Vector2(0f, -12f), new Vector2(660f, 8f)), new Color(1f, 1f, 1f, 0.85f), lineFade);
            TextMeshProUGUI promptSub = Text(Box(breath, "Sub", new Vector2(0f, -50f), new Vector2(1000f, 48f)), "", 32f, Palette.White,
                TextAlignmentOptions.Center, bodyFont, bodyShadow);
            promptSub.characterSpacing = 4f;
            Label(prompt.gameObject, UiText.SliceToPlay.Key, promptMain, promptSub, new Vector2(0f, 26f));

            // The three round buttons from the mockup, along the bottom.
            UnityEngine.UI.Button blades = RoundMenuButton(safe, "BladesButton", buttonBlades, UiText.Blades.Key, new Vector2(-MenuButtonSpacing, MenuButtonsY), MenuButtonSize);
            UnityEngine.UI.Button records = RoundMenuButton(safe, "RecordsButton", buttonRecords, UiText.Records.Key, new Vector2(0f, MenuButtonsY), MenuButtonSize);
            UnityEngine.UI.Button settings = RoundMenuButton(safe, "SettingsButton", buttonSettings, UiText.Settings.Key, new Vector2(MenuButtonSpacing, MenuButtonsY), MenuButtonSize);

            var home = screen.gameObject.AddComponent<MainMenuScreen>();
            Wire(home, ("root", uiRoot), ("audioManager", audio), ("bladesButton", blades), ("recordsButton", records),
                ("settingsButton", settings), ("prompt", breathGroup));
            return home;
        }

        // ---------- Mode choice ----------

        /// <summary>The mockup cards are 572x592 with their shadow; shown a little smaller.</summary>
        static readonly Vector2 ModeCardSize = new(543f, 562f);

        static ModeSelectScreen BuildModes(Transform parent, GameManager game, UiRoot uiRoot)
        {
            RectTransform screen = PanelScreen(parent, "ModeSelect", out RectTransform _, out RectTransform content, 0.45f);
            Header(content, UiText.ChooseMode.Key, 392f, 70f, true, true);

            UnityEngine.UI.Button classic = ModeCard(content, "ClassicCard", modeClassic, UiText.ClassicGame.Key, -305f, 0f, out TextMeshProUGUI classicBest);
            UnityEngine.UI.Button arcade = ModeCard(content, "ArcadeCard", modeArcade, UiText.ArcadeGame.Key, 305f, 1.4f, out TextMeshProUGUI arcadeBest);
            UnityEngine.UI.Button back = BackButton(content, -438f);

            var modes = screen.gameObject.AddComponent<ModeSelectScreen>();
            Wire(modes, ("game", game), ("root", uiRoot), ("classicButton", classic), ("arcadeButton", arcade),
                ("classicBest", classicBest), ("arcadeBest", arcadeBest), ("backButton", back));
            Pop(modes, content);
            return modes;
        }

        /// <summary>
        /// A mode card from the mockup (its baked captions painted out); the caption is drawn in the chosen language
        /// in the card's lower band, and the record goes under the card.
        /// </summary>
        static UnityEngine.UI.Button ModeCard(Transform parent, string name, Sprite art, string key, float x, float pulsePhase, out TextMeshProUGUI best)
        {
            RectTransform root = Box(parent, name, new Vector2(x, 12f), ModeCardSize);
            RectTransform visual = Stretch(root, "Visual");
            UnityEngine.UI.Image face = Image(Stretch(visual, "Art"), Color.white, art, true);

            // The band under the gold line runs from 70% to 92% of the card's height (rows 412-546 of 592).
            float h = ModeCardSize.y;
            TextMeshProUGUI main = Text(Box(visual, "Title", new Vector2(0f, -0.302f * h), new Vector2(440f, 60f)), "", 44f, Palette.Cream,
                TextAlignmentOptions.Center, titleFont, titleGlow);
            main.characterSpacing = 2f;
            ShrinkToFit(main, 28f);
            TextMeshProUGUI sub = Text(Box(visual, "Sub", new Vector2(0f, -0.372f * h), new Vector2(440f, 32f)), "", 23f, WithAlpha(Palette.White, 0.95f),
                TextAlignmentOptions.Center, bodyFont, bodyShadow);
            sub.characterSpacing = 1f;
            ShrinkToFit(sub, 14f);
            Label(root.gameObject, key, main, sub, new Vector2(0f, -0.318f * h));

            // The card art has ~40 units of shadow under it, so this sits just below the card itself.
            best = Text(Box(root, "Best", new Vector2(0f, -h * 0.5f + 6f), new Vector2(520f, 44f)), string.Format(UiText.BestFormat.Kk, 0), 28f, Palette.Gold,
                TextAlignmentOptions.Center, bodyFont, bodyShadow);

            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            Wire(root.gameObject.AddComponent<UiPressScale>(), ("target", visual));
            var pulse = root.gameObject.AddComponent<UiPulse>();
            SetFloat(pulse, "amount", 0.012f);
            SetFloat(pulse, "phase", pulsePhase);
            return button;
        }

        // ---------- Splash ----------

        static void BuildIntro(GameObject root, GameManager game, AudioManager audio, MainMenuScreen home)
        {
            Transform safe = home.transform.Find("SafeArea");
            var intro = root.AddComponent<IntroSequence>();
            Transform rig = Camera.main != null ? Camera.main.transform.parent : null;
            Transform title = safe.Find("TitleBlock");
            Wire(intro, ("game", game), ("audioManager", audio), ("cameraRig", rig), ("curtain", Group(home.transform.Find("IntroCurtain"))),
                ("titleBlock", title), ("titleGroup", Group(title)), ("nameLabel", title.Find("Title").GetComponent<TMP_Text>()),
                ("shine", title.Find("Shine")));

            // What comes in once the name has risen, and from where.
            var reveals = new (string path, Vector2 offset, float delay)[]
            {
                ("Prompt", new Vector2(0f, -40f), 0.5f),
                ("BladesButton", new Vector2(0f, -200f), 0.7f),
                ("RecordsButton", new Vector2(0f, -200f), 0.8f),
                ("SettingsButton", new Vector2(0f, -200f), 0.9f),
            };
            var serialized = new UnityEditor.SerializedObject(intro);
            UnityEditor.SerializedProperty list = serialized.FindProperty("reveals");
            list.arraySize = reveals.Length;
            for (int i = 0; i < reveals.Length; i++)
            {
                UnityEditor.SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("target").objectReferenceValue = Group(safe.Find(reveals[i].path));
                entry.FindPropertyRelative("offset").vector2Value = reveals[i].offset;
                entry.FindPropertyRelative("delay").floatValue = reveals[i].delay;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static CanvasGroup Group(Transform target) => ContentBuilder.GetOrAdd<CanvasGroup>(target.gameObject);
    }
}
