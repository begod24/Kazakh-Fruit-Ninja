using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazakhNinja
{
    public readonly struct CardProgressEvent
    {
        public readonly FoodDefinition Food;
        /// <summary>1 = new card, higher = better frame.</summary>
        public readonly int Level;
        public readonly Vector3 Position;

        public CardProgressEvent(FoodDefinition food, int level, Vector3 position)
        {
            Food = food;
            Level = level;
            Position = position;
        }
    }

    /// <summary>
    /// The Дастархан collection: counts dishes sliced during rounds, levels up their cards,
    /// unlocks blades through their achievements and applies the selected one.
    /// </summary>
    public sealed class CollectionManager : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] CollectionDatabase database;
        [SerializeField] BladeController blade;

        CollectionProgress progress;
        readonly List<BladeDefinition> unlocked = new();
        readonly List<BladeDefinition> unlockedThisRound = new();
        GameState lastState;

        public CollectionDatabase Database => database;
        public CollectionProgress Progress => progress ??= CreateProgress();
        /// <summary>Blades unlocked during the current (or last) round, for the results screen.</summary>
        public IReadOnlyList<BladeDefinition> UnlockedThisRound => unlockedThisRound;

        /// <summary>A card was revealed or reached a better frame during a round.</summary>
        public event Action<CardProgressEvent> CardProgressed;
        /// <summary>A blade's achievement was just reached.</summary>
        public event Action<BladeDefinition> BladeUnlocked;
        /// <summary>Anything shown by the collection screen changed.</summary>
        public event Action Changed;

        void OnEnable()
        {
            GameEvents.FoodSliced += OnFoodSliced;
            game.StateChanged += OnStateChanged;
            game.Feedback += OnFeedback;
            game.RoundRecorded += CheckUnlocks;
        }

        void OnDisable()
        {
            GameEvents.FoodSliced -= OnFoodSliced;
            game.StateChanged -= OnStateChanged;
            game.Feedback -= OnFeedback;
            game.RoundRecorded -= CheckUnlocks;
        }

        void Start()
        {
            RememberUnlocked();
            ApplyCosmetics();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveSystem.Save(game.Save);
        }

        CollectionProgress CreateProgress()
        {
            var ids = new List<string>();
            foreach (FoodDefinition card in database.cards)
                if (card != null) ids.Add(card.id);
            return new CollectionProgress(game.Save, database.thresholds, ids);
        }

        void OnFoodSliced(FoodHitEvent e)
        {
            // Slicing the food that flies behind the menu does not count.
            if (game.State != GameState.Playing || !database.IsCard(e.Food)) return;

            int level = Progress.RegisterSlice(e.Food.id);
            if (level > 0) CardProgressed?.Invoke(new CardProgressEvent(e.Food, level, e.Position));
            // Slice counts and cards open achievements even without a new card level.
            CheckUnlocks();
            if (level > 0) Changed?.Invoke();
        }

        void OnFeedback(GameFeedback feedback)
        {
            if (feedback.Kind == FeedbackKind.Combo) CheckUnlocks();
        }

        void OnStateChanged(GameState state)
        {
            // A new round (not a resume) starts a fresh list of unlocks.
            if (state == GameState.Playing && lastState != GameState.Paused) unlockedThisRound.Clear();
            lastState = state;
        }

        // ---------- Cosmetics ----------

        public bool IsUnlocked(BladeDefinition blade) => blade != null && Progress.IsUnlocked(blade.unlock);

        public BladeDefinition CurrentBlade => Pick(database.blades, game.Save.bladeId, b => b.id, IsUnlocked);

        public void Select(BladeDefinition choice)
        {
            if (!IsUnlocked(choice)) return;
            game.Save.bladeId = choice.id;
            SaveAndApply();
        }

        void SaveAndApply()
        {
            SaveSystem.Save(game.Save);
            ApplyCosmetics();
            Changed?.Invoke();
        }

        void ApplyCosmetics()
        {
            BladeDefinition currentBlade = CurrentBlade;
            if (blade && currentBlade) blade.ApplySkin(currentBlade);
        }

        /// <summary>The saved choice if it is still unlocked, otherwise the first unlocked item.</summary>
        static T Pick<T>(T[] items, string savedId, Func<T, string> id, Func<T, bool> unlocked) where T : ScriptableObject
        {
            T fallback = null;
            foreach (T item in items)
            {
                if (item == null || !unlocked(item)) continue;
                if (id(item) == savedId) return item;
                fallback ??= item;
            }
            return fallback;
        }

        void RememberUnlocked()
        {
            unlocked.Clear();
            foreach (BladeDefinition b in database.blades) if (IsUnlocked(b)) unlocked.Add(b);
        }

        /// <summary>Announces every blade whose achievement has been reached since the last check.</summary>
        void CheckUnlocks()
        {
            bool any = false;
            foreach (BladeDefinition b in database.blades)
            {
                if (b == null || unlocked.Contains(b) || !IsUnlocked(b)) continue;
                unlocked.Add(b);
                unlockedThisRound.Add(b);
                BladeUnlocked?.Invoke(b);
                any = true;
            }
            if (any) Changed?.Invoke();
        }
    }
}
