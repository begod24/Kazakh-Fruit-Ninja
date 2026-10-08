using NUnit.Framework;

namespace KazakhNinja.Tests
{
    public class PowerUpTimersTests
    {
        [Test]
        public void PowerUp_RunsOutAfterItsDuration()
        {
            var timers = new PowerUpTimers();
            timers.Activate(PowerUpType.SlowTime, 4f);

            timers.Tick(1f);
            Assert.That(timers.IsActive(PowerUpType.SlowTime), Is.True);
            Assert.That(timers.Fraction(PowerUpType.SlowTime), Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(timers.IsActive(PowerUpType.DoubleScore), Is.False);

            timers.Tick(3f);
            Assert.That(timers.IsActive(PowerUpType.SlowTime), Is.False);
            Assert.That(timers.Fraction(PowerUpType.SlowTime), Is.Zero);
        }

        [Test]
        public void CatchingItAgain_RefreshesButNeverShortens()
        {
            var timers = new PowerUpTimers();
            timers.Activate(PowerUpType.Frenzy, 5f);
            timers.Tick(4f);

            timers.Activate(PowerUpType.Frenzy, 5f);
            Assert.That(timers.Remaining(PowerUpType.Frenzy), Is.EqualTo(5f));

            timers.Activate(PowerUpType.Frenzy, 2f);
            Assert.That(timers.Remaining(PowerUpType.Frenzy), Is.EqualTo(5f));
        }

        [Test]
        public void Clear_StopsEverything()
        {
            var timers = new PowerUpTimers();
            timers.Activate(PowerUpType.SlowTime, 4f);
            timers.Activate(PowerUpType.DoubleScore, 6f);

            timers.Clear();

            Assert.That(timers.IsActive(PowerUpType.SlowTime), Is.False);
            Assert.That(timers.IsActive(PowerUpType.DoubleScore), Is.False);
        }
    }
}
