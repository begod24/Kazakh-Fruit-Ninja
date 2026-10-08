using System;
using UnityEngine;

namespace KazakhNinja
{
    public readonly struct FoodHitEvent
    {
        public readonly FoodDefinition Food;
        public readonly Vector3 Position;
        public readonly Vector3 BladeDirection;
        /// <summary>1-based hit count on a golden food; 0 otherwise.</summary>
        public readonly int HitIndex;

        public FoodHitEvent(FoodDefinition food, Vector3 position, Vector3 bladeDirection, int hitIndex = 0)
        {
            Food = food;
            Position = position;
            BladeDirection = bladeDirection;
            HitIndex = hitIndex;
        }
    }

    /// <summary>
    /// Gameplay notifications. Score, HUD, effects, audio and the collection listen here,
    /// so the core loop never needs to know about them.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>A food (including a finished golden one) was cut in two.</summary>
        public static event Action<FoodHitEvent> FoodSliced;
        /// <summary>A golden food took one more hit.</summary>
        public static event Action<FoodHitEvent> GoldenHit;
        public static event Action<FoodHitEvent> BombHit;
        /// <summary>A regular food fell off the screen uncut; the position is where it left.</summary>
        public static event Action<FoodHitEvent> FoodMissed;

        public static void RaiseFoodSliced(in FoodHitEvent e) => FoodSliced?.Invoke(e);
        public static void RaiseGoldenHit(in FoodHitEvent e) => GoldenHit?.Invoke(e);
        public static void RaiseBombHit(in FoodHitEvent e) => BombHit?.Invoke(e);
        public static void RaiseFoodMissed(in FoodHitEvent e) => FoodMissed?.Invoke(e);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FoodSliced = null;
            GoldenHit = null;
            BombHit = null;
            FoodMissed = null;
        }
    }
}
