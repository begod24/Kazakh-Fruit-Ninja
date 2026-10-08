using System.Collections.Generic;
using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Closed, readable procedural meshes used as stand-ins for food art.
    /// Every mesh is centred on its origin, so the pivot is also the centre of rotation.
    /// Flat shapes (disc, prism, torus) are built facing the camera (axis along Z).
    /// </summary>
    public static class PlaceholderMeshes
    {
        static readonly Quaternion FaceCamera = Quaternion.Euler(90f, 0f, 0f);

        public static Mesh Sphere(float radius, int longitude = 24, int latitude = 16)
        {
            var data = new MeshData();
            var profile = new List<ProfilePoint>();
            for (int lat = 0; lat <= latitude; lat++)
                profile.Add(SpherePoint(radius, lat, latitude, 0f, 1f - lat / (float)latitude));
            data.Lathe(profile, longitude);
            return data.ToMesh("Sphere", Quaternion.identity);
        }

        /// <param name="height">Total height including both hemispheres; the capsule axis is Y.</param>
        public static Mesh Capsule(float radius, float height, int longitude = 24, int latitude = 16)
        {
            latitude += latitude % 2;
            float halfCylinder = Mathf.Max(0f, height * 0.5f - radius);
            var data = new MeshData();
            var profile = new List<ProfilePoint>();
            int half = latitude / 2;
            float rows = latitude + 1;
            for (int lat = 0; lat <= half; lat++)
                profile.Add(SpherePoint(radius, lat, latitude, halfCylinder, 1f - lat / rows));
            for (int lat = half; lat <= latitude; lat++)
                profile.Add(SpherePoint(radius, lat, latitude, -halfCylinder, 1f - (lat + 1) / rows));
            data.Lathe(profile, longitude);
            return data.ToMesh("Capsule", Quaternion.identity);
        }

        /// <summary>A flat disc (e.g. шелпек) facing the camera.</summary>
        public static Mesh Disc(float radius, float thickness, int segments = 28)
        {
            return Cylinder(radius, thickness, segments, true).ToMesh("Disc", FaceCamera);
        }

        /// <summary>A flat-sided prism facing the camera; 3 sides gives a triangle (e.g. самса).</summary>
        public static Mesh Prism(float radius, float thickness, int sides = 3)
        {
            return Cylinder(radius, thickness, sides, false).ToMesh("Prism", FaceCamera);
        }

        public static Mesh Box(Vector3 size)
        {
            var data = new MeshData();
            Vector3 half = size * 0.5f;
            data.BoxFace(Vector3.right, Vector3.up, Vector3.forward, half.x, half.y, half.z);
            data.BoxFace(Vector3.left, Vector3.up, Vector3.forward, half.x, half.y, half.z);
            data.BoxFace(Vector3.up, Vector3.forward, Vector3.right, half.y, half.z, half.x);
            data.BoxFace(Vector3.down, Vector3.forward, Vector3.right, half.y, half.z, half.x);
            data.BoxFace(Vector3.forward, Vector3.right, Vector3.up, half.z, half.x, half.y);
            data.BoxFace(Vector3.back, Vector3.right, Vector3.up, half.z, half.x, half.y);
            return data.ToMesh("Box", Quaternion.identity);
        }

        /// <summary>
        /// A chili pepper, point down: submesh 0 is the body, submesh 1 the green stem.
        /// Used for the bomb.
        /// </summary>
        public static Mesh Pepper()
        {
            var data = new MeshData();
            data.Lathe(SmoothProfile(new Vector2[]
            {
                new(0f, 0.40f), new(0.14f, 0.39f), new(0.22f, 0.35f), new(0.26f, 0.28f), new(0.27f, 0.18f),
                new(0.26f, 0.05f), new(0.23f, -0.10f), new(0.19f, -0.25f), new(0.14f, -0.38f),
                new(0.08f, -0.50f), new(0.03f, -0.58f), new(0f, -0.62f),
            }), 20);
            data.NewSubmesh();
            data.Lathe(SmoothProfile(new Vector2[]
            {
                new(0f, 0.64f), new(0.05f, 0.635f), new(0.055f, 0.60f), new(0.055f, 0.46f),
                new(0.13f, 0.43f), new(0.17f, 0.39f), new(0.12f, 0.35f), new(0f, 0.34f),
            }), 12);
            return data.ToMesh("Pepper", Quaternion.identity);
        }

        /// <summary>A leather flask (торсық) for the Қымыз power-up.</summary>
        public static Mesh Torsyk()
        {
            var data = new MeshData();
            data.Lathe(SmoothProfile(new Vector2[]
            {
                new(0f, 0.62f), new(0.07f, 0.61f), new(0.08f, 0.52f), new(0.09f, 0.44f), new(0.2f, 0.34f),
                new(0.36f, 0.18f), new(0.44f, 0f), new(0.44f, -0.2f), new(0.38f, -0.36f), new(0.26f, -0.47f), new(0f, -0.5f),
            }), 20);
            return data.ToMesh("Torsyk", Quaternion.identity);
        }

        /// <summary>A bowl (кесе) of көже for the Наурыз power-up: submesh 0 is the bowl, submesh 1 the soup.</summary>
        public static Mesh Bowl()
        {
            var data = new MeshData();
            data.Lathe(SmoothProfile(new Vector2[]
            {
                new(0f, 0.17f), new(0.3f, 0.17f), new(0.44f, 0.17f), new(0.48f, 0.2f), new(0.47f, 0.08f),
                new(0.42f, -0.06f), new(0.32f, -0.17f), new(0.22f, -0.22f), new(0.22f, -0.3f), new(0f, -0.3f),
            }), 24);
            data.NewSubmesh();
            data.Lathe(SmoothProfile(new Vector2[]
            {
                new(0f, 0.2f), new(0.41f, 0.2f), new(0.42f, 0.185f), new(0.41f, 0.172f), new(0f, 0.172f),
            }), 24);
            return data.ToMesh("Bowl", Quaternion.identity);
        }

        /// <summary>A ring (e.g. қарта) facing the camera.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments = 32, int minorSegments = 14)
        {
            var data = new MeshData();
            int stride = minorSegments + 1;
            for (int i = 0; i <= majorSegments; i++)
            {
                float u = Angle(i, majorSegments);
                var radial = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = Angle(j, minorSegments);
                    Vector3 normal = radial * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    data.Add(radial * majorRadius + normal * minorRadius, normal,
                        new Vector2(i / (float)majorSegments, j / (float)minorSegments));
                }
            }
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                    data.Triangle(a, b, c);
                    data.Triangle(b, d, c);
                }
            }
            return data.ToMesh("Torus", FaceCamera);
        }

        static MeshData Cylinder(float radius, float height, int segments, bool smooth)
        {
            var data = new MeshData();
            float h = height * 0.5f;
            if (smooth)
            {
                data.Lathe(new List<ProfilePoint>
                {
                    new(radius, h, 1f, 0f, 1f),
                    new(radius, -h, 1f, 0f, 0f),
                }, segments);
            }
            else
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = Angle(i, segments);
                    float a1 = Angle(i + 1, segments);
                    float mid = (i + 0.5f) / segments * Mathf.PI * 2f;
                    var normal = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                    var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                    var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                    int t0 = data.Add(p0 + Vector3.up * h, normal, new Vector2(0f, 1f));
                    int t1 = data.Add(p1 + Vector3.up * h, normal, new Vector2(1f, 1f));
                    int b0 = data.Add(p0 - Vector3.up * h, normal, new Vector2(0f, 0f));
                    int b1 = data.Add(p1 - Vector3.up * h, normal, new Vector2(1f, 0f));
                    data.Triangle(t0, t1, b0);
                    data.Triangle(t1, b1, b0);
                }
            }
            data.Cap(radius, h, segments, Vector3.up);
            data.Cap(radius, -h, segments, Vector3.down);
            return data;
        }

        /// <summary>
        /// Turns (radius, y) points listed top to bottom, with radius 0 at both ends, into a lathe
        /// profile with smooth outward normals.
        /// </summary>
        static List<ProfilePoint> SmoothProfile(Vector2[] points)
        {
            var profile = new List<ProfilePoint>(points.Length);
            int last = points.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 normal;
                if (i == 0) normal = Vector2.up;
                else if (i == last) normal = Vector2.down;
                else
                {
                    // Outward normal of the tangent (going down the profile) is (-t.y, t.x).
                    Vector2 tangent = points[i + 1] - points[i - 1];
                    normal = new Vector2(-tangent.y, tangent.x).normalized;
                }
                profile.Add(new ProfilePoint(points[i].x, points[i].y, normal.x, normal.y, 1f - i / (float)last));
            }
            return profile;
        }

        // The seam column lands exactly on angle 0 so its duplicated vertices are bit-identical,
        // which lets the slicer stitch cap outlines across the seam.
        static float Angle(int step, int steps) => step == steps ? 0f : step / (float)steps * Mathf.PI * 2f;

        static ProfilePoint SpherePoint(float radius, int lat, int latitude, float yOffset, float v)
        {
            float theta = lat / (float)latitude * Mathf.PI;
            // Exact zero at the poles keeps the pole vertices coincident.
            float ring = lat == 0 || lat == latitude ? 0f : Mathf.Sin(theta);
            float y = lat == 0 ? 1f : lat == latitude ? -1f : Mathf.Cos(theta);
            return new ProfilePoint(ring * radius, y * radius + yOffset, ring, y, v);
        }

        readonly struct ProfilePoint
        {
            public readonly float radius, y, normalRadial, normalY, v;

            public ProfilePoint(float radius, float y, float normalRadial, float normalY, float v)
            {
                this.radius = radius;
                this.y = y;
                this.normalRadial = normalRadial;
                this.normalY = normalY;
                this.v = v;
            }
        }

        sealed class MeshData
        {
            readonly List<Vector3> positions = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<List<int>> submeshes = new() { new List<int>() };

            public int Add(Vector3 position, Vector3 normal, Vector2 uv)
            {
                positions.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                return positions.Count - 1;
            }

            /// <summary>Adds a triangle wound to face along its vertex normals; degenerate ones are skipped.</summary>
            public void Triangle(int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                if (face.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0f) (b, c) = (c, b);
                List<int> triangles = submeshes[submeshes.Count - 1];
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            /// <summary>Following triangles go to a new submesh (a separate material slot).</summary>
            public void NewSubmesh() => submeshes.Add(new List<int>());

            /// <summary>Revolves a profile (top to bottom) around the Y axis.</summary>
            public void Lathe(List<ProfilePoint> profile, int segments)
            {
                int first = positions.Count;
                int stride = segments + 1;
                for (int r = 0; r < profile.Count; r++)
                {
                    ProfilePoint p = profile[r];
                    for (int s = 0; s <= segments; s++)
                    {
                        float angle = Angle(s, segments);
                        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                        Add(new Vector3(cos * p.radius, p.y, sin * p.radius),
                            new Vector3(cos * p.normalRadial, p.normalY, sin * p.normalRadial).normalized,
                            new Vector2(s / (float)segments, p.v));
                    }
                }
                for (int r = 0; r < profile.Count - 1; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int a = first + r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                        Triangle(a, b, c);
                        Triangle(b, d, c);
                    }
                }
            }

            public void Cap(float radius, float y, int segments, Vector3 normal)
            {
                int center = Add(new Vector3(0f, y, 0f), normal, new Vector2(0.5f, 0.5f));
                int first = positions.Count;
                for (int s = 0; s < segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    Add(new Vector3(cos * radius, y, sin * radius), normal, new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
                }
                for (int s = 0; s < segments; s++) Triangle(center, first + s, first + (s + 1) % segments);
            }

            public void BoxFace(Vector3 normal, Vector3 u, Vector3 v, float depth, float halfU, float halfV)
            {
                Vector3 center = normal * depth;
                int a = Add(center - u * halfU - v * halfV, normal, new Vector2(0f, 0f));
                int b = Add(center + u * halfU - v * halfV, normal, new Vector2(1f, 0f));
                int c = Add(center + u * halfU + v * halfV, normal, new Vector2(1f, 1f));
                int d = Add(center - u * halfU + v * halfV, normal, new Vector2(0f, 1f));
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public Mesh ToMesh(string name, Quaternion orientation)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    positions[i] = orientation * positions[i];
                    normals[i] = orientation * normals[i];
                }
                var mesh = new Mesh { name = name };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = submeshes.Count;
                for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i, false);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
