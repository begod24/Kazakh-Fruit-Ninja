using System;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>How often and what the spawner throws. Game modes change it as a round goes on.</summary>
    [Serializable]
    public struct SpawnPacing
    {
        [Tooltip("Seconds between waves (random in range).")]
        public Vector2 waveInterval;
        public Vector2Int foodPerWave;
        [Range(0f, 1f)] public float bombChance;
        [Tooltip("Chance that a wave ends with the golden aport (never two at once).")]
        [Range(0f, 1f)] public float goldenChance;
        [Tooltip("Chance that a wave contains a power-up (Arcade).")]
        [Range(0f, 1f)] public float powerUpChance;
        [Tooltip("Most food (bombs and bonuses included) in the air at once; new throws wait for room. 0 = no limit.")]
        [Min(0)] public int maxInFlight;

        public static SpawnPacing Default => new()
        {
            waveInterval = new Vector2(1.2f, 2.4f),
            foodPerWave = new Vector2Int(1, 4),
            bombChance = 0.25f,
            goldenChance = 0.06f,
        };
    }
}
