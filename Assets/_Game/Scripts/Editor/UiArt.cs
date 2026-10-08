using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Procedural placeholder UI art: white shapes (tinted by the Image colour) drawn from signed distance
    /// functions, so edges are anti-aliased. Existing files are kept, so they can be replaced by real art.
    /// </summary>
    static class UiArt
    {
        public const string SpriteFolder = "Assets/_Game/UI/Sprites";
        public const string RoundedRect = SpriteFolder + "/RoundedRect.png";
        public const string Circle = SpriteFolder + "/Circle.png";
        public const string Kese = SpriteFolder + "/Kese.png";
        public const string Shanyrak = SpriteFolder + "/Shanyrak.png";
        public const string Ornament = SpriteFolder + "/Ornament.png";
        public const string Pause = SpriteFolder + "/Pause.png";
        public const string Vignette = SpriteFolder + "/Vignette.png";
        public const string Glow = SpriteFolder + "/Glow.png";
        public const string Gear = SpriteFolder + "/Gear.png";

        public static void BuildAll()
        {
            ContentBuilder.EnsureFolder(SpriteFolder);

            Make(RoundedRect, 128, 128, new Vector4(48, 48, 48, 48), false, c =>
                c.Fill(p => Box(p, new Vector2(64, 64), new Vector2(63, 63), 40)));

            Make(Circle, 128, 128, Vector4.zero, false, c => c.Fill(p => CircleSdf(p, new Vector2(64, 64), 62)));

            // Кесе (bowl) for lives: a half-disc with a rim, a foot and three cut-out dots.
            Make(Kese, 128, 128, Vector4.zero, false, c =>
            {
                c.Fill(p => Mathf.Max(CircleSdf(p, new Vector2(64, 84), 56), p.y - 84));
                c.Fill(p => Box(p, new Vector2(64, 86), new Vector2(58, 7), 7));
                c.Fill(p => Box(p, new Vector2(64, 24), new Vector2(24, 8), 5));
                foreach (Vector2 dot in new[] { new Vector2(38, 60), new Vector2(64, 54), new Vector2(90, 60) })
                    c.Cut(p => CircleSdf(p, dot, 7));
            });

            // Шаңырақ: the yurt crown — a double ring with sun rays and crossed, bowed bars.
            Make(Shanyrak, 256, 256, Vector4.zero, false, c =>
            {
                var center = new Vector2(128, 128);
                c.Fill(p => Mathf.Abs((p - center).magnitude - 108) - 8);
                c.Fill(p => Mathf.Abs((p - center).magnitude - 88) - 4);
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 from = center + dir * 118, to = center + dir * 126;
                    c.Fill(p => Segment(p, from, to) - 3.2f);
                }
                var bars = new List<List<Vector2>>();
                for (int k = -1; k <= 1; k++)
                {
                    var vertical = new List<Vector2>();
                    var horizontal = new List<Vector2>();
                    for (float t = -1f; t <= 1.001f; t += 0.05f)
                    {
                        float offset = k * 34 + k * 16 * (1f - t * t); // outer bars bow outwards
                        vertical.Add(center + new Vector2(offset, t * 90));
                        horizontal.Add(center + new Vector2(t * 90, offset));
                    }
                    bars.Add(vertical);
                    bars.Add(horizontal);
                }
                foreach (List<Vector2> bar in bars)
                    c.Fill(p => Mathf.Max(Polyline(p, bar) - 5f, (p - center).magnitude - 90));
            });

            // Қошқар мүйіз (ram's horn) border tile: a baseline, a stem splitting into two spirals,
            // and half-diamonds on the edges that join into full diamonds between tiles.
            Make(Ornament, 128, 64, Vector4.zero, true, c =>
            {
                c.Fill(p => Segment(p, new Vector2(-2, 8), new Vector2(130, 8)) - 2.5f);
                c.Fill(p => Segment(p, new Vector2(64, 8), new Vector2(64, 24)) - 3f);
                List<Vector2> right = Spiral(new Vector2(84, 24), 20f, Mathf.PI, -1f);
                List<Vector2> left = Spiral(new Vector2(44, 24), 20f, 0f, 1f);
                c.Fill(p => Polyline(p, right) - 3.2f);
                c.Fill(p => Polyline(p, left) - 3.2f);
                foreach (float x in new[] { 0f, 128f })
                    c.Fill(p => (Mathf.Abs(p.x - x) + Mathf.Abs(p.y - 30)) * 0.7071f - 7f);
            });

            Make(Pause, 128, 128, Vector4.zero, false, c =>
            {
                c.Fill(p => Box(p, new Vector2(46, 64), new Vector2(11, 34), 6));
                c.Fill(p => Box(p, new Vector2(82, 64), new Vector2(11, 34), 6));
            });

            // Dark edges, lighter middle: keeps the menu readable while food flies behind it.
            Make(Vignette, 256, 256, Vector4.zero, false, c =>
                c.Paint(p => Mathf.Lerp(0.5f, 0.92f, Mathf.SmoothStep(0.2f, 1.1f, (p - new Vector2(128, 128)).magnitude / 128f))));

            // Settings: a cog with eight rounded teeth and a hole.
            Make(Gear, 128, 128, Vector4.zero, false, c =>
            {
                var center = new Vector2(64, 64);
                c.Fill(p =>
                {
                    Vector2 d = p - center;
                    float angle = Mathf.Atan2(d.y, d.x);
                    float tooth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.5f, Mathf.Cos(angle * 8f)));
                    return d.magnitude - (42f + 12f * tooth);
                });
                c.Cut(p => CircleSdf(p, center, 19f));
            });

            Make(Glow, 128, 128, Vector4.zero, false, c =>
                c.Paint(p =>
                {
                    float r = Mathf.Clamp01((p - new Vector2(64, 64)).magnitude / 63f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, r);
                    return a * a;
                }));
        }

        public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>Logarithmic spiral from its outer end, turning <paramref name="direction"/> (-1 clockwise).</summary>
        static List<Vector2> Spiral(Vector2 center, float radius, float startAngle, float direction)
        {
            var points = new List<Vector2>();
            for (float turn = 0f; turn <= 6.5f; turn += 0.08f)
            {
                float r = radius * Mathf.Exp(-0.25f * turn);
                float a = startAngle + direction * turn;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return points;
        }

        static void Make(string path, int width, int height, Vector4 border, bool tiled, Action<SdfCanvas> draw)
        {
            if (!File.Exists(path))
            {
                var canvas = new SdfCanvas(width, height);
                draw(canvas);
                canvas.SavePng(path);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, border, tiled);
        }

        static void ConfigureSprite(string path, Vector4 border, bool tiled)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var wrap = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && importer.spriteBorder == border && importer.wrapMode == wrap) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // needed for sliced and tiled images
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // ---------- Signed distance functions (pixels; negative inside) ----------

        static float CircleSdf(Vector2 p, Vector2 center, float radius) => (p - center).magnitude - radius;

        static float Box(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }

        static float Polyline(Vector2 p, List<Vector2> points)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Count; i++) best = Mathf.Min(best, Segment(p, points[i], points[i + 1]));
            return best;
        }

        /// <summary>An alpha mask painted with anti-aliased SDF shapes; saved as white with that alpha.</summary>
        sealed class SdfCanvas
        {
            readonly int width;
            readonly int height;
            readonly float[] alpha;

            public SdfCanvas(int width, int height)
            {
                this.width = width;
                this.height = height;
                alpha = new float[width * height];
            }

            public void Fill(Func<Vector2, float> sdf) => ForEach((i, p) => alpha[i] = Mathf.Max(alpha[i], Coverage(sdf(p))));
            public void Cut(Func<Vector2, float> sdf) => ForEach((i, p) => alpha[i] = Mathf.Min(alpha[i], 1f - Coverage(sdf(p))));
            public void Paint(Func<Vector2, float> value) => ForEach((i, p) => alpha[i] = Mathf.Clamp01(value(p)));

            static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

            void ForEach(Action<int, Vector2> action)
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        action(y * width + x, new Vector2(x + 0.5f, y + 0.5f));
            }

            public void SavePng(string path)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[alpha.Length];
                for (int i = 0; i < alpha.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha[i] * 255f));
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
        }
    }
}
