using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KazakhNinja
{
    [Serializable]
    public sealed class FoodCount
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 2;
        /// <summary>Indexed by <see cref="GameModeType"/>.</summary>
        public int[] bestScores = new int[2];
        /// <summary>Rounds finished, indexed by <see cref="GameModeType"/>.</summary>
        public int[] gamesPlayed = new int[2];
        /// <summary>Most foods cut in one stroke, in any mode.</summary>
        public int bestCombo;

        [Header("Collection")]
        public List<FoodCount> foodCounts = new();
        public int totalSliced;
        public string bladeId;

        [Header("Settings")]
        public bool screenShake = true;
        public bool hitStop = true;
        /// <summary>A <see cref="GraphicsQuality"/>.</summary>
        public int graphics;
        /// <summary>A <see cref="KazakhNinja.Language"/>; 0 is English with Kazakh under it.</summary>
        public int language;

        public int GetBest(GameModeType mode) => (int)mode < bestScores.Length ? bestScores[(int)mode] : 0;

        /// <summary>Stores <paramref name="score"/> if it beats the record; returns true when it does.</summary>
        public bool TrySetBest(GameModeType mode, int score)
        {
            int i = (int)mode;
            if (i >= bestScores.Length) Array.Resize(ref bestScores, i + 1);
            if (score <= bestScores[i]) return false;
            bestScores[i] = score;
            return true;
        }

        public int GetGamesPlayed(GameModeType mode) => gamesPlayed != null && (int)mode < gamesPlayed.Length ? gamesPlayed[(int)mode] : 0;

        public void AddGamePlayed(GameModeType mode)
        {
            int i = (int)mode;
            gamesPlayed ??= new int[2];
            if (i >= gamesPlayed.Length) Array.Resize(ref gamesPlayed, i + 1);
            gamesPlayed[i]++;
        }

        /// <summary>Keeps <paramref name="combo"/> if it is the longest yet; returns true when it is.</summary>
        public bool TrySetBestCombo(int combo)
        {
            if (combo <= bestCombo) return false;
            bestCombo = combo;
            return true;
        }

        public int GetCount(string id)
        {
            if (foodCounts == null) return 0;
            foreach (FoodCount entry in foodCounts)
                if (entry.id == id) return entry.count;
            return 0;
        }

        /// <summary>Adds one slice of <paramref name="id"/>; returns its new count.</summary>
        public int AddSlice(string id)
        {
            foodCounts ??= new List<FoodCount>();
            totalSliced++;
            foreach (FoodCount entry in foodCounts)
            {
                if (entry.id != id) continue;
                return ++entry.count;
            }
            foodCounts.Add(new FoodCount { id = id, count = 1 });
            return 1;
        }
    }

    /// <summary>JSON save in persistentDataPath. Never throws: a broken file means a fresh save.</summary>
    public static class SaveSystem
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem: could not read save, starting fresh. {e.Message}");
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            try
            {
                // Write-then-move so a crash mid-write cannot corrupt the existing save.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem: could not write save. {e.Message}");
            }
        }
    }
}
