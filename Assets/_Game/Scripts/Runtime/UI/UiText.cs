using System.Collections.Generic;

namespace KazakhNinja
{
    /// <summary>
    /// Every player-facing text in English, Kazakh and Russian. Labels built by the UI builder refer to a phrase by
    /// its key (see <see cref="LocalizedLabel"/>); screens that fill in numbers use the phrases directly.
    /// </summary>
    public static class UiText
    {
        static readonly Dictionary<string, Phrase> table = new();

        static Phrase P(string key, string en, string kk, string ru)
        {
            var phrase = new Phrase(key, en, kk, ru);
            table[key] = phrase;
            return phrase;
        }

        public static Phrase Find(string key) => key != null && table.TryGetValue(key, out Phrase phrase) ? phrase : null;

        /// <summary>Every phrase, e.g. to draw all their letters into the font atlas up front.</summary>
        public static IEnumerable<Phrase> All => table.Values;

        /// <summary>The game's name, also used as the product name.</summary>
        public const string GameName = "Qazaq Slash";
        /// <summary>The name as the title shows it, and the gold line under it.</summary>
        public const string Title = "QAZAQ SLASH";
        public const string TitleKk = "ҚАЗАҚ SLASH";

        // ---------- Main menu ----------
        public static readonly Phrase SliceToPlay = P("slice_to_play", "SLICE TO PLAY", "ОЙНАУ ҮШІН КЕС!", "РЕЖЬ, ЧТОБЫ ИГРАТЬ!");
        public static readonly Phrase Blades = P("blades", "BLADES", "ҚЫЛЫШТАР", "КЛИНКИ");
        public static readonly Phrase Records = P("records", "RECORDS", "РЕКОРДТАР", "РЕКОРДЫ");
        public static readonly Phrase Settings = P("settings", "SETTINGS", "БАПТАУЛАР", "НАСТРОЙКИ");

        // ---------- Mode choice ----------
        public static readonly Phrase ChooseMode = P("choose_mode", "CHOOSE GAME MODE", "ОЙЫН РЕЖИМІН ТАҢДАУ", "ВЫБЕРИ РЕЖИМ ИГРЫ");
        public static readonly Phrase ClassicGame = P("classic_game", "CLASSIC GAME", "КЛАССИКАЛЫҚ ОЙЫН", "КЛАССИЧЕСКАЯ ИГРА");
        public static readonly Phrase ArcadeGame = P("arcade_game", "ARCADE GAME", "АРКАДА ОЙЫНЫ", "АРКАДНАЯ ИГРА");
        public static readonly Phrase ClassicInfo = P("classic_info", "3 lives · don't cut the pepper!", "3 өмір · бұрышты кеспе!", "3 жизни · не режь перец!");
        public static readonly Phrase ArcadeInfo = P("arcade_info", "60 seconds · catch the bonuses!", "60 секунд · бонустарды жина!", "60 секунд · лови бонусы!");
        public static readonly Phrase GoBack = P("go_back", "GO BACK", "АРТҚА ОРАЛУ", "НАЗАД");
        /// <summary>Format: best score.</summary>
        public static readonly Phrase BestFormat = P("best_format", "Best: {0}", "Рекорд: {0}", "Рекорд: {0}");
        public static readonly Phrase BestShortFormat = P("best_short", "BEST {0}", "РЕКОРД {0}", "РЕКОРД {0}");

        // ---------- Blades ----------
        public static readonly Phrase BladesHint = P("blades_hint", "Achievements unlock new blades", "Жаңа қылыштар жетістік үшін ашылады", "Новые клинки открываются за достижения");
        public static readonly Phrase Selected = P("selected", "Selected", "Таңдалды", "Выбран");
        public static readonly Phrase Select = P("select", "Tap to choose", "Таңдау үшін бас", "Нажми, чтобы выбрать");
        public static readonly Phrase Unlocked = P("unlocked", "Unlocked", "Ашылды", "Открыт");
        static readonly Phrase NeedCards = P("need_cards", "Collect {0} cards", "{0} карта жина", "Собери {0} карточек");
        static readonly Phrase NeedAllCards = P("need_all_cards", "Collect all {0} cards", "Барлық {0} картаны жина", "Собери все {0} карточек");
        static readonly Phrase NeedSlices = P("need_slices", "Slice {0} foods", "{0} тағам кес", "Разрежь {0} блюд");
        static readonly Phrase NeedClassic = P("need_classic", "Score {0} in Classic", "Классикада {0} ұпай жина", "Набери {0} очков в классике");
        static readonly Phrase NeedArcade = P("need_arcade", "Score {0} in Arcade", "Аркадада {0} ұпай жина", "Набери {0} очков в аркаде");
        static readonly Phrase NeedCombo = P("need_combo", "Make a ×{0} combo", "×{0} комбо жаса", "Сделай комбо ×{0}");
        /// <summary>Format: progress, goal.</summary>
        public static readonly Phrase ProgressFormat = P("progress", "{0}/{1}", "{0}/{1}", "{0}/{1}");
        public static readonly Phrase NewBladeFormat = P("new_blade", "New blade: {0}", "Жаңа қылыш: {0}", "Новый клинок: {0}");

