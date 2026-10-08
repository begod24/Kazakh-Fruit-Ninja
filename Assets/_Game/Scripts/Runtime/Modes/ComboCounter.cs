using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Groups slices that follow each other quickly into one stroke;
    /// a stroke of <see cref="MinCombo"/> or more foods is a combo.
    /// </summary>
    public sealed class ComboCounter
    {
        public const int MinCombo = 3;

        readonly float window;
        int count;
        float lastTime;
        Vector3 positionSum;

        /// <param name="window">Longest pause between two slices of the same stroke, in seconds.</param>
        public ComboCounter(float window) => this.window = window;

        public int Count => count;

        public void Register(float time, Vector3 position)
        {
            if (count > 0 && time - lastTime > window) Reset();
            count++;
            lastTime = time;
            positionSum += position;
        }

        /// <summary>Once the stroke is over, reports it if it was a combo and starts a new one.</summary>
        public bool TryComplete(float time, out int comboCount, out Vector3 position)
        {
            comboCount = count;
            position = count > 0 ? positionSum / count : Vector3.zero;
            if (count == 0 || time - lastTime <= window) return false;
            Reset();
            return comboCount >= MinCombo;
        }

        public void Reset()
        {
            count = 0;
            positionSum = Vector3.zero;
        }
    }
}
