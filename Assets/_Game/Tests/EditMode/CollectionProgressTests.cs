using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class CollectionProgressTests
    {
        static readonly int[] Thresholds = { 10, 100, 500 };
        static readonly string[] Cards = { "baursak", "kazy", "kurt" };

        static CollectionProgress Create(SaveData save) => new(save, Thresholds, Cards);

        static void Slice(CollectionProgress progress, string id, int times)
        {
            for (int i = 0; i < times; i++) progress.RegisterSlice(id);
        }

        [Test]
        public void CardIsRevealedAtTheFirstThreshold()
        {
            var progress = Create(new SaveData());
            Slice(progress, "baursak", 9);
            Assert.That(progress.GetLevel("baursak"), Is.Zero);
            Assert.That(progress.NextThreshold("baursak"), Is.EqualTo(10));

            Assert.That(progress.RegisterSlice("baursak"), Is.EqualTo(1), "the tenth slice reveals the card");
            Assert.That(progress.RegisterSlice("baursak"), Is.Zero, "no new level on the eleventh");
            Assert.That(progress.NextThreshold("baursak"), Is.EqualTo(100));
        }

        [Test]
        public void TopLevelHasNoNextThreshold()
        {
            var progress = Create(new SaveData());
            Slice(progress, "kurt", 499);

            Assert.That(progress.RegisterSlice("kurt"), Is.EqualTo(3));
            Assert.That(progress.NextThreshold("kurt"), Is.EqualTo(-1));
            Slice(progress, "kurt", 100);
            Assert.That(progress.GetLevel("kurt"), Is.EqualTo(3));
        }

        [Test]
        public void CardsCollected_CountsRevealedCardsOnly()
        {
            var progress = Create(new SaveData());
            Slice(progress, "baursak", 10);
            Slice(progress, "kazy", 3);
            Slice(progress, "not_a_card", 50);

            Assert.That(progress.CardsCollected, Is.EqualTo(1));
            Assert.That(progress.TotalSlices, Is.EqualTo(63));
        }

        [Test]
        public void UnlockRequirements()
        {
            var progress = Create(new SaveData());
            Slice(progress, "baursak", 10);
            Slice(progress, "kazy", 10);

            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.Free, 0)), Is.True);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.CardsCollected, 2)), Is.True);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.CardsCollected, 3)), Is.False);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.TotalSlices, 20)), Is.True);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.TotalSlices, 21)), Is.False);
        }

        [Test]
        public void AchievementRequirements()
        {
            var save = new SaveData();
            var progress = Create(save);
            save.TrySetBest(GameModeType.Classic, 120);
            save.TrySetBest(GameModeType.Arcade, 150);
            Assert.That(save.TrySetBestCombo(5), Is.True);
            Assert.That(save.TrySetBestCombo(4), Is.False, "a shorter combo does not replace the best");

            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.ClassicScore, 100)), Is.True);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.ArcadeScore, 200)), Is.False);
            Assert.That(progress.Current(new UnlockRequirement(UnlockKind.ArcadeScore, 200)), Is.EqualTo(150));
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.BestCombo, 5)), Is.True);
            Assert.That(progress.IsUnlocked(new UnlockRequirement(UnlockKind.BestCombo, 7)), Is.False);
        }

        [Test]
        public void Progress_SurvivesAJsonRoundTrip()
        {
            var save = new SaveData { bladeId = "kylysh" };
            Slice(Create(save), "kazy", 12);

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.That(Create(loaded).GetLevel("kazy"), Is.EqualTo(1));
            Assert.That(loaded.GetCount("kazy"), Is.EqualTo(12));
            Assert.That(loaded.totalSliced, Is.EqualTo(12));
            Assert.That(loaded.bladeId, Is.EqualTo("kylysh"));
        }

        [Test]
        public void SaveFromBeforeTheCollection_StartsEmpty()
        {
            var loaded = JsonUtility.FromJson<SaveData>("{\"version\":1,\"bestScores\":[5,7]}");

            Assert.That(loaded.GetBest(GameModeType.Arcade), Is.EqualTo(7));
            Assert.That(Create(loaded).CardsCollected, Is.Zero);
            Assert.That(Create(loaded).RegisterSlice("kurt"), Is.Zero);
        }
    }
}
