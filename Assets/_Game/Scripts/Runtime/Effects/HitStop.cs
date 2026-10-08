using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// A split-second slowdown of game time on big hits, so they land with weight. Time dips to
    /// <see cref="LowestScale"/> and eases back to full speed; it never stops dead, because a full freeze
    /// reads as the game stuttering, above all on a phone.
    /// The <see cref="GameManager"/> multiplies its own time scale by <see cref="Factor"/> every frame.
    /// </summary>
    public static class HitStop
    {
        const float LowestScale = 0.3f;

        static float start;
        static float until;

        /// <summary>Slows the game for <paramref name="seconds"/> of real time, unless the player turned it off.</summary>
        public static void Trigger(float seconds)
        {
            if (!GameSettings.HitStop || seconds <= 0f) return;
            float now = Time.unscaledTime;
            if (now + seconds <= until) return; // the one under way lasts longer
            start = now;
            until = now + seconds;
        }

        public static float Factor
        {
            get
            {
                float now = Time.unscaledTime;
                return now < until ? Mathf.SmoothStep(LowestScale, 1f, (now - start) / (until - start)) : 1f;
            }
        }

        public static void Cancel() => until = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => start = until = 0f;
    }
}
