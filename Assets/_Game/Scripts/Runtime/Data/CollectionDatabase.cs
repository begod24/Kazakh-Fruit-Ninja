using UnityEngine;

namespace KazakhNinja
{
    /// <summary>What can be collected (dish cards) and unlocked (blades).</summary>
    [CreateAssetMenu(fileName = "CollectionDatabase", menuName = "Kazakh Ninja/Collection Database")]
    public sealed class CollectionDatabase : ScriptableObject
    {
        [Tooltip("Dishes with a card on the Дастархан, in display order.")]
        public FoodDefinition[] cards = new FoodDefinition[0];
        [Tooltip("Slices of one dish needed for each card level: the first reveals the card and puts the dish on the table.")]
        public int[] thresholds = { 10, 100, 500 };
        [Tooltip("In display order; the first unlocked blade is the default.")]
        public BladeDefinition[] blades = new BladeDefinition[0];

        public bool IsCard(FoodDefinition food) => System.Array.IndexOf(cards, food) >= 0;
    }
}
