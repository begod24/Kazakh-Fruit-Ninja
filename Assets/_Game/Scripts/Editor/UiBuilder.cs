using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Builds the uGUI canvas in the open Game scene: the main page with the splash, the mode choice, the blades,
    /// records and settings panels, the HUD, pause and results. The canvas is rebuilt from scratch on every run
    /// (nothing outside it refers to it). The look follows the menu mockups (UI/New UI): dark translucent cards
    /// with a thin gold frame, serif titles with a small Kazakh line under them, қошқар мүйіз ornaments.
    /// Reference resolution 1920x1080, matched on height because the game is landscape and phones differ mostly in width.
    /// </summary>
    public static partial class UiBuilder
    {
        const string MenuArtFolder = "Assets/_Game/UI/Sprites/Menu";

        static class Palette
        {
            public static readonly Color Gold = Hex(0xE8C778);
            public static readonly Color GoldLight = Hex(0xFBE6B0);
            public static readonly Color GoldDeep = Hex(0xC99A4E);
            public static readonly Color Cream = Hex(0xFFF2CF);
            public static readonly Color White = Hex(0xFFFBF2);
            /// <summary>The Arcade card's fill.</summary>
            public static readonly Color Ink = Hex(0x18150F);
            /// <summary>The Classic card's fill.</summary>
            public static readonly Color Brown = Hex(0x6D421F);
            public static readonly Color Red = Hex(0xC8322F);
            public static readonly Color Sky = Hex(0x4FC3D9);
            public static readonly Color Magenta = Hex(0xD6407F);
            public static readonly Color Green = Hex(0x5DBB63);
        }

        static TMP_FontAsset titleFont, serifFont, bodyFont;
        static Material titleGlow, serifShadow, bodyShadow, bodyOutline;
        static Sprite rounded, circle, vignette, glow, pauseIcon;
        static Sprite ring, ringCircle, shadow, divider, lineFade, crest, flourish, arrow, check, lockIcon, heart;
        static Sprite modeClassic, modeArcade, thumbClassic, thumbArcade, buttonBlades, buttonRecords, buttonSettings;

        [MenuItem("Kazakh Ninja/Build UI", priority = 23)]
        public static void BuildUi()
        {
            var game = Object.FindFirstObjectByType<GameManager>();
            var collection = Object.FindFirstObjectByType<CollectionManager>();
            if (game == null || collection == null)
            {
                Debug.LogError("Kazakh Ninja: the open scene has no GameManager/CollectionManager; run Kazakh Ninja/Build Game Scene first.");
                return;
            }
            if (!UiFonts.Build())
            {
                Debug.LogError("Kazakh Ninja: the UI fonts could not be made (see above).");
                return;
            }

            ContentTexts.Apply();
            UiArt.BuildAll();
            LoadArt();
            MakeMaterials();
            EnsureEventSystem();

            GameObject old = GameObject.Find("UI");
            if (old != null) Object.DestroyImmediate(old);
            BuildCanvas(game, collection);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Kazakh Ninja: UI built.");
        }

        static void LoadArt()
        {
            rounded = UiArt.Load(UiArt.RoundedRect);
            circle = UiArt.Load(UiArt.Circle);
            vignette = UiArt.Load(UiArt.Vignette);
            glow = UiArt.Load(UiArt.Glow);
            pauseIcon = UiArt.Load(UiArt.Pause);

            ring = MenuSprite("RingRect", new Vector4(48, 48, 48, 48));
            ringCircle = MenuSprite("RingCircle", Vector4.zero);
            shadow = MenuSprite("ShadowRect", new Vector4(63, 63, 63, 63));
            divider = MenuSprite("Divider", Vector4.zero);
            lineFade = MenuSprite("LineFade", Vector4.zero);
            crest = MenuSprite("Crest", Vector4.zero);
            flourish = MenuSprite("Flourish", Vector4.zero);
            arrow = MenuSprite("Arrow", Vector4.zero);
            check = MenuSprite("Check", Vector4.zero);
            lockIcon = MenuSprite("Lock", Vector4.zero);
            heart = MenuSprite("Heart", Vector4.zero);
            modeClassic = MenuSprite("ModeClassic", Vector4.zero);
            modeArcade = MenuSprite("ModeArcade", Vector4.zero);
            thumbClassic = MenuSprite("ThumbClassic", Vector4.zero);
            thumbArcade = MenuSprite("ThumbArcade", Vector4.zero);
            buttonBlades = MenuSprite("ButtonBlades", Vector4.zero);
            buttonRecords = MenuSprite("ButtonRecords", Vector4.zero);
            buttonSettings = MenuSprite("ButtonSettings", Vector4.zero);
        }

        /// <summary>The art from ArtSource/UI/build_menu_art.py, imported as UI sprites.</summary>
        static Sprite MenuSprite(string name, Vector4 border)
        {
            string path = $"{MenuArtFolder}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Kazakh Ninja: {path} is missing; run ArtSource/UI/build_menu_art.py.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void MakeMaterials()
        {
            titleFont = UiFonts.Title;
            serifFont = UiFonts.SerifBold;
            bodyFont = UiFonts.Body;
            titleGlow = UiFonts.Shadowed(titleFont, "Title Glow", 0.75f, 0.25f, new Vector2(0f, -0.6f), 0.6f);
            serifShadow = UiFonts.Shadowed(serifFont, "Serif Shadow", 0.45f, 0.15f, new Vector2(0.3f, -0.7f), 0.6f);
            bodyShadow = UiFonts.Shadowed(bodyFont, "Body Shadow", 0.5f, 0.1f, new Vector2(0.25f, -0.6f), 0.55f);
            // Numbers are set in Montserrat: Cormorant's figures are old-style (a 0 reads as an o).
            bodyOutline = UiFonts.Outlined(bodyFont, "Body Outline", new Color(0.13f, 0.08f, 0.04f, 1f), 0.18f);
            AssetDatabase.SaveAssets();
        }

        static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null) eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            // The project uses the Input System only, so the legacy StandaloneInputModule would not work.
            if (eventSystem.TryGetComponent(out StandaloneInputModule legacy)) Object.DestroyImmediate(legacy);
            if (!eventSystem.TryGetComponent(out InputSystemUIInputModule module))
            {
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        // ---------- Canvas ----------

        static void BuildCanvas(GameManager game, CollectionManager collection)
        {
            var root = new GameObject("UI", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var uiRoot = root.AddComponent<UiRoot>();
            var audio = Object.FindFirstObjectByType<AudioManager>();

            MainMenuScreen home = BuildHome(root.transform, uiRoot, audio);
            ModeSelectScreen modes = BuildModes(root.transform, game, uiRoot);
            BladesScreen blades = BuildBlades(root.transform, collection, uiRoot);
            RecordsScreen records = BuildRecords(root.transform, game, collection, uiRoot);
            HudScreen hud = BuildHud(root.transform, game);
            FloatingTextLayer popups = BuildPopups(root.transform, game, collection);
            PauseScreen pause = BuildPause(root.transform, game, uiRoot);
            SettingsScreen settings = BuildSettings(root.transform, game, uiRoot);
            GameOverScreen gameOver = BuildGameOver(root.transform, game, collection);

            var flashRect = Stretch(root.transform, "Flash");
            Image(flashRect, new Color(1f, 1f, 1f, 0f), null);
            Wire(popups, ("flash", flashRect.gameObject.AddComponent<ScreenFlash>()));

            Wire(uiRoot, ("game", game), ("home", home), ("modes", modes), ("blades", blades), ("records", records),
                ("settings", settings), ("hud", hud), ("pause", pause), ("gameOver", gameOver));
            BuildIntro(root, game, audio, home);
        }

        // ---------- Widgets ----------

        /// <summary>
        /// The mockup cards' frame: a soft shadow, a translucent fill and a thin gold line.
        /// <paramref name="radius"/> is the corner radius in canvas units.
        /// </summary>
        static UnityEngine.UI.Image Frame(RectTransform rect, Color fill, Color line, float radius, bool dropShadow = true, bool raycast = false)
        {
            if (dropShadow)
            {
                UnityEngine.UI.Image back = Image(Stretch(rect, "Shadow", -26f, -40f, -26f, -14f), new Color(0f, 0f, 0f, 0.5f), shadow);
                back.pixelsPerUnitMultiplier = 1f;
            }
            UnityEngine.UI.Image face = Image(Stretch(rect, "Fill"), fill, rounded, raycast);
            face.pixelsPerUnitMultiplier = 40f / radius;
            Image(Stretch(rect, "Line"), line, ring).pixelsPerUnitMultiplier = 40f / radius;
            return face;
        }

        /// <summary>A big dark card with a gold frame that holds a panel's contents.</summary>
        static RectTransform Panel(Transform parent, Vector2 center, Vector2 size)
        {
            RectTransform panel = Box(parent, "Panel", center, size);
            Frame(panel, WithAlpha(Palette.Ink, 0.86f), Palette.Gold, 30f, true, true);
            return panel;
        }

        /// <summary>A smaller section inside a panel: darker, with a fainter frame.</summary>
        static UnityEngine.UI.Image Tile(RectTransform rect, float radius = 16f, float lineAlpha = 0.45f, bool raycast = false) =>
            Frame(rect, new Color(0f, 0f, 0f, 0.32f), WithAlpha(Palette.Gold, lineAlpha), radius, false, raycast);

        /// <summary>A tile that can glow when chosen: nearly opaque, so the glow behind it only shows around its edge.</summary>
        static UnityEngine.UI.Image SolidTile(RectTransform rect, float radius) =>
            Frame(rect, WithAlpha(Hex(0x14110C), 0.96f), Palette.Gold, radius, false, true);

        /// <summary>
        /// A title as on the mockups: a crest of ram's horns, the big serif line, a gold divider and the small Kazakh line.
        /// <paramref name="y"/> is the big line's height; with one language the divider moves up to it.
        /// </summary>
        static LocalizedLabel Header(Transform parent, string key, float y, float size = 76f, bool withCrest = true, bool flourishes = false)
        {
            RectTransform header = Box(parent, "Header", new Vector2(0f, y), new Vector2(1400f, 200f));
            if (withCrest) Image(Box(header, "Crest", new Vector2(0f, 84f), new Vector2(150f, 70f)), Palette.Gold, crest).preserveAspect = true;
            TextMeshProUGUI main = GoldText(Box(header, "Title", new Vector2(0f, 14f), new Vector2(1200f, size * 1.3f)), "", size, TextAlignmentOptions.Center);
            main.characterSpacing = 6f;
            ShrinkToFit(main, size * 0.6f);
            Image(Box(header, "Divider", new Vector2(0f, -34f), new Vector2(620f, 20f)), WithAlpha(Palette.Gold, 0.85f), divider);
            TextMeshProUGUI sub = Text(Box(header, "Sub", new Vector2(0f, -66f), new Vector2(1000f, 40f)), "", 26f, WithAlpha(Palette.Cream, 0.9f),
                TextAlignmentOptions.Center, bodyFont, bodyShadow);
            sub.characterSpacing = 8f;
            if (flourishes)
                foreach (float side in new[] { -1f, 1f })
                {
                    RectTransform f = Box(header, side < 0 ? "FlourishLeft" : "FlourishRight", new Vector2(side * 720f, 14f), new Vector2(300f, 60f));
                    Image(f, Palette.Gold, flourish);
                    if (side > 0) f.localScale = new Vector3(-1f, 1f, 1f);
                }
            return Label(header.gameObject, key, main, sub, new Vector2(0f, 4f));
        }

        /// <summary>A pill-shaped button with a gold frame, like the mockup's "Go back": a big line and a small second line.</summary>
        static UnityEngine.UI.Button PillButton(Transform parent, string name, string key, Vector2 size, bool backArrow = false, bool highlight = false)
        {
            RectTransform root = Place(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Fixed(root, size.x, size.y);
            RectTransform visual = Stretch(root, "Visual");
            float radius = size.y * 0.5f;
            UnityEngine.UI.Image face = Frame(visual, highlight ? WithAlpha(Palette.Brown, 0.92f) : WithAlpha(Palette.Ink, 0.8f),
                highlight ? Palette.GoldLight : Palette.Gold, radius, true, true);
            float textLeft = backArrow ? 64f : 0f;
            if (backArrow)
                Image(Place(visual, "Arrow", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(radius + 14f, 0f), new Vector2(46f, 46f)),
                    Palette.Gold, arrow);
            float mainSize = Mathf.Min(40f, size.y * 0.42f);
            TextMeshProUGUI main = Text(Place(visual, "Title", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(textLeft * 0.5f, size.y * 0.14f),
                new Vector2(-textLeft - 40f, mainSize * 1.3f)), "", mainSize, Palette.Cream, TextAlignmentOptions.Center, serifFont, serifShadow);
            main.characterSpacing = 3f;
            ShrinkToFit(main, mainSize * 0.6f);
            TextMeshProUGUI sub = Text(Place(visual, "Sub", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(textLeft * 0.5f, -size.y * 0.24f),
                new Vector2(-textLeft - 40f, 28f)), "", Mathf.Min(20f, size.y * 0.21f), WithAlpha(Palette.Cream, 0.85f), TextAlignmentOptions.Center, bodyFont, bodyShadow);
            sub.characterSpacing = 3f;
            ShrinkToFit(sub, 12f);
            Label(root.gameObject, key, main, sub, new Vector2(textLeft * 0.5f, 0f));

            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            Wire(root.gameObject.AddComponent<UiPressScale>(), ("target", visual));
            return button;
        }

        /// <summary>A round icon button (the main page's three) with its label under it.</summary>
        static UnityEngine.UI.Button RoundMenuButton(Transform parent, string name, Sprite art, string key, Vector2 position, float size)
        {
            RectTransform root = Place(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), position, new Vector2(size + 60f, size + 60f));
            RectTransform visual = Stretch(root, "Visual");
            Image(Box(visual, "Shadow", new Vector2(0f, -12f), new Vector2(size * 1.25f, size * 1.25f)), new Color(0f, 0f, 0f, 0.55f), glow);
            UnityEngine.UI.Image face = Image(Box(visual, "Icon", new Vector2(0f, 0f), new Vector2(size, size)), Color.white, art, true);
            TextMeshProUGUI label = Text(Box(visual, "Label", new Vector2(0f, -size * 0.5f - 34f), new Vector2(300f, 44f)), "", 30f, Palette.Cream,
                TextAlignmentOptions.Center, serifFont, serifShadow);
            label.characterSpacing = 3f;
            ShrinkToFit(label, 20f);
            Label(root.gameObject, key, label, null, Vector2.zero, true);

            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            Wire(root.gameObject.AddComponent<UiPressScale>(), ("target", visual));
            return button;
        }

        /// <summary>A small round button (pause) in the same dark-and-gold look.</summary>
        static UnityEngine.UI.Button RoundIconButton(RectTransform root, Sprite icon)
        {
            RectTransform visual = Stretch(root, "Visual");
            Image(Stretch(visual, "Shadow", -18f, -26f, -18f, -10f), new Color(0f, 0f, 0f, 0.5f), glow);
            UnityEngine.UI.Image face = Image(Stretch(visual, "Fill"), WithAlpha(Palette.Ink, 0.82f), circle, true);
            Image(Stretch(visual, "Line"), Palette.Gold, ringCircle);
            Image(Stretch(visual, "Icon", 30f), Palette.Cream, icon);
            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            Wire(root.gameObject.AddComponent<UiPressScale>(), ("target", visual));
            return button;
        }

        /// <summary>A panel screen: a dim over the world, and a fixed-size stage that shrinks to fit narrow screens (4:3).</summary>
        static RectTransform PanelScreen(Transform parent, string name, out RectTransform stage, out RectTransform content, float dim = 0.55f)
        {
            RectTransform screen = ScreenPanel(parent, name);
            Image(Stretch(screen, "Dim"), new Color(0f, 0f, 0f, dim), vignette, true);
            RectTransform safe = SafeArea(screen);
            stage = Box(safe, "Stage", Vector2.zero, new Vector2(1800f, 1000f));
            stage.gameObject.AddComponent<UiFitInParent>();
            content = Stretch(stage, "Content");
            return screen;
        }

        /// <summary>Wires a <see cref="UiScreen"/>'s pop-in target.</summary>
        static void Pop(UiScreen screen, RectTransform target) => Wire(screen, ("pop", target));

        /// <summary>The "Go back" pill under a panel.</summary>
        static UnityEngine.UI.Button BackButton(Transform parent, float y)
        {
            UnityEngine.UI.Button back = PillButton(parent, "Back", UiText.GoBack.Key, new Vector2(380f, 92f), true);
            ((RectTransform)back.transform).anchoredPosition = new Vector2(0f, y);
            return back;
        }

        static LocalizedLabel Label(GameObject host, string key, TMP_Text main, TMP_Text sub, Vector2 mainAlone, bool body = false)
        {
            var label = host.AddComponent<LocalizedLabel>();
            var serialized = new SerializedObject(label);
            serialized.FindProperty("key").stringValue = key;
            serialized.FindProperty("main").objectReferenceValue = main;
            serialized.FindProperty("sub").objectReferenceValue = sub;
            serialized.FindProperty("body").boolValue = body;
            serialized.FindProperty("mainAlone").vector2Value = mainAlone;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Show the default language in the editor too.
            Phrase phrase = UiText.Find(key);
            if (phrase != null)
            {
                main.text = body ? phrase.Kk : phrase.En;
                if (sub != null) sub.text = phrase.Kk;
            }
            return label;
        }

        /// <summary>Serif title text with the mockups' gold gradient.</summary>
        static TextMeshProUGUI GoldText(RectTransform rect, string text, float size, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = Text(rect, text, size, Color.white, alignment, titleFont, titleGlow);
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(Palette.GoldLight, Palette.GoldLight, Palette.GoldDeep, Palette.GoldDeep);
            return label;
        }

        /// <summary>Lets a single-line label shrink down to <paramref name="minSize"/> when its text is wider than its box.</summary>
        static TextMeshProUGUI ShrinkToFit(TextMeshProUGUI label, float minSize)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = label.fontSize;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static void SetColors(UnityEngine.UI.Button button)
        {
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; // views show their own locked/selected look
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None; // touch game: no keyboard focus highlight
            button.navigation = navigation;
        }

        // ---------- Layout helpers ----------

        /// <summary>A full-screen, initially hidden screen with a CanvasGroup.</summary>
        static RectTransform ScreenPanel(Transform parent, string name)
        {
            RectTransform screen = Stretch(parent, name);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return screen;
        }

        static RectTransform SafeArea(Transform parent)
        {
            RectTransform safe = Stretch(parent, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        /// <summary>A centred box of a fixed size at an offset from its parent's centre.</summary>
        static RectTransform Box(Transform parent, string name, Vector2 offset, Vector2 size) =>
            Place(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, size);

        static RectTransform Stretch(Transform parent, string name, float inset = 0f) => Stretch(parent, name, inset, inset, inset, inset);

        static RectTransform Stretch(Transform parent, string name, float left, float bottom, float right, float top)
        {
            RectTransform rect = Place(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        static RectTransform Place(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Fixed(RectTransform rect, float width, float height)
        {
            var element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = element.minHeight = height;
        }

        static UnityEngine.UI.HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var row = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = alignment;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            return row;
        }

        static UnityEngine.UI.Image Image(RectTransform rect, Color color, Sprite sprite, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        static TextMeshProUGUI Text(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment,
            TMP_FontAsset fontAsset = null, Material material = null)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset != null ? fontAsset : bodyFont;
            if (material != null) label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        // ---------- Wiring ----------

        static void Wire(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null) Debug.LogError($"Kazakh Ninja: {target.GetType().Name} has no serialized field '{field}'.");
                else property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
