using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    public static partial class UiBuilder
    {
        // ---------- HUD ----------

        static HudScreen BuildHud(Transform parent, GameManager game)
        {
            RectTransform screen = ScreenPanel(parent, "Hud");
            RectTransform safe = SafeArea(screen);

            RectTransform scoreGroup = Place(safe, "ScoreGroup", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -26f), new Vector2(560f, 200f));
            RectTransform scoreRect = Place(scoreGroup, "Score", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -66f), new Vector2(560f, 136f));
            TextMeshProUGUI score = Text(scoreRect, "0", 104f, Palette.GoldLight, TextAlignmentOptions.Left, bodyFont, bodyOutline);
            TextMeshProUGUI best = Text(Place(scoreGroup, "Best", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -134f), new Vector2(560f, 40f)),
                string.Format(UiText.BestShortFormat.Kk, 0), 28f, WithAlpha(Palette.Cream, 0.9f), TextAlignmentOptions.Left, bodyFont, bodyShadow);
            best.characterSpacing = 4f;

            RectTransform timerGroup = Place(safe, "Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(236f, 96f));
            Frame(timerGroup, WithAlpha(Palette.Ink, 0.8f), Palette.Gold, 48f);
            TextMeshProUGUI timer = Text(Stretch(timerGroup, "Text", 0f, 2f, 0f, 0f), "1:00", 54f, Palette.White, TextAlignmentOptions.Center, bodyFont, bodyShadow);

            RectTransform powerUps = Place(safe, "PowerUps", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 64f));
            Row(powerUps, 18f);
            var indicators = new Object[]
            {
                PowerUpPill(powerUps, PowerUpType.SlowTime, Palette.Sky),
                PowerUpPill(powerUps, PowerUpType.DoubleScore, Palette.Magenta),
                PowerUpPill(powerUps, PowerUpType.Frenzy, Palette.Green),
            };

            RectTransform pauseRect = Place(safe, "PauseButton", Vector2.one, Vector2.one, Vector2.one, new Vector2(-44f, -32f), new Vector2(104f, 104f));
            UnityEngine.UI.Button pauseButton = RoundIconButton(pauseRect, pauseIcon);

            // Classic's three lives: the mockup's hearts, lost ones turn grey and tip over.
            RectTransform lives = Place(safe, "Lives", Vector2.one, Vector2.one, Vector2.one, new Vector2(-174f, -40f), new Vector2(300f, 90f));
            Row(lives, 12f, TextAnchor.MiddleRight);
            var hearts = new Object[3];
            for (int i = 0; i < hearts.Length; i++)
            {
                RectTransform icon = Place(lives, $"Life{i}", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86f, 77f));
                Fixed(icon, 86f, 77f);
                hearts[i] = Image(icon, Color.white, heart);
            }

            var hud = screen.gameObject.AddComponent<HudScreen>();
            Wire(hud, ("game", game), ("scoreText", score), ("bestText", best), ("timerGroup", timerGroup.gameObject), ("timerText", timer),
                ("livesGroup", lives.gameObject), ("pauseButton", pauseButton));
            WireArray(hud, "lifeIcons", hearts);
            WireArray(hud, "powerUpIndicators", indicators);
            return hud;
        }

        static PowerUpIndicator PowerUpPill(Transform parent, PowerUpType type, Color color)
        {
            RectTransform pill = Place(parent, type.ToString(), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 64f));
            Fixed(pill, 250f, 64f);
            var group = pill.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Frame(pill, WithAlpha(color, 0.85f), Palette.GoldLight, 32f, false);
            TextMeshProUGUI label = Text(Stretch(pill, "Label", 0f, 8f, 0f, 0f), UiText.PowerUpName(type), 28f, Color.white, TextAlignmentOptions.Center, bodyFont, bodyShadow);
            RectTransform barRect = Place(pill, "Bar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-50f, 6f));
            UnityEngine.UI.Image bar = Image(barRect, WithAlpha(Color.white, 0.9f), null);
            bar.type = UnityEngine.UI.Image.Type.Filled;
            bar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            bar.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

            var indicator = pill.gameObject.AddComponent<PowerUpIndicator>();
            var serialized = new UnityEditor.SerializedObject(indicator);
            serialized.FindProperty("type").enumValueIndex = (int)type;
            serialized.FindProperty("fill").objectReferenceValue = bar;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return indicator;
        }

        static FloatingTextLayer BuildPopups(Transform parent, GameManager game, CollectionManager collection)
        {
            RectTransform layer = Stretch(parent, "Popups");
            RectTransform templateRect = Box(layer, "PopupTemplate", Vector2.zero, new Vector2(900f, 240f));
            TextMeshProUGUI template = Text(templateRect, "+1", 80f, Color.white, TextAlignmentOptions.Center, bodyFont, bodyOutline);
            template.lineSpacing = -10f;
            templateRect.gameObject.SetActive(false);
            var popups = layer.gameObject.AddComponent<FloatingTextLayer>();
            Wire(popups, ("game", game), ("gameCamera", Camera.main), ("template", template), ("collection", collection));
            return popups;
        }

        // ---------- Pause ----------

        static PauseScreen BuildPause(Transform parent, GameManager game, UiRoot uiRoot)
        {
            RectTransform screen = PanelScreen(parent, "Pause", out RectTransform _, out RectTransform content, 0.65f);
            // Low enough to clear the Arcade timer at the top of the screen.
            const float top = 350f, height = 660f;
            Panel(content, new Vector2(0f, top - height * 0.5f), new Vector2(820f, height));
            Header(content, UiText.Paused.Key, top - 72f);

            var size = new Vector2(560f, 92f);
            UnityEngine.UI.Button resume = PillButton(content, "Resume", UiText.Resume.Key, size, false, true);
            UnityEngine.UI.Button restart = PillButton(content, "Restart", UiText.Restart.Key, size);
            UnityEngine.UI.Button settings = PillButton(content, "Settings", UiText.Settings.Key, size);
            UnityEngine.UI.Button toMenu = PillButton(content, "Menu", UiText.MainMenu.Key, size);
            float y = 124f;
            foreach (UnityEngine.UI.Button button in new[] { resume, restart, settings, toMenu })
            {
                ((RectTransform)button.transform).anchoredPosition = new Vector2(0f, y);
                y -= 112f;
            }

            var pauseScreen = screen.gameObject.AddComponent<PauseScreen>();
            Wire(pauseScreen, ("game", game), ("root", uiRoot), ("resumeButton", resume), ("restartButton", restart),
                ("settingsButton", settings), ("menuButton", toMenu));
            Pop(pauseScreen, content);
            return pauseScreen;
        }

        // ---------- Results ----------

        static GameOverScreen BuildGameOver(Transform parent, GameManager game, CollectionManager collection)
        {
            RectTransform screen = PanelScreen(parent, "GameOver", out RectTransform _, out RectTransform content, 0.7f);
            Panel(content, PanelCenter(860f), new Vector2(1060f, 860f));
            LocalizedLabel title = Header(content, UiText.GameOver.Key, HeaderY);

            TextMeshProUGUI scoreLabel = Text(Box(content, "ScoreLabel", new Vector2(0f, 262f), new Vector2(600f, 36f)), "", 26f, WithAlpha(Palette.Cream, 0.75f),
                TextAlignmentOptions.Center, bodyFont);
            scoreLabel.characterSpacing = 12f;
            Label(scoreLabel.gameObject, UiText.Score.Key, scoreLabel, null, Vector2.zero, true);
            TextMeshProUGUI score = Text(Box(content, "Score", new Vector2(0f, 156f), new Vector2(800f, 160f)), "0", 132f, Palette.White,
                TextAlignmentOptions.Center, bodyFont, bodyShadow);

            // Takes the record line's place (under the score), so it never covers a long score.
            RectTransform badge = Box(content, "NewBest", new Vector2(0f, 36f), new Vector2(340f, 66f));
            badge.localRotation = Quaternion.Euler(0f, 0f, 3f);
            Frame(badge, WithAlpha(Palette.Red, 0.92f), Palette.GoldLight, 33f);
            TextMeshProUGUI badgeText = Text(Stretch(badge, "Text", 10f, 2f, 10f, 0f), "", 30f, Color.white, TextAlignmentOptions.Center, bodyFont, bodyShadow);
            Label(badgeText.gameObject, UiText.NewBest.Key, badgeText, null, Vector2.zero, true);
            badge.gameObject.AddComponent<UiPulse>();

            TextMeshProUGUI best = Text(Box(content, "Best", new Vector2(0f, 36f), new Vector2(700f, 50f)), string.Format(UiText.BestFormat.Kk, 0), 32f,
                Palette.GoldLight, TextAlignmentOptions.Center, bodyFont, bodyShadow);
            TextMeshProUGUI sliced = Text(Box(content, "Sliced", new Vector2(-220f, -44f), new Vector2(420f, 44f)), string.Format(UiText.SlicedFormat.Kk, 0), 28f,
                Palette.Cream, TextAlignmentOptions.Center, bodyFont);
            TextMeshProUGUI combo = Text(Box(content, "BestCombo", new Vector2(220f, -44f), new Vector2(420f, 44f)), string.Format(UiText.BestComboFormat.Kk, 0), 28f,
                Palette.Cream, TextAlignmentOptions.Center, bodyFont);
            Image(Box(content, "StatsDivider", new Vector2(0f, -44f), new Vector2(2f, 40f)), WithAlpha(Palette.Gold, 0.5f), null);

            RectTransform unlock = Box(content, "Unlock", new Vector2(0f, -128f), new Vector2(860f, 64f));
            Tile(unlock, 32f, 0.9f);
            TextMeshProUGUI unlockText = Text(Stretch(unlock, "Text", 20f, 2f, 20f, 0f), "", 28f, Palette.GoldLight, TextAlignmentOptions.Center, bodyFont);
            ShrinkToFit(unlockText, 18f);
            unlock.gameObject.AddComponent<UiPulse>();
            unlock.gameObject.SetActive(false);

            UnityEngine.UI.Button again = PillButton(content, "Again", UiText.PlayAgain.Key, new Vector2(420f, 100f), false, true);
            ((RectTransform)again.transform).anchoredPosition = new Vector2(-220f, -272f);
            UnityEngine.UI.Button toMenu = PillButton(content, "Menu", UiText.MainMenu.Key, new Vector2(380f, 100f));
            ((RectTransform)toMenu.transform).anchoredPosition = new Vector2(220f, -272f);

            var gameOver = screen.gameObject.AddComponent<GameOverScreen>();
            Wire(gameOver, ("game", game), ("collection", collection), ("title", title), ("scoreText", score), ("bestText", best),
                ("newBestBadge", badge.gameObject), ("slicedText", sliced), ("comboText", combo), ("unlockGroup", unlock.gameObject),
                ("unlockText", unlockText), ("againButton", again), ("menuButton", toMenu));
            Pop(gameOver, content);
            return gameOver;
        }
    }
}
