using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class FoodDatabaseTests
    {
        FoodDatabase database;
        FoodDefinition common;
        FoodDefinition never;

        [SetUp]
        public void SetUp()
        {
            common = ScriptableObject.CreateInstance<FoodDefinition>();
            common.spawnWeight = 1f;
            never = ScriptableObject.CreateInstance<FoodDefinition>();
            never.spawnWeight = 0f;
            database = ScriptableObject.CreateInstance<FoodDatabase>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(common);
            Object.DestroyImmediate(never);
        }

        [Test]
        public void PickRandom_SkipsZeroWeightAndMissingEntries()
        {
            database.foods = new[] { never, null, common, never };

            for (int i = 0; i < 200; i++) Assert.That(database.PickRandom(), Is.SameAs(common));
        }

        [Test]
        public void PickRandom_ReturnsNullWhenNothingCanSpawn()
        {
            database.foods = new[] { never, null };

            Assert.That(database.PickRandom(), Is.Null);
        }
    }
}
