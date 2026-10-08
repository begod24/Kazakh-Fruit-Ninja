using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Procedural effect textures: the particle atlas (16 white shapes, tinted per particle), the splat atlas
    /// (8 stain shapes as distance fields with a wet highlight), a tileable noise, and the blade swatches.
    /// </summary>
    static class FxArt
    {
        public const string Folder = "Assets/_Game/Textures/FX";
        public const string AtlasPath = Folder + "/FxAtlas.png";
        public const string SplatAtlasPath = Folder + "/SplatAtlas.png";
        public const string NoisePath = Folder + "/Noise.png";
        public const string BladeFolder = "Assets/_Game/Textures/Blades";

        /// <summary>Frames of the particle atlas (4×4), row by row from the top-left.</summary>
        public const int SoftDot = 0, Droplet = 1, HotDot = 2, Ring = 3, CrumbFirst = 4, CrumbLast = 7, Puff = 8, Star = 9,
            Coin = 10, Confetti = 11, Candy = 12, Petal = 13, Ornament = 14, Snowflake = 15;

        public const int AtlasColumns = 4;
        const int AtlasCell = 128;
        const int SplatCell = 256;

        public static void BuildAll(bool force)
        {
            if (force || !File.Exists(AtlasPath)) PaintAtlas();
            if (force || !File.Exists(SplatAtlasPath)) PaintSplatAtlas();
            if (force || !File.Exists(NoisePath)) PaintNoise();

            ProcTex.Configure(AtlasPath, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
            });
            ProcTex.Configure(SplatAtlasPath, importer =>
            {
                // Distance fields are data, not colour.
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
            });
            ProcTex.Configure(NoisePath, importer =>
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
            });
        }

        // ---------- Particle atlas ----------

        /// <summary>A cell painter: point in -1..1 (y up) → (brightness, alpha).</summary>
        delegate Vector2 CellShader(Vector2 p);

        static void PaintAtlas()
        {
            const int size = AtlasCell * AtlasColumns;
            var pixels = new Color[size * size];
            var shaders = new CellShader[16];
            const float px = 2f / AtlasCell;

            shaders[SoftDot] = p =>
            {
                float r = p.magnitude;
                float a = Mathf.Clamp01(1f - r);
                return new Vector2(1f, a * a);
            };
            shaders[Droplet] = p =>
            {
                float r = p.magnitude;
                float a = ProcTex.Coverage(r - 0.8f, px * 1.5f);
                float highlight = ProcTex.SmoothStep(0.42f, 0f, (p - new Vector2(-0.3f, 0.32f)).magnitude);
                float shade = 0.78f + 0.12f * (1f - ProcTex.SmoothStep(0.45f, 0.8f, r)) + 0.3f * highlight;
                return new Vector2(Mathf.Min(1f, shade), a);
            };
            shaders[HotDot] = p =>
            {
                float r2 = p.sqrMagnitude;
                return new Vector2(1f, Mathf.Clamp01(Mathf.Exp(-r2 * 6f) * 1.1f + Mathf.Exp(-r2 * 40f) * 0.4f));
            };
            shaders[Ring] = p =>
            {
                float r = p.magnitude;
                float band = (r - 0.8f) / 0.09f;
                return new Vector2(1f, Mathf.Exp(-band * band) * ProcTex.SmoothStep(1f, 0.93f, r));
            };
            for (int i = CrumbFirst; i <= CrumbLast; i++)
            {
                float[] radii = CrumbRadii(i);
                shaders[i] = p => Crumb(p, radii, px);
            }
            shaders[Puff] = p =>
            {
                float n = ProcTex.Fbm(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f, 3, 3, 17, false);
                float a = ProcTex.SmoothStep(1f, 0.1f, p.magnitude + (n - 0.5f) * 0.55f) * 0.9f;
                return new Vector2(0.88f + 0.12f * n, a);
            };
            shaders[Star] = p =>
            {
                float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                float cross = Mathf.Max(Mathf.Exp(-ax * 14f) * Mathf.Exp(-ay * 2.6f), Mathf.Exp(-ay * 14f) * Mathf.Exp(-ax * 2.6f));
                Vector2 d = new(Mathf.Abs(p.x + p.y), Mathf.Abs(p.x - p.y));
                float diagonal = Mathf.Max(Mathf.Exp(-d.x * 16f) * Mathf.Exp(-d.y * 5f), Mathf.Exp(-d.y * 16f) * Mathf.Exp(-d.x * 5f)) * 0.45f;
                float glow = Mathf.Exp(-p.sqrMagnitude * 16f);
                return new Vector2(1f, Mathf.Clamp01(cross + diagonal + glow));
            };
            shaders[Coin] = p =>
            {
                float r = p.magnitude;
                float a = ProcTex.Coverage(r - 0.86f, px * 1.5f);
                float shade = 0.78f;
                if (r > 0.7f) shade = 0.98f;
                else if (Mathf.Abs(r - 0.52f) < 0.04f) shade = 0.62f;
                shade += 0.2f * ProcTex.SmoothStep(0.5f, 0f, (p - new Vector2(-0.35f, 0.4f)).magnitude);
                return new Vector2(Mathf.Min(1f, shade), a);
            };
            shaders[Confetti] = p =>
            {
                float a = ProcTex.Coverage(ProcTex.Box(p, Vector2.zero, new Vector2(0.85f, 0.45f), 0.08f), px * 1.5f);
                return new Vector2(0.78f + 0.22f * (p.y * 0.5f + 0.5f), a);
            };
            shaders[Candy] = p =>
            {
                float body = new Vector2(p.x / 0.5f, p.y / 0.36f).magnitude - 1f;
                float ax = Mathf.Abs(p.x);
                float spread = Mathf.Lerp(0.1f, 0.4f, Mathf.InverseLerp(0.4f, 0.95f, ax));
                float ends = Mathf.Max(Mathf.Abs(p.y) - spread - 0.03f * Mathf.Sin(p.y * 40f), Mathf.Max(0.4f - ax, ax - 0.95f));
                float a = Mathf.Max(ProcTex.Coverage(body * 0.36f, px * 1.5f), ProcTex.Coverage(ends, px * 1.5f));
                float shade = body < 0f ? 0.88f + 0.12f * ProcTex.SmoothStep(0.3f, 0f, Mathf.Abs(p.y - 0.12f)) : 0.72f;
                return new Vector2(shade, a);
            };
            shaders[Petal] = p =>
            {
                float width = 0.58f * Mathf.Lerp(0.2f, 1f, ProcTex.SmoothStep(-0.9f, 0.35f, p.y));
                float sdf = new Vector2(p.x / Mathf.Max(width, 0.01f), p.y / 0.9f).magnitude - 1f;
                float a = ProcTex.Coverage(sdf * 0.5f, px * 1.5f);
                float shade = 0.82f + 0.18f * (p.y * 0.5f + 0.5f) - 0.08f * ProcTex.SmoothStep(0.06f, 0f, Mathf.Abs(p.x));
                return new Vector2(shade, a);
            };
            Vector2[] leftHorn = Spiral(new Vector2(-0.32f, 0.12f), 0.3f, 0f, 1f), rightHorn = Spiral(new Vector2(0.32f, 0.12f), 0.3f, Mathf.PI, -1f);
            shaders[Ornament] = p =>
            {
                float stem = ProcTex.Segment(p, new Vector2(0f, -0.75f), new Vector2(0f, 0.12f));
                float horns = Mathf.Min(ProcTex.Polyline(p, leftHorn), ProcTex.Polyline(p, rightHorn));
                float bud = (Mathf.Abs(p.x) + Mathf.Abs(p.y + 0.75f)) * 0.7071f - 0.1f;
                float sdf = Mathf.Min(Mathf.Min(stem, horns) - 0.075f, bud);
                return new Vector2(1f, ProcTex.Coverage(sdf, px * 1.5f));
            };
            shaders[Snowflake] = p =>
            {
                float best = float.MaxValue;
                for (int k = 0; k < 6; k++)
                {
                    float angle = k * Mathf.PI / 3f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    best = Mathf.Min(best, ProcTex.Segment(p, Vector2.zero, dir * 0.88f));
                    Vector2 fork = dir * 0.5f;
                    foreach (float turn in new[] { 0.8f, -0.8f })
                    {
                        var branch = new Vector2(Mathf.Cos(angle + turn), Mathf.Sin(angle + turn));
                        best = Mathf.Min(best, ProcTex.Segment(p, fork, fork + branch * 0.26f));
                    }
                }
                return new Vector2(1f, ProcTex.Coverage(best - 0.055f, px * 1.5f));
            };

            for (int frame = 0; frame < 16; frame++)
            {
                int column = frame % AtlasColumns, row = frame / AtlasColumns;
                int x0 = column * AtlasCell, y0 = (AtlasColumns - 1 - row) * AtlasCell;
                for (int y = 0; y < AtlasCell; y++)
                {
                    for (int x = 0; x < AtlasCell; x++)
                    {
                        var p = new Vector2((x + 0.5f) / AtlasCell * 2f - 1f, (y + 0.5f) / AtlasCell * 2f - 1f);
                        Vector2 value = shaders[frame](p);
                        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(AtlasCell - 1 - x, AtlasCell - 1 - y));
                        float alpha = edge < 1f ? 0f : value.y; // a clear border, so neighbours never bleed in
                        pixels[(y0 + y) * size + x0 + x] = new Color(value.x, value.x, value.x, alpha);
                    }
                }
            }
            ProcTex.SavePng(AtlasPath, size, size, pixels, true);
        }

        static float[] CrumbRadii(int frame)
        {
            var random = new Random(frame * 31 + 5);
            int corners = 5 + random.Next(4);
            var radii = new float[corners];
            for (int i = 0; i < corners; i++) radii[i] = 0.55f + (float)random.NextDouble() * 0.33f;
            return radii;
        }

        /// <summary>An irregular lit chunk: a star-shaped polygon shaded like a little dome.</summary>
        static Vector2 Crumb(Vector2 p, float[] radii, float px)
        {
            float angle = Mathf.Atan2(p.y, p.x);
            if (angle < 0f) angle += Mathf.PI * 2f;
            float slot = angle / (Mathf.PI * 2f) * radii.Length;
            int i = Mathf.FloorToInt(slot) % radii.Length;
            float radius = Mathf.Lerp(radii[i], radii[(i + 1) % radii.Length], slot - Mathf.Floor(slot));
            float r = p.magnitude;
            float a = ProcTex.Coverage(r - radius, px * 1.5f);
            float t = Mathf.Clamp01(r / radius);
            var normal = new Vector3(p.x / radius, p.y / radius, Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
            float light = Mathf.Clamp01(Vector3.Dot(normal.normalized, new Vector3(-0.45f, 0.55f, 0.7f).normalized));
            float speckle = ProcTex.ValueNoise(p.x * 9f + 40f, p.y * 9f, 0, 3) * 0.12f;
            return new Vector2(0.5f + 0.5f * light + speckle - 0.06f, a);
        }

        /// <summary>A қошқар мүйіз horn: a spiral curling inwards from its outer end.</summary>
        static Vector2[] Spiral(Vector2 center, float radius, float startAngle, float direction)
        {
            var points = new List<Vector2>();
            for (float turn = 0f; turn <= 5.8f; turn += 0.08f)
            {
                float r = radius * Mathf.Exp(-0.27f * turn);
                float a = startAngle + direction * turn;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return points.ToArray();
        }

        // ---------- Splat atlas ----------

        struct Ball
        {
            public Vector2 center;
            public float radius;
        }

        static void PaintSplatAtlas()
        {
            const int columns = 4, rows = 2;
            int width = SplatCell * columns, height = SplatCell * rows;
            var pixels = new Color[width * height];
            for (int cell = 0; cell < 8; cell++)
            {
                Color[] block = cell switch
                {
                    <= 4 => LiquidSplat(cell),
                    <= 6 => CrumbScatter(cell),
                    _ => PowderCloud(),
                };
                int x0 = cell % columns * SplatCell, y0 = (rows - 1 - cell / columns) * SplatCell;
                for (int y = 0; y < SplatCell; y++)
                    Array.Copy(block, y * SplatCell, pixels, (y0 + y) * width + x0, SplatCell);
            }
            ProcTex.SavePng(SplatAtlasPath, width, height, pixels, false);
        }

        /// <summary>Metaball splashes: a main pool, satellites, and spikes of shrinking drops.</summary>
        static Color[] LiquidSplat(int variant)
        {
            var random = new Random(variant * 97 + 13);
            float R() => (float)random.NextDouble();
            var balls = new List<Ball>();
            var pools = new List<Ball>();

            void AddPool(Vector2 center, float radius)
            {
                balls.Add(new Ball { center = center, radius = radius });
                pools.Add(new Ball { center = center, radius = radius });
            }

            void Spike(Vector2 from, float angle, float length, float radius)
            {
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int steps = 5;
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    balls.Add(new Ball { center = from + dir * (length * t), radius = radius * Mathf.Lerp(1f, 0.35f, t) });
                }
                balls.Add(new Ball { center = from + dir * (length * 1.18f), radius = radius * 0.55f });
            }

            switch (variant)
            {
                case 0: // a classic splash
                    AddPool(Vector2.zero, 0.34f);
                    for (int i = 0; i < 5; i++) Spike(Vector2.zero, i * 1.25f + R(), 0.45f + R() * 0.25f, 0.09f);
                    break;
                case 1: // thrown sideways: a stretched pool with droplets along its length
                    AddPool(new Vector2(-0.15f, 0f), 0.28f);
                    AddPool(new Vector2(0.18f, 0.03f), 0.22f);
                    for (int i = 0; i < 6; i++) balls.Add(new Ball { center = new Vector2(0.45f + i * 0.08f, (R() - 0.5f) * 0.3f), radius = 0.07f - i * 0.008f });
                    for (int i = 0; i < 4; i++) balls.Add(new Ball { center = new Vector2(-0.55f - i * 0.07f, (R() - 0.5f) * 0.25f), radius = 0.06f - i * 0.01f });
                    break;
                case 2: // a burst with many thin spikes
                    AddPool(Vector2.zero, 0.27f);
                    for (int i = 0; i < 9; i++) Spike(Vector2.zero, i * 0.7f + R() * 0.3f, 0.5f + R() * 0.3f, 0.06f);
                    break;
                case 3: // two pools that ran together
                    AddPool(new Vector2(-0.18f, 0.08f), 0.26f);
                    AddPool(new Vector2(0.2f, -0.1f), 0.2f);
                    for (int i = 0; i < 3; i++) Spike(new Vector2(0.2f, -0.1f), -0.6f + i * 0.9f, 0.4f, 0.07f);
                    break;
                default: // a lobed pool
                    AddPool(Vector2.zero, 0.3f);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f + R() * 0.5f;
                        AddPool(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.26f, 0.13f + R() * 0.06f);
                    }
                    for (int i = 0; i < 2; i++) Spike(Vector2.zero, R() * 6.28f, 0.55f, 0.07f);
                    break;
            }
            // Loose droplets all round.
            int loose = 8 + random.Next(6);
            for (int i = 0; i < loose; i++)
            {
                float angle = R() * Mathf.PI * 2f, distance = 0.55f + R() * 0.35f;
                balls.Add(new Ball { center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, radius = 0.02f + R() * 0.045f });
            }

            var mask = new bool[SplatCell * SplatCell];
            ForEachPixel((i, p) =>
            {
                float field = 0f;
                foreach (Ball ball in balls) field += ball.radius * ball.radius / Mathf.Max((p - ball.center).sqrMagnitude, 1e-6f);
                mask[i] = field >= 1f && p.magnitude < 0.96f;
            });
            return Encode(mask, pools, 0.5f, 1f, variant * 7 + 1);
        }

        /// <summary>A scatter of small crumbs, denser in the middle.</summary>
        static Color[] CrumbScatter(int variant)
        {
            var random = new Random(variant * 53 + 7);
            float R() => (float)random.NextDouble();
            var crumbs = new List<(Vector2 center, float radius, float[] corners)>();
            int count = 26 + random.Next(10);
            for (int i = 0; i < count; i++)
            {
                float angle = R() * Mathf.PI * 2f, distance = Mathf.Pow(R(), 0.8f) * 0.82f;
                var corners = new float[5 + random.Next(3)];
                for (int k = 0; k < corners.Length; k++) corners[k] = 0.6f + R() * 0.4f;
                crumbs.Add((new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, 0.025f + R() * 0.06f * (1f - distance * 0.5f), corners));
            }
            var mask = new bool[SplatCell * SplatCell];
            ForEachPixel((i, p) =>
            {
                foreach ((Vector2 center, float radius, float[] corners) in crumbs)
                {
                    Vector2 d = p - center;
                    if (d.sqrMagnitude > radius * radius * 1.1f) continue;
                    float angle = Mathf.Atan2(d.y, d.x);
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    float slot = angle / (Mathf.PI * 2f) * corners.Length;
                    int k = Mathf.FloorToInt(slot) % corners.Length;
                    float edge = Mathf.Lerp(corners[k], corners[(k + 1) % corners.Length], slot - Mathf.Floor(slot)) * radius;
                    if (d.magnitude <= edge) { mask[i] = true; break; }
                }
            });
            return Encode(mask, null, 0.5f, 1f, variant * 11 + 3);
        }

        /// <summary>A soft, ragged cloud of powder; its field rises slowly, so its edge stays soft.</summary>
        static Color[] PowderCloud()
        {
            var mask = new bool[SplatCell * SplatCell];
            ForEachPixel((i, p) =>
            {
                float n = ProcTex.Fbm(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f, 4, 3, 29, false);
                mask[i] = p.magnitude + (n - 0.5f) * 0.5f < 0.62f;
            });
            return Encode(mask, null, 2f, 0.9f, 41);
        }

        static void ForEachPixel(Action<int, Vector2> paint)
        {
            for (int y = 0; y < SplatCell; y++)
                for (int x = 0; x < SplatCell; x++)
                    paint(y * SplatCell + x, new Vector2((x + 0.5f) / SplatCell * 2f - 1f, (y + 0.5f) / SplatCell * 2f - 1f));
        }

        /// <summary>
        /// R: 0.5 on the edge rising to <paramref name="peak"/> at the deepest point (<paramref name="curve"/> above 1
        /// makes the rise slow near the edge: a soft rim); G: a wet highlight on the rim facing the top-left and on the
        /// pools; B: pigment variation.
        /// </summary>
        static Color[] Encode(bool[] mask, List<Ball> pools, float curve, float peak, int seed)
        {
            float[] inside = ProcTex.DistanceToOutside(mask, SplatCell, SplatCell);
            float[] outside = ProcTex.DistanceTo(mask, SplatCell, SplatCell);
            float deepest = 1f;
            foreach (float d in inside) deepest = Mathf.Max(deepest, d);

            var result = new Color[mask.Length];
            for (int y = 0; y < SplatCell; y++)
            {
                for (int x = 0; x < SplatCell; x++)
                {
                    int i = y * SplatCell + x;
                    float field;
                    float gloss = 0f;
                    if (mask[i])
                    {
                        float depth = inside[i] / deepest;
                        field = 0.5f + (peak - 0.5f) * Mathf.Pow(depth, curve);
                        if (pools != null)
                        {
                            // A broad sheen on the lit side of each pool and a sharp specular spot inside it.
                            var p = new Vector2((x + 0.5f) / SplatCell * 2f - 1f, (y + 0.5f) / SplatCell * 2f - 1f);
                            foreach (Ball pool in pools)
                            {
                                Vector2 sheen = pool.center + new Vector2(-0.3f, 0.3f) * pool.radius;
                                Vector2 spot = pool.center + new Vector2(-0.42f, 0.42f) * pool.radius;
                                gloss = Mathf.Max(gloss, ProcTex.SmoothStep(pool.radius * 0.9f, pool.radius * 0.25f, (p - sheen).magnitude) * 0.3f);
                                gloss = Mathf.Max(gloss, ProcTex.SmoothStep(pool.radius * 0.22f, pool.radius * 0.05f, (p - spot).magnitude));
                            }
                            // Keep it off the very edge, where the pool is thin.
                            gloss *= ProcTex.SmoothStep(1f, 4f, inside[i]);
                        }
                    }
                    else field = 0.5f - 0.5f * Mathf.Clamp01(outside[i] / 10f);

                    float pigment = ProcTex.Fbm((x + 0.5f) / SplatCell, (y + 0.5f) / SplatCell, 3, 4, seed);
                    result[i] = new Color(field, gloss, Mathf.Clamp01(0.25f + pigment), 1f);
                }
            }
            return result;
        }

        static float Sample(float[] values, int x, int y) =>
            values[Mathf.Clamp(y, 0, SplatCell - 1) * SplatCell + Mathf.Clamp(x, 0, SplatCell - 1)];

        // ---------- Noise ----------

        static void PaintNoise()
        {
            const int size = 256;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float a = ProcTex.Fbm(u, v, 4, 5, 3);
                    float b = ProcTex.Fbm(u, v, 8, 4, 9);
                    float c = 1f - Mathf.Abs(ProcTex.Fbm(u, v, 4, 4, 21) * 2f - 1f);
                    pixels[y * size + x] = new Color(Stretch(a), Stretch(b), c * c, 1f);
                }
            }
            ProcTex.SavePng(NoisePath, size, size, pixels, false);
        }

        /// <summary>fBm bunches up around 0.5; spread it back over 0..1.</summary>
        static float Stretch(float value) => Mathf.Clamp01((value - 0.5f) * 1.9f + 0.5f);

        // ---------- Blade swatches ----------

        /// <summary>A swatch of a blade for the collection: its trail on a dark tile, in its style.</summary>
        public static Sprite BladeSwatch(string id, Gradient colors, BladeStyle style, Color accent, bool force)
        {
            string path = $"{BladeFolder}/blade_{id}.png";
            if (force || !File.Exists(path))
            {
                const int width = 256, height = 128;
                var pixels = new Color[width * height];
                var background = new Color(0.1f, 0.06f, 0.045f, 1f);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float u = Mathf.InverseLerp(20f, 236f, p.x);
                        // The stroke curves a little, thick at the tip (left), thin at the tail.
                        float centerY = 64f + 18f * Mathf.Sin(u * Mathf.PI * 0.9f - 0.3f);
                        float halfWidth = Mathf.Lerp(20f, 3f, u);
                        float across = Mathf.Abs(p.y - centerY) / halfWidth;
                        if (style == BladeStyle.Lightning) across = Mathf.Abs(p.y - centerY - 10f * (ProcTex.ValueNoise(u * 14f, 3f, 0, 5) * 2f - 1f)) / halfWidth;
                        float inStroke = u >= 0f && u <= 1f ? Mathf.Clamp01((1f - across) * halfWidth) : 0f;
                        Color c = colors.Evaluate(u);
                        float body = ProcTex.SmoothStep(1f, 0.35f, across);
                        float core = ProcTex.SmoothStep(0.3f, 0f, across);
                        Color stroke = Color.Lerp(c, Color.white, core * 0.8f);
                        float alpha = body * inStroke * Mathf.Lerp(1f, 0.35f, u);

                        switch (style)
                        {
                            case BladeStyle.Steel:
                                stroke = Color.Lerp(stroke, Color.white, ProcTex.SmoothStep(0.12f, 0f, Mathf.Abs(u - 0.3f)) * body);
                                break;
                            case BladeStyle.Wind:
                                alpha *= 0.35f + 0.65f * ProcTex.SmoothStep(0.45f, 0.7f, ProcTex.ValueNoise(u * 18f, across * 2.5f, 0, 8));
                                break;
                            case BladeStyle.Ornament:
                                float cell = Mathf.Repeat(u * 7f, 1f) - 0.5f;
                                float diamond = ProcTex.SmoothStep(0.42f, 0.32f, Mathf.Abs(cell) * 1.2f + across * 0.55f);
                                stroke = Color.Lerp(stroke, accent, diamond);
                                alpha = Mathf.Max(alpha, diamond * inStroke);
                                break;
                            case BladeStyle.Fire:
                                float flame = Mathf.Clamp01(ProcTex.ValueNoise(u * 10f, p.y * 0.08f, 0, 4) * 1.7f - across * 1.1f + 0.2f);
                                stroke = Color.Lerp(accent, c, flame);
                                alpha *= Mathf.Clamp01(flame * 1.8f);
                                break;
                        }
                        pixels[y * width + x] = Color.Lerp(background, new Color(stroke.r, stroke.g, stroke.b, 1f), Mathf.Clamp01(alpha));
                    }
                }
                if (style == BladeStyle.Coins)
                {
                    // Coins and sweets falling off the stroke.
                    foreach ((float cx, float cy, Color color) in new[] { (70f, 30f, accent), (120f, 24f, new Color(0.9f, 0.25f, 0.35f)), (165f, 36f, accent), (205f, 26f, new Color(0.3f, 0.7f, 0.45f)) })
                    {
                        for (int y = (int)cy - 9; y <= cy + 9; y++)
                            for (int x = (int)cx - 9; x <= cx + 9; x++)
                            {
                                float coverage = ProcTex.Coverage(ProcTex.Circle(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy), 7f), 1f);
                                pixels[y * width + x] = Color.Lerp(pixels[y * width + x], color, coverage);
                            }
                    }
                }
                ProcTex.SavePng(path, width, height, pixels, false);
            }
            ProcTex.Configure(path, importer =>
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
            });
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    /// <summary>The trail patterns of the Blade Trail shader (its _Style keywords).</summary>
    enum BladeStyle
    {
        Plain,
        Steel,
        Wind,
        Ornament,
        Lightning,
        Fire,
        /// <summary>Plain trail with coins falling off it (for the swatch only).</summary>
        Coins,
    }
}
