using UnityEngine;

namespace KazakhNinja.Tests
{
    public enum Shape { Sphere, Capsule, Disc, Prism, Box, Torus, Pepper, Torsyk, Bowl }

    static class TestMeshes
    {
        public static Mesh Create(Shape shape) => shape switch
        {
            Shape.Sphere => PlaceholderMeshes.Sphere(0.5f),
            Shape.Capsule => PlaceholderMeshes.Capsule(0.2f, 1.5f),
            Shape.Disc => PlaceholderMeshes.Disc(0.62f, 0.1f),
            Shape.Prism => PlaceholderMeshes.Prism(0.6f, 0.3f),
            Shape.Box => PlaceholderMeshes.Box(new Vector3(0.95f, 0.42f, 0.42f)),
            Shape.Torus => PlaceholderMeshes.Torus(0.42f, 0.17f),
            Shape.Pepper => PlaceholderMeshes.Pepper(),
            Shape.Torsyk => PlaceholderMeshes.Torsyk(),
            Shape.Bowl => PlaceholderMeshes.Bowl(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(shape)),
        };

        /// <summary>Positive when every face points outward (Unity front face = cross(b - a, c - a)).</summary>
        public static float SignedVolume(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            double volume = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                    volume += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
            }
            return (float)(volume / 6.0);
        }

        /// <summary>Sum of area-weighted face normals; zero for a closed surface.</summary>
        public static Vector3 AreaVectorSum(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3 sum = Vector3.zero;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                    sum += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            }
            return sum * 0.5f;
        }
    }
}
