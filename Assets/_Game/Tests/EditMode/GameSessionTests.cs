using NUnit.Framework;

namespace KazakhNinja.Tests
{
    public class GameSessionTests
    {
        static GameRules Classic => new() { lives = 3, bomb = BombEffect.CostsLife };
        static GameRules Arcade => new() { duration = 60f, bomb = BombEffect.CostsPoints, bombPenalty = 10, powerUpsEnabled = true };
        static GameRules SuddenDeath => new() { lives = 3, bomb = BombEffect.EndsRound };

        [Test]
        public void Classic_ThirdMissEndsTheRound()
        {
            var session = new GameSession(Classic);
            GameOverReason reason = GameOverReason.None;
            session.Ended += r => reason = r;

            session.Miss();
            session.Miss();
            Assert.That(session.IsOver, Is.False);
            Assert.That(session.Lives, Is.EqualTo(1));

            session.Miss();
            Assert.That(session.IsOver, Is.True);
            Assert.That(reason, Is.EqualTo(GameOverReason.OutOfLives));
        }

        [Test]
        public void Classic_BombCostsALifeAndKeepsTheScore()
        {
            var session = new GameSession(Classic);
            session.AddPoints(7);

            Assert.That(session.Bomb(), Is.Zero);

            Assert.That(session.Lives, Is.EqualTo(2));
            Assert.That(session.IsOver, Is.False);
            Assert.That(session.Score, Is.EqualTo(7));
        }

        [Test]
        public void Classic_ThirdBombEndsTheRound()
        {
            var session = new GameSession(Classic);
            session.Bomb();
            session.Bomb();
            Assert.That(session.IsOver, Is.False);

            session.Bomb();
            Assert.That(session.EndReason, Is.EqualTo(GameOverReason.Bomb));
        }

        [Test]
        public void Classic_MissesAndBombsShareTheLives()
        {
            var session = new GameSession(Classic);
            session.Miss();
            session.Bomb();
            Assert.That(session.Lives, Is.EqualTo(1));

            session.Miss();
            Assert.That(session.EndReason, Is.EqualTo(GameOverReason.OutOfLives));
        }

        [Test]
        public void EndsRoundRule_OneBombIsEnough()
        {
            var session = new GameSession(SuddenDeath);
            session.Bomb();

            Assert.That(session.EndReason, Is.EqualTo(GameOverReason.Bomb));
            Assert.That(session.Lives, Is.EqualTo(3));
        }

        [Test]
        public void Arcade_BombCostsPointsButNeverGoesBelowZero()
        {
            var session = new GameSession(Arcade);
            session.AddPoints(25);

            Assert.That(session.Bomb(), Is.EqualTo(10));
            Assert.That(session.Score, Is.EqualTo(15));
            Assert.That(session.Bomb(), Is.EqualTo(10));
            Assert.That(session.Bomb(), Is.EqualTo(5), "only what is left can be taken");
            Assert.That(session.Bomb(), Is.EqualTo(0));
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.IsOver, Is.False);
        }

        [Test]
        public void Arcade_MissesAreFree()
        {
            var session = new GameSession(Arcade);
            for (int i = 0; i < 10; i++) session.Miss();

            Assert.That(session.IsOver, Is.False);
        }

        [Test]
        public void Arcade_EndsWhenTheClockRunsOut()
        {
            var session = new GameSession(Arcade);

            session.Tick(59.5f);
            Assert.That(session.IsOver, Is.False);
            Assert.That(session.TimeLeft, Is.EqualTo(0.5f).Within(1e-4f));

            session.Tick(1f);
            Assert.That(session.EndReason, Is.EqualTo(GameOverReason.TimeUp));
            Assert.That(session.TimeLeft, Is.Zero);
        }

        [Test]
        public void Classic_HasNoClock()
        {
            var session = new GameSession(Classic);
            session.Tick(10000f);

            Assert.That(session.IsOver, Is.False);
            Assert.That(session.Elapsed, Is.EqualTo(10000f));
        }

        [Test]
        public void Multiplier_ScalesGainedPoints()
        {
            var session = new GameSession(Arcade) { Multiplier = 2 };

            Assert.That(session.AddPoints(3), Is.EqualTo(6));
            Assert.That(session.Score, Is.EqualTo(6));
        }

        [Test]
        public void NothingCountsAfterTheRoundEnded()
        {
            var session = new GameSession(SuddenDeath);
            session.Bomb();

            Assert.That(session.AddPoints(5), Is.Zero);
            session.Miss();
            session.RegisterSlice();
            Assert.That(session.Score, Is.Zero);
            Assert.That(session.Lives, Is.EqualTo(3));
            Assert.That(session.SlicedCount, Is.Zero);
        }

        [Test]
        public void Ended_FiresOnlyOnce()
        {
            var session = new GameSession(SuddenDeath);
            int ended = 0;
            session.Ended += _ => ended++;

            session.Bomb();
            session.Bomb();
            session.Miss();

            Assert.That(ended, Is.EqualTo(1));
        }
    }
}
