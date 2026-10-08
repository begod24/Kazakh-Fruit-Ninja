using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class ComboCounterTests
    {
        const float Window = 0.25f;

        [Test]
        public void ThreeQuickSlices_AreAComboOnceTheStrokeEnds()
        {
            var combo = new ComboCounter(Window);
            combo.Register(0f, new Vector3(0f, 0f, 0f));
            combo.Register(0.1f, new Vector3(3f, 0f, 0f));
            combo.Register(0.2f, new Vector3(6f, 3f, 0f));

            Assert.That(combo.TryComplete(0.3f, out _, out _), Is.False, "stroke still going");
            Assert.That(combo.TryComplete(0.5f, out int count, out Vector3 position), Is.True);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(position, Is.EqualTo(new Vector3(3f, 1f, 0f)));
        }

        [Test]
        public void TwoSlices_AreNotACombo()
        {
            var combo = new ComboCounter(Window);
            combo.Register(0f, Vector3.zero);
            combo.Register(0.1f, Vector3.zero);

            Assert.That(combo.TryComplete(1f, out int count, out _), Is.False);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(combo.Count, Is.Zero, "counter resets for the next stroke");
        }

        [Test]
        public void SlowSlices_StartNewStrokes()
        {
            var combo = new ComboCounter(Window);
            combo.Register(0f, Vector3.zero);
            combo.Register(0.5f, Vector3.zero);
            combo.Register(1.0f, Vector3.zero);

            Assert.That(combo.Count, Is.EqualTo(1));
            Assert.That(combo.TryComplete(2f, out _, out _), Is.False);
        }
    }
}
