using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// The records panel: the best score and rounds played in each mode, overall stats, and the Дастархан —
    /// how much of each dish has been sliced, with its card level and story.
    /// </summary>
    public sealed class RecordsScreen : UiScreen
    {
        [SerializeField] GameManager game;
        [SerializeField] CollectionManager collection;
        [SerializeField] UiRoot root;
        [SerializeField] UnityEngine.UI.Button backButton;

        [Header("Modes")]
        [SerializeField] TMP_Text classicBest;
        [SerializeField] TMP_Text classicGames;
        [SerializeField] TMP_Text arcadeBest;
        [SerializeField] TMP_Text arcadeGames;

        [Header("Stats")]
        [SerializeField] TMP_Text totalSliced;
        [SerializeField] TMP_Text bestCombo;
        [SerializeField] TMP_Text cardsCollected;

        [Header("Dishes")]
        [Tooltip("Inactive template, cloned once per dish.")]
        [SerializeField] CollectionCardView cardTemplate;
        [SerializeField] RectTransform cardGrid;
        [SerializeField] UnityEngine.UI.Image detailIcon;
        [SerializeField] TMP_Text detailName;
        [SerializeField] TMP_Text detailSubtitle;
        [SerializeField] TMP_Text detailFact;
        [SerializeField] TMP_Text detailCount;

        readonly List<CollectionCardView> cards = new();
        FoodDefinition selected;
        Sprite fallbackIcon;

        protected override void Awake()
        {
            base.Awake();
            fallbackIcon = detailIcon.sprite;
            backButton.onClick.AddListener(() => root.ShowPage(MenuPage.Home));
            cardTemplate.gameObject.SetActive(false);
            foreach (FoodDefinition food in collection.Database.cards)
            {
                if (food == null) continue;
                CollectionCardView card = Instantiate(cardTemplate, cardGrid);
                card.gameObject.SetActive(true);
                card.Button.onClick.AddListener(() =>
                {
                    selected = food;
                    Refresh();
                });
                cards.Add(card);
            }
        }

        void OnEnable() => Loc.Changed += Refresh;
        void OnDisable() => Loc.Changed -= Refresh;

        protected override void OnShow() => Refresh();

        void Refresh()
        {
            if (!IsVisible) return;
            SaveData save = game.Save;
            classicBest.SetText("{0}", save.GetBest(GameModeType.Classic));
            arcadeBest.SetText("{0}", save.GetBest(GameModeType.Arcade));
            classicGames.SetText(UiText.GamesFormat.Format(save.GetGamesPlayed(GameModeType.Classic)));
            arcadeGames.SetText(UiText.GamesFormat.Format(save.GetGamesPlayed(GameModeType.Arcade)));

            CollectionProgress progress = collection.Progress;
            totalSliced.SetText("{0}", progress.TotalSlices);
            bestCombo.SetText(save.bestCombo > 0 ? $"×{save.bestCombo}" : "—");
            cardsCollected.SetText("{0}/{1}", progress.CardsCollected, progress.CardCount);

            CollectionDatabase database = collection.Database;
            if (selected == null) selected = FirstCard(database);
            int c = 0;
            foreach (FoodDefinition food in database.cards)
            {
                if (food == null) continue;
                cards[c++].Bind(food, progress.GetCount(food.id), progress.GetLevel(food.id), progress.NextThreshold(food.id), food == selected);
            }
            ShowDetail(selected, progress);
        }

        void ShowDetail(FoodDefinition food, CollectionProgress progress)
        {
            if (food == null) return;
            bool unlocked = progress.GetLevel(food.id) > 0;
            detailIcon.sprite = food.icon != null ? food.icon : fallbackIcon;
            detailIcon.color = unlocked ? Color.white : new Color(0f, 0f, 0f, 0.55f);
            detailName.SetText(UiText.Name(food));
            detailSubtitle.SetText(unlocked ? UiText.OtherNames(food) : "");
            // The story is the reward for revealing the card.
            detailFact.SetText(unlocked ? UiText.Fact(food) : UiText.RevealFormat.Format(progress.NextThreshold(food.id)));
            detailCount.SetText(UiText.CountFormat.Format(progress.GetCount(food.id)));
        }

        static FoodDefinition FirstCard(CollectionDatabase database)
        {
            foreach (FoodDefinition food in database.cards)
                if (food != null) return food;
            return null;
        }
    }
}
