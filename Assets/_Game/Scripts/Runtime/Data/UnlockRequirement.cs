using System;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>What a blade's achievement asks for. Stored as a number in assets: only add new kinds at the end.</summary>
    public enum UnlockKind
    {
        /// <summary>Available from the start.</summary>
        Free,
        /// <summary>Needs this many collection cards (dishes sliced at least the first threshold).</summary>
        CardsCollected,
        /// <summary>Needs this many foods sliced in rounds, in total.</summary>
        TotalSlices,
        /// <summary>Needs a Classic record of at least this many points.</summary>
        ClassicScore,
        /// <summary>Needs an Arcade record of at least this many points.</summary>
        ArcadeScore,
        /// <summary>Needs a combo of at least this many foods in one stroke.</summary>
        BestCombo,
    }

    [Serializable]
    public struct UnlockRequirement
    {
        public UnlockKind kind;
        [Min(0)] public int amount;

        public UnlockRequirement(UnlockKind kind, int amount)
        {
            this.kind = kind;
            this.amount = amount;
        }
    }
}
