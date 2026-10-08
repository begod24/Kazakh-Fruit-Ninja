using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class SaveDataTests
    {
        [Test]
        public void TrySetBest_OnlyAcceptsHigherScores()
        {
            var data = new SaveData();

            Assert.That(data.TrySetBest(GameModeType.Arcade, 40), Is.True);
            Assert.That(data.TrySetBest(GameModeType.Arcade, 40), Is.False);
            Assert.That(data.TrySetBest(GameModeType.Arcade, 12), Is.False);
            Assert.That(data.GetBest(GameModeType.Arcade), Is.EqualTo(40));
            Assert.That(data.GetBest(GameModeType.Classic), Is.Zero);
        }

        [Test]
        public void Records_SurviveAJsonRoundTrip()
        {
            var data = new SaveData();
            data.TrySetBest(GameModeType.Classic, 77);

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.That(loaded.GetBest(GameModeType.Classic), Is.EqualTo(77));
        }

        [Test]
        public void OldSaveWithFewerModes_StillWorks()
        {
            var loaded = JsonUtility.FromJson<SaveData>("{\"version\":1,\"bestScores\":[5]}");

            Assert.That(loaded.GetBest(GameModeType.Arcade), Is.Zero);
            Assert.That(loaded.TrySetBest(GameModeType.Arcade, 9), Is.True);
            Assert.That(loaded.GetBest(GameModeType.Arcade), Is.EqualTo(9));
        }
    }
}
