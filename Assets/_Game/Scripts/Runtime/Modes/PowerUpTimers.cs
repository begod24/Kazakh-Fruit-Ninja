using UnityEngine;

namespace KazakhNinja
{
    public enum PowerUpType
    {
        /// <summary>Қымыз: slow motion.</summary>
        SlowTime,
        /// <summary>Той: double points.</summary>
        DoubleScore,
        /// <summary>Наурыз: food pours in from the sides.</summary>
        Frenzy,
    }

    /// <summary>Remaining time of each power-up. Catching one that is already active refreshes it.</summary>
    public sealed class PowerUpTimers
    {
        public const int Count = 3;

        readonly float[] remaining = new float[Count];
        readonly float[] duration = new float[Count];

        public void Activate(PowerUpType type, float seconds)
        {
            int i = (int)type;
            if (seconds <= remaining[i]) return;
            remaining[i] = seconds;
            duration[i] = seconds;
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < Count; i++) remaining[i] = Mathf.Max(0f, remaining[i] - deltaTime);
        }

        public bool IsActive(PowerUpType type) => remaining[(int)type] > 0f;
        public float Remaining(PowerUpType type) => remaining[(int)type];

        /// <summary>Remaining time as a fraction of the last activation, 0..1.</summary>
        public float Fraction(PowerUpType type)
        {
            int i = (int)type;
            return duration[i] > 0f ? remaining[i] / duration[i] : 0f;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) remaining[i] = duration[i] = 0f;
        }
    }
}
