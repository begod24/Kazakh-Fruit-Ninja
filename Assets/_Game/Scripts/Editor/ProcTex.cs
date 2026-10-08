using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    /// <summary>Shared maths for the procedural textures: tileable noise, distance transforms, shapes, PNG output.</summary>
    static class ProcTex
    {
        // ---------- Noise ----------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Value noise in 0..1 that repeats every <paramref name="period"/> cells.</summary>
        public static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            int Wrap(int v) => period > 0 ? ((v % period) + period) % period : v;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>
        /// Fractal noise in 0..1 over the unit square, tileable when <paramref name="tile"/> is set
        /// (u and v in 0..1 wrap seamlessly).
        /// </summary>
        public static float Fbm(float u, float v, int baseCells, int octaves, int seed, bool tile = true)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            int cells = baseCells;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(u * cells, v * cells, tile ? cells : 0, seed + i * 101) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                cells *= 2;
            }
            return sum / total;
        }

        /// <summary>Smooth 1D noise in -1..1 that repeats over 0..1, for ridge lines and silhouettes.</summary>
        public static float Ridge(float u, int baseCells, int octaves, int seed)
        {
            float sum = 0f, amplitude = 1f, total = 0f;
            int cells = baseCells;
            for (int i = 0; i < octaves; i++)
            {
                sum += (ValueNoise(u * cells, 0.5f, cells, seed + i * 57) * 2f - 1f) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                cells *= 2;
            }
            return sum / total;
        }

        // ---------- Distance fields ----------

        /// <summary>Exact Euclidean distance (in pixels) from every pixel to the nearest pixel where <paramref name="mask"/> is false.</summary>
        public static float[] DistanceToOutside(bool[] mask, int width, int height)
        {
            var inverted = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++) inverted[i] = !mask[i];
            return DistanceTo(inverted, width, height);
        }

        /// <summary>Exact Euclidean distance (in pixels) from every pixel to the nearest pixel where <paramref name="targets"/> is true.</summary>
        public static float[] DistanceTo(bool[] targets, int width, int height)
        {
            // Felzenszwalb & Huttenlocher: two passes of the 1D squared distance transform.
            const float Infinity = 1e20f;
            var grid = new float[width * height];
            for (int i = 0; i < grid.Length; i++) grid[i] = targets[i] ? 0f : Infinity;

            int n = Mathf.Max(width, height);
            var f = new float[n];
            var d = new float[n];
            var v = new int[n];
            var z = new float[n + 1];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) f[y] = grid[y * width + x];
                Transform1D(f, height, d, v, z);
                for (int y = 0; y < height; y++) grid[y * width + x] = d[y];
            }
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) f[x] = grid[y * width + x];
                Transform1D(f, width, d, v, z);
                for (int x = 0; x < width; x++) grid[y * width + x] = Mathf.Sqrt(d[x]);
            }
            return grid;
        }

        static void Transform1D(float[] f, int n, float[] d, int[] v, float[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
            }
        }

        // ---------- Shapes (signed distances, negative inside) ----------

        public static float Circle(Vector2 p, Vector2 center, float radius) => (p - center).magnitude - radius;

        public static float Box(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return (p - (a + ab * t)).magnitude;
        }

        public static float Polyline(Vector2 p, Vector2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++) best = Mathf.Min(best, Segment(p, points[i], points[i + 1]));
            return best;
        }

        /// <summary>Anti-aliased coverage of a signed distance, with <paramref name="pixel"/> the size of one pixel in its units.</summary>
        public static float Coverage(float sdf, float pixel) => Mathf.Clamp01(0.5f - sdf / Mathf.Max(pixel, 1e-6f));

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static Color Hex(int rgb, float alpha = 1f) =>
            new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        // ---------- Output ----------

        public static void SavePng(string path, int width, int height, Color[] pixels, bool alpha)
        {
            ContentBuilder.EnsureFolder(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Import settings for generated textures; reimports only when something differs.</summary>
        public static void Configure(string path, Action<TextureImporter> setup)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            string before = EditorJsonUtility.ToJson(importer);
            setup(importer);
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
        }
    }
}
