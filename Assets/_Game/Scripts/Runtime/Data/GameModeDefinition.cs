using System;
using UnityEngine;

namespace KazakhNinja
{
    public enum GameModeType
    {
        Classic,
        Arcade,
    }

    public enum BombEffect
    {
        /// <summary>The round is over at once (classic Fruit Ninja).</summary>
        EndsRound,
        /// <summary>Costs one of the shared lives; the round ends when they run out.</summary>
        CostsLife,
        /// <summary>Costs <see cref="GameRules.bombPenalty"/> points.</summary>
        CostsPoints,
    }

    [Serializable]
    public struct GameRules
    {
        [Tooltip("0 = no lives, misses are free.")]
        [Min(0)] public int lives;
        [Tooltip("Round length in seconds; 0 = no time limit.")]
        [Min(0f)] public float duration;
        [Tooltip("What cutting the bomb pepper does.")]
        public BombEffect bomb;
        [Tooltip("Points lost per bomb with Costs Points.")]
        [Min(0)] public int bombPenalty;
        public bool powerUpsEnabled;
    }

    [CreateAssetMenu(fileName = "Mode_", menuName = "Kazakh Ninja/Game Mode")]
    public sealed class GameModeDefinition : ScriptableObject
    {
        public GameModeType type;
        public GameRules rules;

        [Header("Pacing — x axis is seconds since the round started")]
        [Tooltip("Average seconds between waves.")]
        public AnimationCurve waveInterval = AnimationCurve.Constant(0f, 1f, 1.8f);
        [Tooltip("Largest wave size.")]
        public AnimationCurve foodPerWave = AnimationCurve.Constant(0f, 1f, 3f);
        public AnimationCurve bombChance = AnimationCurve.Constant(0f, 1f, 0.2f);
        [Range(0f, 1f)] public float goldenChance = 0.05f;
        [Range(0f, 1f)] public float powerUpChance;
        [Tooltip("Most food (bombs and bonuses included) in the air at once, so a small screen never gets crowded; new throws wait for room. 0 = no limit.")]
        [Min(0)] public int maxInFlight;

        [Header("Backdrop")]
        [Tooltip("Seconds into the round at which the backdrop moves on to its next scene, so the day turns to night as it gets harder.")]
        public float[] backgroundTimes = new float[0];

        /// <summary>Index of the backdrop scene for <paramref name="elapsed"/> seconds into the round.</summary>
        public int BackgroundAt(float elapsed)
        {
            int index = 0;
            if (backgroundTimes == null) return index;
            while (index < backgroundTimes.Length && elapsed >= backgroundTimes[index]) index++;
            return index;
        }

        public SpawnPacing Evaluate(float elapsed)
        {
            float interval = Mathf.Max(0.25f, waveInterval.Evaluate(elapsed));
            int maxFood = Mathf.Max(1, Mathf.RoundToInt(foodPerWave.Evaluate(elapsed)));
            return new SpawnPacing
            {
                waveInterval = new Vector2(interval * 0.8f, interval * 1.2f),
                foodPerWave = new Vector2Int(Mathf.Max(1, maxFood - 2), maxFood),
                bombChance = Mathf.Clamp01(bombChance.Evaluate(elapsed)),
                goldenChance = goldenChance,
                powerUpChance = rules.powerUpsEnabled ? powerUpChance : 0f,
                maxInFlight = maxInFlight,
            };
        }
    }
}
