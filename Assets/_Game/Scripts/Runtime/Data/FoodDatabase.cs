using UnityEngine;

namespace KazakhNinja
{
    [CreateAssetMenu(fileName = "FoodDatabase", menuName = "Kazakh Ninja/Food Database")]
    public sealed class FoodDatabase : ScriptableObject
    {
        [Tooltip("Regular food, picked by spawn weight.")]
        public FoodDefinition[] foods = new FoodDefinition[0];
        public FoodDefinition bomb;
        public FoodDefinition golden;
        [Tooltip("Қымыз, Той, Наурыз.")]
        public FoodDefinition[] powerUps = new FoodDefinition[0];

        /// <summary>A random power-up, or null if there are none.</summary>
        public FoodDefinition PickPowerUp()
        {
            int count = 0;
            foreach (FoodDefinition p in powerUps) if (p != null) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            foreach (FoodDefinition p in powerUps)
            {
                if (p == null) continue;
                if (pick-- == 0) return p;
            }
            return null;
        }

        /// <summary>Weighted random pick by <see cref="FoodDefinition.spawnWeight"/>; null if nothing can spawn.</summary>
        public FoodDefinition PickRandom()
        {
            float total = 0f;
            foreach (FoodDefinition food in foods)
                if (food != null) total += food.spawnWeight;
            if (total <= 0f) return null;

            float roll = Random.value * total;
            FoodDefinition last = null;
            foreach (FoodDefinition food in foods)
            {
                if (food == null || food.spawnWeight <= 0f) continue;
                last = food;
                roll -= food.spawnWeight;
                if (roll < 0f) return food;
            }
            return last; // roll == total
        }
    }
}
