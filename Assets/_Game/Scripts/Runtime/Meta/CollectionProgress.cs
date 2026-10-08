using System.Collections.Generic;

namespace KazakhNinja
{
    /// <summary>
    /// Card levels and unlock rules on top of the save data. Plain C# (ids and numbers only)
    /// so the rules can be unit tested.
    /// </summary>
    public sealed class CollectionProgress
    {
        readonly SaveData save;
        readonly int[] thresholds;
        readonly IReadOnlyList<string> cardIds;

        public CollectionProgress(SaveData save, int[] thresholds, IReadOnlyList<string> cardIds)
        {
            this.save = save;
            this.thresholds = thresholds;
            this.cardIds = cardIds;
        }

        public int MaxLevel => thresholds.Length;
        public int CardCount => cardIds.Count;
        public int TotalSlices => save.totalSliced;

        public int GetCount(string id) => save.GetCount(id);

        /// <summary>0 = still locked; 1 = card revealed and dish on the table; higher = better frame.</summary>
        public int GetLevel(string id) => LevelFor(save.GetCount(id));

        /// <summary>Slices needed for the next level, or -1 at the top level.</summary>
        public int NextThreshold(string id)
        {
            int level = GetLevel(id);
            return level < thresholds.Length ? thresholds[level] : -1;
        }

        /// <summary>Cards with at least level 1.</summary>
        public int CardsCollected
        {
            get
            {
                int collected = 0;
                foreach (string id in cardIds)
                    if (GetLevel(id) > 0) collected++;
                return collected;
            }
        }

        /// <summary>Counts one slice; returns the level just reached, or 0 when no threshold was crossed.</summary>
        public int RegisterSlice(string id)
        {
            int before = GetLevel(id);
            int after = LevelFor(save.AddSlice(id));
            return after > before ? after : 0;
        }

        public bool IsUnlocked(UnlockRequirement requirement) =>
            requirement.kind == UnlockKind.Free || Current(requirement) >= requirement.amount;

        /// <summary>How far the player is towards <paramref name="requirement"/>, in its own units.</summary>
        public int Current(UnlockRequirement requirement) => requirement.kind switch
        {
            UnlockKind.CardsCollected => CardsCollected,
            UnlockKind.TotalSlices => TotalSlices,
            UnlockKind.ClassicScore => save.GetBest(GameModeType.Classic),
            UnlockKind.ArcadeScore => save.GetBest(GameModeType.Arcade),
            UnlockKind.BestCombo => save.bestCombo,
            _ => requirement.amount,
        };

        int LevelFor(int count)
        {
            int level = 0;
            while (level < thresholds.Length && count >= thresholds[level]) level++;
            return level;
        }
    }
}
