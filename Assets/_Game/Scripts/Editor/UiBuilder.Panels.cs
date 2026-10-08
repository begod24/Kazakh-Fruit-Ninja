using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    public static partial class UiBuilder
    {
        // Every panel sits on the same 1800x1000 stage: its top edge at 470 (the header's crest sits on it),
        // the "Go back" pill under its bottom edge.
        const float PanelTop = 470f, HeaderY = 398f, BackY = -448f;

        static Vector2 PanelCenter(float height) => new(0f, PanelTop - height * 0.5f);

        // ---------- Blades ----------

        static readonly Vector2 BladeTileSize = new(340f, 270f);

        static BladesScreen BuildBlades(Transform parent, CollectionManager collection, UiRoot uiRoot)
        {
            RectTransform screen = PanelScreen(parent, "Blades", out RectTransform _, out RectTransform content);
            Panel(content, PanelCenter(860f), new Vector2(1600f, 860f));
            Header(content, UiText.Blades.Key, HeaderY);
            TextMeshProUGUI hint = Text(Box(content, "Hint", new Vector2(0f, 286f), new Vector2(1300f, 36f)), "", 24f, WithAlpha(Palette.Cream, 0.75f),
                TextAlignmentOptions.Center, bodyFont, bodyShadow);
            Label(hint.gameObject, UiText.BladesHint.Key, hint, null, Vector2.zero, true);

            // Four blades on the first row, the rest centred under them.
            var rows = new Object[2];
            for (int r = 0; r < rows.Length; r++)
            {
                RectTransform row = Box(content, $"Row{r}", new Vector2(0f, 125f - r * (BladeTileSize.y + 22f)), new Vector2(1500f, BladeTileSize.y));
                Row(row, 24f);
                rows[r] = row;
            }
            BladeTileView template = BuildBladeTile((Transform)rows[0]);
            UnityEngine.UI.Button back = BackButton(content, BackY);

            var blades = screen.gameObject.AddComponent<BladesScreen>();
            Wire(blades, ("collection", collection), ("root", uiRoot), ("tileTemplate", template), ("backButton", back));
            WireArray(blades, "rows", rows);
            Pop(blades, content);
            return blades;
        }

        static BladeTileView BuildBladeTile(Transform row)
        {
            RectTransform tile = Place(row, "TileTemplate", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, BladeTileSize);
            Fixed(tile, BladeTileSize.x, BladeTileSize.y);
            RectTransform glowRect = Stretch(tile, "Glow", -26f);
            Image(glowRect, WithAlpha(Palette.Gold, 0.45f), shadow);
            UnityEngine.UI.Image face = SolidTile(tile, 18f);
            UnityEngine.UI.Image frame = tile.Find("Line").GetComponent<UnityEngine.UI.Image>();

            // From the top: the trail (with a lock over it while closed), the name, the state or achievement, its progress.
            UnityEngine.UI.Image preview = Image(Box(tile, "Preview", new Vector2(0f, 60f), new Vector2(304f, 122f)), Color.white, null);
            RectTransform lockRect = Box(tile, "Lock", new Vector2(0f, 60f), new Vector2(56f, 56f));
            Image(lockRect, WithAlpha(Palette.Cream, 0.92f), lockIcon);
            RectTransform badge = Box(tile, "Check", new Vector2(BladeTileSize.x * 0.5f - 12f, BladeTileSize.y * 0.5f - 12f), new Vector2(50f, 50f));
            Image(badge, Palette.Gold, circle);
            Image(Box(badge, "Icon", Vector2.zero, new Vector2(30f, 30f)), Palette.Ink, check);

            TextMeshProUGUI name = Text(Box(tile, "Name", new Vector2(0f, -28f), new Vector2(310f, 44f)), "Пышақ", 34f, Palette.Cream,
                TextAlignmentOptions.Center, serifFont, serifShadow);
            ShrinkToFit(name, 22f);
            TextMeshProUGUI status = Text(Box(tile, "Status", new Vector2(0f, -73f), new Vector2(312f, 54f)), UiText.Select.Kk, 21f, WithAlpha(Palette.Cream, 0.8f),
                TextAlignmentOptions.Center, bodyFont);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.enableAutoSizing = true;
            status.fontSizeMin = 14f;
            status.fontSizeMax = 21f;

            RectTransform progress = Box(tile, "Progress", new Vector2(0f, -114f), new Vector2(300f, 22f));
            Image(Place(progress, "Back", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200f, 8f)),
                WithAlpha(Color.white, 0.15f), rounded).pixelsPerUnitMultiplier = 10f;
            UnityEngine.UI.Image fill = Image(Place(progress, "Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200f, 8f)),
                Palette.Gold, null);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            TextMeshProUGUI progressText = Text(Place(progress, "Text", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(94f, 26f)),
                "0/100", 18f, WithAlpha(Palette.Cream, 0.85f), TextAlignmentOptions.Right, bodyFont);

            var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            tile.gameObject.AddComponent<UiPressScale>();
            var view = tile.gameObject.AddComponent<BladeTileView>();
            Wire(view, ("button", button), ("frame", frame), ("glow", glowRect.gameObject), ("preview", preview), ("nameText", name),
                ("statusText", status), ("checkIcon", badge.gameObject), ("lockIcon", lockRect.gameObject), ("progressGroup", progress.gameObject),
                ("progressFill", fill), ("progressText", progressText));
            tile.gameObject.SetActive(false);
            return view;
        }

        // ---------- Records ----------

        const float RecordsLeftX = -560f, RecordsRightX = 250f;

        static RecordsScreen BuildRecords(Transform parent, GameManager game, CollectionManager collection, UiRoot uiRoot)
        {
            RectTransform screen = PanelScreen(parent, "Records", out RectTransform _, out RectTransform content);
            Panel(content, PanelCenter(860f), new Vector2(1720f, 860f));
            Header(content, UiText.Records.Key, HeaderY);

            // Left: each mode's record, then the overall numbers.
            (TextMeshProUGUI classicBest, TextMeshProUGUI classicGames) = ModeRecord(content, "ClassicRecord", thumbClassic, UiText.Classic.Key, 170f);
            (TextMeshProUGUI arcadeBest, TextMeshProUGUI arcadeGames) = ModeRecord(content, "ArcadeRecord", thumbArcade, UiText.Arcade.Key, -46f);
            RectTransform stats = Box(content, "Stats", new Vector2(RecordsLeftX, -262f), new Vector2(520f, 200f));
            Tile(stats);
            TextMeshProUGUI totalSliced = StatLine(stats, "TotalSliced", UiText.TotalSliced.Key, 60f);
            TextMeshProUGUI bestCombo = StatLine(stats, "BestCombo", UiText.BestCombo.Key, 0f);
            TextMeshProUGUI cards = StatLine(stats, "Cards", UiText.Cards.Key, -60f);

            // Right: the Дастархан — every dish, how many were sliced, its card level; the chosen one's story below.
            TextMeshProUGUI section = GoldText(Box(content, "DastarkhanTitle", new Vector2(RecordsRightX, 262f), new Vector2(1080f, 52f)), "", 40f, TextAlignmentOptions.Center);
            section.characterSpacing = 6f;
            Label(section.gameObject, UiText.Dastarkhan.Key, section, null, Vector2.zero);
            TextMeshProUGUI hint = Text(Box(content, "DastarkhanHint", new Vector2(RecordsRightX, 224f), new Vector2(1080f, 32f)), "", 22f,
                WithAlpha(Palette.Cream, 0.7f), TextAlignmentOptions.Center, bodyFont, bodyShadow);
            Label(hint.gameObject, UiText.DastarkhanHint.Key, hint, null, Vector2.zero, true);

            RectTransform grid = Box(content, "Grid", new Vector2(RecordsRightX, 20f), new Vector2(1080f, 360f));
            var layout = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.cellSize = new Vector2(142f, 172f);
            layout.spacing = new Vector2(14f, 14f);
            layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 7;
            layout.childAlignment = TextAnchor.UpperCenter;
            CollectionCardView cardTemplate = BuildDishTile(grid);

            RectTransform detail = Box(content, "Detail", new Vector2(RecordsRightX, -268f), new Vector2(1080f, 196f));
            Tile(detail);
            UnityEngine.UI.Image detailIcon = Image(Place(detail, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(166f, 166f)),
                Color.white, circle);
            detailIcon.preserveAspect = true;
            TextMeshProUGUI detailName = Text(Place(detail, "Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -10f), new Vector2(560f, 54f)),
                "Бауырсақ", 42f, Palette.GoldLight, TextAlignmentOptions.Left, serifFont, serifShadow);
            TextMeshProUGUI detailSubtitle = Text(Place(detail, "Subtitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(208f, -62f), new Vector2(600f, 28f)),
                "Baursak · Баурсак", 20f, WithAlpha(Palette.Cream, 0.7f), TextAlignmentOptions.Left, bodyFont);
            TextMeshProUGUI detailFact = Text(Place(detail, "Fact", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(208f, -94f), new Vector2(850f, 70f)),
                "", 20f, Palette.Cream, TextAlignmentOptions.TopLeft, bodyFont);
            detailFact.textWrappingMode = TextWrappingModes.Normal;
            detailFact.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI detailCount = Text(Place(detail, "Count", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -18f), new Vector2(300f, 34f)),
                string.Format(UiText.CountFormat.Kk, 0), 24f, Palette.Gold, TextAlignmentOptions.Right, bodyFont);

            UnityEngine.UI.Button back = BackButton(content, BackY);

            var records = screen.gameObject.AddComponent<RecordsScreen>();
            Wire(records, ("game", game), ("collection", collection), ("root", uiRoot), ("backButton", back),
                ("classicBest", classicBest), ("classicGames", classicGames), ("arcadeBest", arcadeBest), ("arcadeGames", arcadeGames),
                ("totalSliced", totalSliced), ("bestCombo", bestCombo), ("cardsCollected", cards),
                ("cardTemplate", cardTemplate), ("cardGrid", grid),
                ("detailIcon", detailIcon), ("detailName", detailName), ("detailSubtitle", detailSubtitle), ("detailFact", detailFact), ("detailCount", detailCount));
            Pop(records, content);
            return records;
        }

        /// <summary>A mode's record: its picture from the mode card, its name, best score and rounds played.</summary>
        static (TextMeshProUGUI best, TextMeshProUGUI games) ModeRecord(Transform parent, string name, Sprite thumb, string key, float y)
        {
            RectTransform tile = Box(parent, name, new Vector2(RecordsLeftX, y), new Vector2(520f, 196f));
            Tile(tile);
            UnityEngine.UI.Image picture = Image(Box(tile, "Thumb", new Vector2(-140f, 0f), new Vector2(214f, 151f)), Color.white, thumb);
            picture.preserveAspect = true;
            TextMeshProUGUI title = GoldText(Place(tile, "Name", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-20f, 62f), new Vector2(270f, 44f)),
                "", 34f, TextAlignmentOptions.Left);
            title.characterSpacing = 3f;
            ShrinkToFit(title, 22f);
            Label(title.gameObject, key, title, null, new Vector2(-20f, 62f));
            TextMeshProUGUI label = Text(Place(tile, "BestLabel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-18f, 24f), new Vector2(270f, 28f)),
                "", 20f, WithAlpha(Palette.Cream, 0.7f), TextAlignmentOptions.Left, bodyFont);
            Label(label.gameObject, UiText.BestScore.Key, label, null, Vector2.zero, true);
            TextMeshProUGUI best = Text(Place(tile, "Best", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-20f, -22f), new Vector2(270f, 64f)),
                "0", 54f, Palette.Cream, TextAlignmentOptions.Left, bodyFont, bodyShadow);
            TextMeshProUGUI games = Text(Place(tile, "Games", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-18f, -70f), new Vector2(270f, 28f)),
                string.Format(UiText.GamesFormat.Kk, 0), 20f, WithAlpha(Palette.Cream, 0.7f), TextAlignmentOptions.Left, bodyFont);
            return (best, games);
        }

        /// <summary>A label on the left and its number on the right.</summary>
        static TextMeshProUGUI StatLine(Transform tile, string name, string key, float y)
        {
            RectTransform line = Box(tile, name, new Vector2(0f, y), new Vector2(460f, 50f));
            TextMeshProUGUI label = Text(Place(line, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 40f)),
                "", 24f, Palette.Cream, TextAlignmentOptions.Left, bodyFont);
            ShrinkToFit(label, 16f);
            Label(label.gameObject, key, label, null, Vector2.zero, true);
            return Text(Place(line, "Value", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(160f, 50f)),
                "0", 32f, Palette.GoldLight, TextAlignmentOptions.Right, bodyFont, bodyShadow);
        }

        static CollectionCardView BuildDishTile(Transform grid)
        {
            RectTransform tile = Place(grid, "CardTemplate", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(142f, 172f));
            RectTransform selection = Stretch(tile, "Selection", -18f);
            Image(selection, WithAlpha(Palette.Gold, 0.55f), shadow);
            selection.gameObject.SetActive(false);
            UnityEngine.UI.Image face = SolidTile(tile, 14f);
            UnityEngine.UI.Image frame = tile.Find("Line").GetComponent<UnityEngine.UI.Image>();

            // From the top: icon, name, how many were sliced, level dots, progress to the next level.
            UnityEngine.UI.Image icon = Image(Box(tile, "Icon", new Vector2(0f, 36f), new Vector2(94f, 94f)), Color.white, circle);
            icon.preserveAspect = true;
            TextMeshProUGUI name = Text(Box(tile, "Name", new Vector2(0f, -22f), new Vector2(132f, 26f)), "Бауырсақ", 18f, Palette.Cream, TextAlignmentOptions.Center, bodyFont);
            ShrinkToFit(name, 11f);
            TextMeshProUGUI count = Text(Box(tile, "Count", new Vector2(0f, -47f), new Vector2(132f, 28f)), "0", 22f, Palette.GoldLight, TextAlignmentOptions.Center, bodyFont);

            RectTransform pipsRow = Box(tile, "Pips", new Vector2(0f, -68f), new Vector2(60f, 10f));
            Row(pipsRow, 6f);
            var pips = new Object[3];
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pip = Place(pipsRow, $"Pip{i}", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
                Fixed(pip, 10f, 10f);
                pips[i] = Image(pip, WithAlpha(Color.white, 0.15f), circle);
            }
            Image(Box(tile, "ProgressBack", new Vector2(0f, -79f), new Vector2(112f, 4f)), WithAlpha(Color.white, 0.15f), null);
            UnityEngine.UI.Image fill = Image(Box(tile, "ProgressFill", new Vector2(0f, -79f), new Vector2(112f, 4f)), Palette.Gold, null);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

            var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            var view = tile.gameObject.AddComponent<CollectionCardView>();
            Wire(view, ("button", button), ("frame", frame), ("icon", icon), ("nameText", name), ("countText", count),
                ("progressFill", fill), ("selection", selection.gameObject));
            WireArray(view, "pips", pips);
            tile.gameObject.SetActive(false);
            return view;
        }

        // ---------- Settings ----------

        static SettingsScreen BuildSettings(Transform parent, GameManager game, UiRoot uiRoot)
        {
            RectTransform screen = PanelScreen(parent, "Settings", out RectTransform _, out RectTransform content, 0.62f);
            const float height = 700f;
            Panel(content, PanelCenter(height), new Vector2(1180f, height));
            Header(content, UiText.Settings.Key, HeaderY);

            (UnityEngine.UI.Button shake, TextMeshProUGUI shakeValue) = SettingRow(content, "Shake", UiText.ScreenShake.Key, 236f);
            (UnityEngine.UI.Button hitStop, TextMeshProUGUI hitStopValue) = SettingRow(content, "HitStop", UiText.HitStop.Key, 124f);
            (UnityEngine.UI.Button graphics, TextMeshProUGUI graphicsValue) = SettingRow(content, "Graphics", UiText.Graphics.Key, 12f);

            // Language: four chips, the chosen one lit.
            RectTransform languageRow = Box(content, "Language", new Vector2(0f, -112f), new Vector2(1000f, 96f));
            Tile(languageRow);
            TextMeshProUGUI languageLabel = Text(Place(languageRow, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(230f, 60f)),
                "", 30f, Palette.Cream, TextAlignmentOptions.Left, bodyFont);
            ShrinkToFit(languageLabel, 20f);
            Label(languageLabel.gameObject, UiText.LanguageLabel.Key, languageLabel, null, Vector2.zero, true);
            RectTransform chips = Place(languageRow, "Chips", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(720f, 70f));
            Row(chips, 12f, TextAnchor.MiddleRight);
            var languageButtons = new Object[4];
            for (int i = 0; i < languageButtons.Length; i++)
            {
                RectTransform chip = Place(chips, $"Chip{i}", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(165f, 66f));
                Fixed(chip, 165f, 66f);
                Tile(chip, 33f, 1f, true);
                Text(Stretch(chip, "Text", 8f, 0f, 8f, 0f), Loc.OptionName((Language)i), 22f, Palette.Cream, TextAlignmentOptions.Center, bodyFont);
                var button = chip.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = chip.Find("Line").GetComponent<UnityEngine.UI.Image>();
                SetColors(button);
                chip.gameObject.AddComponent<UiPressScale>();
                languageButtons[i] = button;
            }

            UnityEngine.UI.Button back = BackButton(content, PanelTop - height - 58f);

            var settings = screen.gameObject.AddComponent<SettingsScreen>();
            Wire(settings, ("game", game), ("root", uiRoot), ("shakeButton", shake), ("shakeValue", shakeValue),
                ("hitStopButton", hitStop), ("hitStopValue", hitStopValue), ("graphicsButton", graphics), ("graphicsValue", graphicsValue),
                ("backButton", back));
            WireArray(settings, "languageButtons", languageButtons);
            Pop(settings, content);
            return settings;
        }

        /// <summary>A whole-width button: the option on the left, its value on the right.</summary>
        static (UnityEngine.UI.Button, TextMeshProUGUI) SettingRow(Transform parent, string name, string key, float y)
        {
            RectTransform row = Box(parent, name, new Vector2(0f, y), new Vector2(1000f, 96f));
            UnityEngine.UI.Image face = Tile(row, 18f, 0.45f, true);
            TextMeshProUGUI label = Text(Place(row, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(640f, 60f)),
                "", 30f, Palette.Cream, TextAlignmentOptions.Left, bodyFont);
            Label(label.gameObject, key, label, null, Vector2.zero, true);
            TextMeshProUGUI value = Text(Place(row, "Value", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(280f, 60f)),
                UiText.On.Kk, 36f, Palette.GoldLight, TextAlignmentOptions.Right, serifFont, serifShadow);
            var button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;
            SetColors(button);
            row.gameObject.AddComponent<UiPressScale>();
            return (button, value);
        }
    }
}
