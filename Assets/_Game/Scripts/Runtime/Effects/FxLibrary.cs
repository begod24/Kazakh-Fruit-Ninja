using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// The shared particle systems every effect is emitted into, one per kind of particle. However many foods
    /// are cut at once, all their droplets are one draw call, all their sparks another, and so on; nothing is
    /// instantiated during play. Built by Kazakh Ninja/Build Effects; the textures are one atlas (FxArt).
    /// </summary>
    public sealed class FxLibrary : MonoBehaviour
    {
        [SerializeField] ParticleSystem slash;
        [SerializeField] ParticleSystem droplets;
        [SerializeField] ParticleSystem mist;
        [SerializeField] ParticleSystem crumbs;
        [SerializeField] ParticleSystem powder;
        [Tooltip("Coins, sweets and confetti.")]
        [SerializeField] ParticleSystem festive;
        [SerializeField] ParticleSystem glints;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] ParticleSystem embers;
        [SerializeField] ParticleSystem ornaments;
        [SerializeField] ParticleSystem rings;
        [SerializeField] ParticleSystem fire;
        [SerializeField] ParticleSystem smoke;
        [Tooltip("Splash stains on the backdrop (Kazakh Ninja/Splat shader, variant in the colour's alpha).")]
        [SerializeField] ParticleSystem stains;

        static readonly Color32[] FestiveColors =
        {
            new(255, 206, 64, 255), new(232, 62, 84, 255), new(64, 184, 112, 255),
            new(64, 150, 232, 255), new(236, 104, 176, 255), new(255, 244, 220, 255),
        };

        /// <summary>Scales how many particles each effect emits (lower on weak phones).</summary>
        public float Density { get; set; } = 1f;

        int Count(int full) => Mathf.Max(1, Mathf.RoundToInt(full * Density));

        // ---------- Slice ----------

        /// <summary>A bright line along the cut: a coloured glow with a thin white-hot core.</summary>
        public void Slash(Vector3 position, Vector3 direction, Color color, float length)
        {
            // Billboards turn clockwise for positive rotation.
            float angle = -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Emit(slash, position, Vector3.zero, new Vector3(length, 0.34f, 1f), color, 0.2f, angle);
            Emit(slash, position, Vector3.zero, new Vector3(length * 0.9f, 0.09f, 1f), Color.Lerp(color, Color.white, 0.75f), 0.14f, angle);
        }

        /// <summary>Juice thrown out of both sides of the cut, mostly <paramref name="main"/> with some <paramref name="second"/>.</summary>
        public void Juice(Vector3 position, Vector3 along, Vector3 normal, Color main, Color second, float amount = 1f)
        {
            int count = Count(Mathf.RoundToInt(14 * amount));
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 direction = normal * (side * Random.Range(0.6f, 1f)) + along * Random.Range(-0.45f, 0.45f)
                                    + Vector3.up * Random.Range(0f, 0.5f) + (Vector3)Random.insideUnitCircle * 0.25f;
                Vector3 velocity = direction.normalized * Random.Range(2.5f, 8.5f) + Vector3.up * 1.2f;
                bool big = i < 3;
                Color color = Jitter(Random.value < 0.8f ? main : second, 0.1f);
                Emit(droplets, position, velocity * (big ? 0.6f : 1f), big ? Random.Range(0.18f, 0.28f) : Random.Range(0.06f, 0.15f),
                    color, Random.Range(0.45f, 0.85f));
            }
            // A soft puff of fine spray behind the droplets gives the splash some volume.
            Emit(mist, position + (Vector3)Random.insideUnitCircle * 0.15f, (Vector3)Random.insideUnitCircle * 0.6f,
                Random.Range(0.75f, 1.05f) * amount, WithAlpha(main, 0.45f), Random.Range(0.3f, 0.42f));
        }

        /// <summary>Crumbs of pastry or cheese, bouncing out of the cut and falling.</summary>
        public void Crumbs(Vector3 position, Vector3 along, Vector3 normal, Color main, Color second, float amount = 1f)
        {
            int count = Count(Mathf.RoundToInt(11 * amount));
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 direction = normal * (side * Random.Range(0.4f, 1f)) + along * Random.Range(-0.6f, 0.6f) + Vector3.up * Random.Range(0.2f, 0.9f);
                Emit(crumbs, position, direction.normalized * Random.Range(2f, 6.5f), Random.Range(0.08f, 0.2f),
                    Jitter(Random.value < 0.6f ? main : second, 0.12f), Random.Range(0.6f, 1.1f), Random.Range(0f, 360f));
            }
            Emit(mist, position, Vector3.zero, 0.9f * amount, WithAlpha(main, 0.3f), 0.3f);
        }

        /// <summary>A soft cloud of powder with a few small bits (құрт).</summary>
        public void Powder(Vector3 position, Color main, Color second, float amount = 1f)
        {
            int clouds = Count(Mathf.RoundToInt(5 * amount));
            for (int i = 0; i < clouds; i++)
            {
                Vector3 velocity = (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.6f, 2.2f)) + Vector3.up * 0.3f;
                Emit(powder, position + (Vector3)Random.insideUnitCircle * 0.2f, velocity, Random.Range(0.45f, 0.8f),
                    WithAlpha(Jitter(i % 3 == 0 ? second : main, 0.05f), 0.8f), Random.Range(0.6f, 1f), Random.Range(0f, 360f));
            }
            Crumbs(position, Vector3.right, Vector3.up, main, second, 0.45f * amount);
        }

        /// <summary>Coins, sweets and confetti thrown up like шашу at a той.</summary>
        public void Festive(Vector3 position, float amount = 1f)
        {
            int count = Count(Mathf.RoundToInt(12 * amount));
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(3f, 8f), 0f);
                Emit(festive, position, velocity, Random.Range(0.14f, 0.24f), FestiveColors[Random.Range(0, FestiveColors.Length)],
                    Random.Range(1.1f, 1.7f), Random.Range(0f, 360f));
            }
            Glints(position, new Color(1f, 0.9f, 0.55f), Count(Mathf.RoundToInt(3 * amount)), 0.6f, 0.45f);
        }

        // ---------- Sparkle and fire ----------

        public void Glints(Vector3 position, Color color, int count, float radius, float size)
        {
            for (int i = 0; i < count; i++)
                Emit(glints, position + (Vector3)Random.insideUnitCircle * radius, (Vector3)Random.insideUnitCircle * 0.5f,
                    size * Random.Range(0.6f, 1.2f), color, Random.Range(0.3f, 0.55f), Random.Range(0f, 90f));
        }

        /// <summary>Fast streaks thrown along <paramref name="direction"/> with <paramref name="spread"/> (0 = a line, 1 = all round).</summary>
        public void Sparks(Vector3 position, Vector3 direction, Color color, int count, float speed, float spread)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 random = Random.insideUnitCircle.normalized;
                Vector3 heading = Vector3.Slerp(direction.sqrMagnitude > 0f ? direction.normalized : random, random, spread);
                Emit(sparks, position, heading * (speed * Random.Range(0.5f, 1.2f)), Random.Range(0.05f, 0.1f), color,
                    Random.Range(0.18f, 0.4f));
            }
        }

        public void Embers(Vector3 position, Color color, int count, float speed)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
                Emit(embers, position + (Vector3)Random.insideUnitCircle * 0.2f,
                    (Vector3)Random.insideUnitCircle * speed + Vector3.up * Random.Range(0.5f, 2f), Random.Range(0.06f, 0.13f),
                    Jitter(color, 0.1f), Random.Range(0.6f, 1.2f));
        }

        /// <summary>A ring of glowing ornaments opening out from the cut.</summary>
        public void Ornaments(Vector3 position, Color color, int count)
        {
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = start + i * 360f / count;
                Vector3 heading = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                Emit(ornaments, position + heading * 0.3f, heading * 1.6f, Random.Range(0.3f, 0.42f), color, 0.55f, -angle);
            }
        }

        /// <summary>An expanding ring: a shock wave or the ripple of a power-up.</summary>
        public void Ring(Vector3 position, Color color, float size, float lifetime) =>
            Emit(rings, position, Vector3.zero, size, color, lifetime);

        public void Fire(Vector3 position, int count, float speed)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
                Emit(fire, position + (Vector3)Random.insideUnitCircle * 0.3f, (Vector3)Random.insideUnitCircle * speed + Vector3.up * 0.6f,
                    Random.Range(0.7f, 1.3f), Color.white, Random.Range(0.35f, 0.6f), Random.Range(0f, 360f));
        }

        public void Smoke(Vector3 position, Color color, int count, float speed)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
                Emit(smoke, position + (Vector3)Random.insideUnitCircle * 0.4f, (Vector3)Random.insideUnitCircle * speed + Vector3.up * 0.8f,
                    Random.Range(0.9f, 1.6f), color, Random.Range(0.9f, 1.5f), Random.Range(0f, 360f));
        }

        // ---------- Stains ----------

        /// <summary>
        /// A stain on the backdrop. <paramref name="variant"/> picks the shape in the splat atlas (0-4 splashes,
        /// 5-6 crumbs, 7 powder); the Splat shader reads it from the colour's alpha, so all stains share one draw call.
        /// </summary>
        public void Stain(Vector3 position, float angle, Vector2 size, Color color, int variant, float lifetime)
        {
            Color32 packed = color;
            packed.a = (byte)(Mathf.Clamp(variant, 0, 7) * 32 + 16);
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startSize3D = new Vector3(size.x, size.y, 1f),
                startColor = packed,
                startLifetime = lifetime,
                rotation = angle,
                applyShapeToPosition = false,
            };
            stains.Emit(p, 1);
        }

        /// <summary>Removes every particle, e.g. when a round restarts.</summary>
        public void Clear()
        {
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true)) system.Clear(true);
        }

        /// <summary>
        /// Emits one invisible particle from every system at <paramref name="position"/>, so their GPU programs are
        /// built at startup instead of stalling the first cut or the first pepper.
        /// </summary>
        public void WarmUp(Vector3 position)
        {
            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
                Emit(system, position, Vector3.zero, 0.001f, Color.clear, 0.05f);
        }

        // ---------- Helpers ----------

        static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, Color color, float lifetime, float rotation = 0f)
        {
            if (system == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startColor = color,
                startLifetime = lifetime,
                rotation = rotation,
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, Vector3 size, Color color, float lifetime, float rotation)
        {
            if (system == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize3D = size,
                startColor = color,
                startLifetime = lifetime,
                rotation = rotation,
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        /// <summary>A slightly lighter or darker shade, so a burst is not one flat colour.</summary>
        static Color Jitter(Color color, float amount)
        {
            float k = 1f + Random.Range(-amount, amount);
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