        // ---------- Records ----------
        public static readonly Phrase Classic = P("classic", "CLASSIC", "КЛАССИКА", "КЛАССИКА");
        public static readonly Phrase Arcade = P("arcade", "ARCADE", "АРКАДА", "АРКАДА");
        public static readonly Phrase BestScore = P("best_score", "Best score", "Үздік ұпай", "Лучший счёт");
        public static readonly Phrase GamesFormat = P("games_format", "Games played: {0}", "Ойын саны: {0}", "Сыграно игр: {0}");
        public static readonly Phrase TotalSliced = P("total_sliced", "Foods sliced", "Барлығы кесілді", "Всего нарезано");
        public static readonly Phrase BestCombo = P("best_combo", "Best combo", "Үздік комбо", "Лучшее комбо");
        public static readonly Phrase Cards = P("cards", "Cards", "Карталар", "Карточки");
        public static readonly Phrase Dastarkhan = P("dastarkhan", "DASTARKHAN", "ДАСТАРХАН", "ДАСТАРХАН");
        public static readonly Phrase DastarkhanHint = P("dastarkhan_hint", "How much of each dish you have sliced", "Қай тағамды қанша кестің", "Сколько каждого блюда ты нарезал");
        public static readonly Phrase CountFormat = P("count_format", "Sliced: {0}", "Кесілді: {0}", "Нарезано: {0}");
        public static readonly Phrase Mastered = P("mastered", "Master!", "Шебер!", "Мастер!");
        /// <summary>Format: slices needed to reveal the card.</summary>
        public static readonly Phrase RevealFormat = P("reveal_format", "Slice it {0} times to open the card.", "Картаны ашу үшін {0} рет кес.", "Разрежь {0} раз, чтобы открыть карточку.");

        // ---------- Settings ----------
        public static readonly Phrase ScreenShake = P("screen_shake", "Screen shake", "Экран дірілі", "Тряска экрана");
        public static readonly Phrase HitStop = P("hit_stop", "Slow-down on hits", "Соққы кідірісі", "Замедление при ударе");
        public static readonly Phrase Graphics = P("graphics", "Graphics", "Графика", "Графика");
        public static readonly Phrase LanguageLabel = P("language", "Language", "Тіл", "Язык");
        public static readonly Phrase On = P("on", "On", "Қосулы", "Вкл.");
        public static readonly Phrase Off = P("off", "Off", "Өшірулі", "Выкл.");
        public static readonly Phrase GraphicsHigh = P("graphics_high", "High", "Жоғары", "Высокая");
        public static readonly Phrase GraphicsLow = P("graphics_low", "Low", "Төмен", "Низкая");

        // ---------- Pause ----------
        public static readonly Phrase Paused = P("paused", "PAUSED", "ҮЗІЛІС", "ПАУЗА");
        public static readonly Phrase Resume = P("resume", "RESUME", "ЖАЛҒАСТЫРУ", "ПРОДОЛЖИТЬ");
        public static readonly Phrase Restart = P("restart", "RESTART", "ҚАЙТА БАСТАУ", "ЗАНОВО");
        public static readonly Phrase MainMenu = P("main_menu", "MAIN MENU", "БАСТЫ БЕТ", "ГЛАВНОЕ МЕНЮ");

        // ---------- Results ----------
        public static readonly Phrase GameOver = P("game_over", "GAME OVER", "ОЙЫН АЯҚТАЛДЫ", "ИГРА ОКОНЧЕНА");
        public static readonly Phrase BombOver = P("bomb_over", "BOOM! HOT PEPPER!", "БУМ! АЩЫ БҰРЫШ!", "БУМ! ОСТРЫЙ ПЕРЕЦ!");
        public static readonly Phrase TimeUp = P("time_up", "TIME'S UP!", "УАҚЫТ БІТТІ!", "ВРЕМЯ ВЫШЛО!");
        public static readonly Phrase Score = P("score", "SCORE", "ҰПАЙ", "СЧЁТ");
        public static readonly Phrase NewBest = P("new_best", "New record!", "Жаңа рекорд!", "Новый рекорд!");
        public static readonly Phrase SlicedFormat = P("sliced_format", "Sliced: {0}", "Кесілді: {0}", "Нарезано: {0}");
        public static readonly Phrase BestComboFormat = P("best_combo_format", "Best combo: {0}", "Үздік комбо: {0}", "Лучшее комбо: {0}");
        public static readonly Phrase PlayAgain = P("play_again", "PLAY AGAIN", "ТАҒЫ ДА", "ЕЩЁ РАЗ");

