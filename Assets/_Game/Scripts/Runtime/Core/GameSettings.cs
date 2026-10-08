using System;
using UnityEngine;

namespace KazakhNinja
{
    public enum GraphicsQuality
    {
        /// <summary>Picked from the device on first launch.</summary>
        Auto,
        Low,
        High,
    }

    /// <summary>
    /// Player options kept in the save: the comfort toggles (screen shake, hit-stop) and the graphics level.
    /// Bound to the loaded save by the <see cref="GameManager"/>; whoever changes an option saves the file.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Phones with less memory than this start on low graphics (no bloom, fewer particles).</summary>
        const int HighGraphicsMemoryMb = 3000;

        static SaveData save;

        /// <summary>An option changed (or a save was bound); effects re-read what they use.</summary>
        public static event Action Changed;

        public static void Bind(SaveData data)
        {
            save = data;
            Changed?.Invoke();
        }

        public static bool ScreenShake
        {
            get => save == null || save.screenShake;
            set
            {
                if (save == null || save.screenShake == value) return;
                save.screenShake = value;
                Changed?.Invoke();
            }
        }

        /// <summary>The split-second slowdown on big hits (see <see cref="KazakhNinja.HitStop"/>).</summary>
        public static bool HitStop
        {
            get => save == null || save.hitStop;
            set
            {
                if (save == null || save.hitStop == value) return;
                save.hitStop = value;
                Changed?.Invoke();
            }
        }

        public static GraphicsQuality Graphics
        {
            get => save != null ? (GraphicsQuality)save.graphics : GraphicsQuality.Auto;
            set
            {
                if (save == null || save.graphics == (int)value) return;
                save.graphics = (int)value;
                Changed?.Invoke();
            }
        }

        /// <summary>Bloom and full particle counts.</summary>
        public static bool HighGraphics => Graphics switch
        {
            GraphicsQuality.Low => false,
            GraphicsQuality.High => true,
            _ => SystemInfo.systemMemorySize >= HighGraphicsMemoryMb,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            save = null;
            Changed = null;
        }
    }
}