        // ---------- Popups ----------
        public static readonly Phrase ComboFormat = P("combo_format", "{0} COMBO +{1}", "{0} КОМБО +{1}", "{0} КОМБО +{1}");
        public const string LostLife = "X";
        public static readonly Phrase BombLifeLost = P("bomb_life_lost", "-1 life", "-1 өмір", "-1 жизнь");
        static readonly Phrase Praise3 = P("praise_3", "Nice!", "Жарайсың!", "Отлично!");
        static readonly Phrase Praise4 = P("praise_4", "Great!", "Керемет!", "Здорово!");
        static readonly Phrase Praise5 = P("praise_5", "Amazing!", "Тамаша!", "Потрясающе!");
        static readonly Phrase NewCard = P("new_card", "New card!", "Жаңа карта!", "Новая карточка!");
        static readonly Phrase SilverCard = P("silver_card", "Silver card!", "Күміс карта!", "Серебряная карточка!");
        static readonly Phrase GoldCard = P("gold_card", "Gold card!", "Алтын карта!", "Золотая карточка!");
        static readonly Phrase KymyzName = P("kymyz_name", "Kymyz", "Қымыз", "Кумыс");
        static readonly Phrase ToyName = P("toy_name", "Toi ×2", "Той ×2", "Той ×2");
        static readonly Phrase NauryzName = P("nauryz_name", "Nauryz", "Наурыз", "Наурыз");
        static readonly Phrase KymyzNews = P("kymyz_news", "Kymyz! Slow time", "Қымыз! Баяу уақыт", "Кумыс! Замедление");
        static readonly Phrase ToyNews = P("toy_news", "Toi! Double points", "Той! Екі есе ұпай", "Той! Двойные очки");
        static readonly Phrase NauryzNews = P("nauryz_news", "Nauryz! Food rain", "Наурыз! Тағам жаңбыры", "Наурыз! Дождь из еды");

        public static string CardLevelUp(int level, string name) => level switch
        {
            1 => $"{NewCard.Text}\n<size=60%>{name}</size>",
            2 => $"{name}\n<size=60%>{SilverCard.Text}</size>",
            _ => $"{name}\n<size=60%>{GoldCard.Text}</size>",
        };

        /// <param name="cardCount">Cards in the collection, to say "all" when that is what is needed.</param>
        public static string Requirement(UnlockRequirement requirement, int cardCount = -1) => requirement.kind switch
        {
            UnlockKind.CardsCollected when requirement.amount == cardCount => NeedAllCards.Format(requirement.amount),
            UnlockKind.CardsCollected => NeedCards.Format(requirement.amount),
            UnlockKind.TotalSlices => NeedSlices.Format(requirement.amount),
            UnlockKind.ClassicScore => NeedClassic.Format(requirement.amount),
            UnlockKind.ArcadeScore => NeedArcade.Format(requirement.amount),
            UnlockKind.BestCombo => NeedCombo.Format(requirement.amount),
            _ => "",
        };

        public static string ComboPraise(int count) => (count >= 5 ? Praise5 : count == 4 ? Praise4 : Praise3).Text;

        public static Phrase GameOverTitle(GameOverReason reason) => reason switch
        {
            GameOverReason.Bomb => BombOver,
            GameOverReason.TimeUp => TimeUp,
            _ => GameOver,
        };

        public static string PowerUpName(PowerUpType type) => (type switch
        {
            PowerUpType.SlowTime => KymyzName,
            PowerUpType.DoubleScore => ToyName,
            _ => NauryzName,
        }).Text;

        public static string PowerUpAnnouncement(PowerUpType type) => (type switch
        {
            PowerUpType.SlowTime => KymyzNews,
            PowerUpType.DoubleScore => ToyNews,
            _ => NauryzNews,
        }).Text;

        // ---------- Content names ----------

        public static string Name(FoodDefinition food) => Loc.Text(food.nameEn, food.nameKk, food.nameRu);
        public static string Fact(FoodDefinition food) => Loc.Text(food.factEn, food.factKk, food.factRu);
        public static string Name(BladeDefinition blade) => Loc.Text(blade.nameEn, blade.nameKk, blade.nameRu);

        /// <summary>The food's name in the two languages the player is not reading, e.g. "Baursak · Баурсак".</summary>
        public static string OtherNames(FoodDefinition food)
        {
            string shown = Name(food);
            var others = new List<string>(2);
            foreach (string name in new[] { food.nameKk, food.nameRu, food.nameEn })
                if (!string.IsNullOrEmpty(name) && name != shown && !others.Contains(name)) others.Add(name);
            return string.Join(" · ", others);
        }
    }
}
