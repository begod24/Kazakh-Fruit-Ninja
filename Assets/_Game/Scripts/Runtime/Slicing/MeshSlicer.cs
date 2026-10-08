using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KazakhNinja
{
    /// <summary>
    /// Cuts a mesh with a plane into two closed halves. The cut surface ("cap") is appended as an
    /// extra submesh, so it can use its own "inside" material.
    /// Each boundary loop of the cut is capped by ear clipping, so concave cross-sections
    /// (a bowl's rim, real food models) are filled exactly. Loops nested inside other loops
    /// (hollow objects) are not turned into holes.
    /// Uses static buffers: main thread only, no per-call garbage.
    /// </summary>
    public static class MeshSlicer
    {
        // Quantisation used to match cap boundary points shared by neighbouring triangles.
        const float KeyScale = 10000f;
        const float MinSegmentLengthSqr = 1e-12f;

        static readonly List<Vector3> srcVertices = new();
        static readonly List<Vector3> srcNormals = new();
        static readonly List<Vector2> srcUVs = new();
        static readonly List<int> srcTriangles = new();
        static readonly List<float> distances = new();

        // Cap boundary: every two consecutive points form one segment.
        static readonly List<Vector3> segments = new();
        static readonly Dictionary<Vector3Int, int> firstEndpoint = new();
        static readonly List<int> nextEndpoint = new();
        static readonly List<bool> segmentUsed = new();
        static readonly List<Vector3> loopPoints = new();
        static readonly List<int> loopStarts = new();
        static readonly List<Vector2> loop2D = new();
        static readonly List<int> earPolygon = new();
        static readonly List<int> capTriangles = new();

        static readonly Builder positive = new();
        static readonly Builder negative = new();

        static bool hasNormals;
        static bool hasUVs;
        // Normal-mapped meshes need tangents; they are rebuilt for the halves instead of interpolated.
        static bool hasTangents;

        /// <summary>
        /// Slices <paramref name="source"/> with <paramref name="plane"/> (in the mesh's local space).
        /// The side the plane normal points to goes into <paramref name="positiveResult"/>.
        /// Returns false, leaving the results untouched, when the plane does not split the mesh.
        /// </summary>
        public static bool Slice(Mesh source, Plane plane, Mesh positiveResult, Mesh negativeResult)
        {
            if (source == null || !source.isReadable)
            {
                Debug.LogError($"MeshSlicer: mesh '{(source ? source.name : "null")}' is missing or not Read/Write enabled.");
                return false;
            }

            source.GetVertices(srcVertices);
            source.GetNormals(srcNormals);
            source.GetUVs(0, srcUVs);
            int vertexCount = srcVertices.Count;
            hasNormals = srcNormals.Count == vertexCount;
            hasUVs = srcUVs.Count == vertexCount;
            hasTangents = source.HasVertexAttribute(VertexAttribute.Tangent);

            distances.Clear();
            bool anyPositive = false, anyNegative = false;
            for (int i = 0; i < vertexCount; i++)
            {
                float distance = plane.GetDistanceToPoint(srcVertices[i]);
                distances.Add(distance);
                if (distance >= 0f) anyPositive = true;
                else anyNegative = true;
            }
            if (!anyPositive || !anyNegative) return false;

            int submeshCount = source.subMeshCount;
            positive.Begin(vertexCount, submeshCount);
            negative.Begin(vertexCount, submeshCount);
            segments.Clear();

            for (int submesh = 0; submesh < submeshCount; submesh++)
            {
                source.GetTriangles(srcTriangles, submesh);
                for (int t = 0; t < srcTriangles.Count; t += 3)
                    SplitTriangle(submesh, srcTriangles[t], srcTriangles[t + 1], srcTriangles[t + 2]);
            }
            if (!positive.HasTriangles || !negative.HasTriangles) return false;

            BuildCaps(plane.normal, submeshCount);
            positive.WriteTo(positiveResult);
            negative.WriteTo(negativeResult);
            return true;
        }

        static void SplitTriangle(int submesh, int a, int b, int c)
        {
            bool pa = distances[a] >= 0f, pb = distances[b] >= 0f, pc = distances[c] >= 0f;
            if (pa == pb && pb == pc)
            {
                Builder side = pa ? positive : negative;
                side.AddTriangle(submesh, side.MapVertex(a), side.MapVertex(b), side.MapVertex(c));
                return;
            }

            // Rotate (keeping the winding) so the vertex alone on its side comes first.
            int lone, o1, o2;
            if (pa != pb && pa != pc) { lone = a; o1 = b; o2 = c; }
            else if (pb != pa && pb != pc) { lone = b; o1 = c; o2 = a; }
            else { lone = c; o1 = a; o2 = b; }

            Builder loneSide = distances[lone] >= 0f ? positive : negative;
            Builder otherSide = loneSide == positive ? negative : positive;

            Intersect(lone, o1, out Vector3 p1, out Vector3 n1, out Vector2 uv1);
            Intersect(lone, o2, out Vector3 p2, out Vector3 n2, out Vector2 uv2);
            long key1 = EdgeKey(lone, o1), key2 = EdgeKey(lone, o2);

            // lone → I1 → I2 keeps the original orientation; so does the quad I1 → o1 → o2 → I2.
            loneSide.AddTriangle(submesh, loneSide.MapVertex(lone),
                loneSide.EdgeVertex(key1, p1, n1, uv1), loneSide.EdgeVertex(key2, p2, n2, uv2));

            int q1 = otherSide.EdgeVertex(key1, p1, n1, uv1);
            int q2 = otherSide.EdgeVertex(key2, p2, n2, uv2);
            int m2 = otherSide.MapVertex(o2);
            otherSide.AddTriangle(submesh, q1, otherSide.MapVertex(o1), m2);
            otherSide.AddTriangle(submesh, q1, m2, q2);

            if ((p1 - p2).sqrMagnitude > MinSegmentLengthSqr)
            {
                segments.Add(p1);
                segments.Add(p2);
            }
        }

        static void Intersect(int u, int v, out Vector3 position, out Vector3 normal, out Vector2 uv)
        {
            // Order the edge by position so that triangles sharing it (even across UV seams,
            // where vertices are duplicated) compute bit-identical points.
            if (IsLess(srcVertices[v], srcVertices[u])) (u, v) = (v, u);

            float du = distances[u], dv = distances[v];
            float t = Mathf.Clamp01(du / (du - dv));
            position = Vector3.LerpUnclamped(srcVertices[u], srcVertices[v], t);
            normal = hasNormals ? Vector3.LerpUnclamped(srcNormals[u], srcNormals[v], t).normalized : Vector3.zero;
            uv = hasUVs ? Vector2.LerpUnclamped(srcUVs[u], srcUVs[v], t) : Vector2.zero;
        }

        static bool IsLess(Vector3 a, Vector3 b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }

        static long EdgeKey(int a, int b)
        {
            return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        }

        static Vector3Int Quantize(Vector3 p)
        {
            return new Vector3Int(
                Mathf.RoundToInt(p.x * KeyScale),
                Mathf.RoundToInt(p.y * KeyScale),
                Mathf.RoundToInt(p.z * KeyScale));
        }

        static void BuildCaps(Vector3 planeNormal, int capSubmesh)
        {
            int segmentCount = segments.Count / 2;
            if (segmentCount < 3) return;

            // Multimap: quantised point -> segment endpoints (endpoint e belongs to segment e / 2).
            firstEndpoint.Clear();
            nextEndpoint.Clear();
            segmentUsed.Clear();
            for (int e = 0; e < segments.Count; e++)
            {
                Vector3Int key = Quantize(segments[e]);
                nextEndpoint.Add(firstEndpoint.TryGetValue(key, out int head) ? head : -1);
                firstEndpoint[key] = e;
            }
            for (int s = 0; s < segmentCount; s++) segmentUsed.Add(false);

            // Chain segments into closed loops.
            loopPoints.Clear();
            loopStarts.Clear();
            for (int s = 0; s < segmentCount; s++)
            {
                if (segmentUsed[s]) continue;
                segmentUsed[s] = true;

                int start = loopPoints.Count;
                Vector3Int startKey = Quantize(segments[2 * s]);
                loopPoints.Add(segments[2 * s]);
                int current = 2 * s + 1;
                while (true)
                {
                    Vector3 point = segments[current];
                    Vector3Int key = Quantize(point);
                    if (key == startKey) break;
                    loopPoints.Add(point);

                    int next = -1;
                    for (int e = firstEndpoint[key]; e != -1; e = nextEndpoint[e])
                    {
                        if (!segmentUsed[e >> 1]) { next = e; break; }
                    }
                    if (next == -1) break; // open chain (non-manifold input): cap what we have
                    segmentUsed[next >> 1] = true;
                    current = next ^ 1; // the other end of that segment
                }

                if (loopPoints.Count - start >= 3) loopStarts.Add(start);
                else loopPoints.RemoveRange(start, loopPoints.Count - start);
            }
            if (loopStarts.Count == 0) return;
            loopStarts.Add(loopPoints.Count);

            // Planar UVs shared by all loops, so a texture lines up across the whole cut.
            Vector3 tangent = Vector3.Cross(planeNormal, Mathf.Abs(planeNormal.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(planeNormal, tangent);
            Vector3 origin = Vector3.zero;
            for (int i = 0; i < loopPoints.Count; i++) origin += loopPoints[i];
            origin /= loopPoints.Count;
            float extent = 1e-4f;
            for (int i = 0; i < loopPoints.Count; i++)
            {
                Vector3 d = loopPoints[i] - origin;
                extent = Mathf.Max(extent, Mathf.Abs(Vector3.Dot(d, tangent)), Mathf.Abs(Vector3.Dot(d, bitangent)));
            }
            var uvFrame = new CapUVFrame(origin, tangent, bitangent, 0.5f / extent);

            for (int l = 0; l + 1 < loopStarts.Count; l++)
            {
                int from = loopStarts[l], to = loopStarts[l + 1];
                // Triangles come out counter-clockwise around the plane normal: front faces point along it.
                TriangulateLoop(from, to, tangent, bitangent);
                AddCap(negative, planeNormal, capSubmesh, from, to, uvFrame, false);
                AddCap(positive, -planeNormal, capSubmesh, from, to, uvFrame, true); // faces the other way
            }
        }

        static void AddCap(Builder builder, Vector3 normal, int submesh, int from, int to, in CapUVFrame uvFrame, bool flip)
        {
            int first = builder.VertexCount;
            for (int i = from; i < to; i++) builder.AddVertex(loopPoints[i], normal, uvFrame.Project(loopPoints[i]));
            for (int t = 0; t < capTriangles.Count; t += 3)
            {
                int a = first + capTriangles[t], b = first + capTriangles[t + 1], c = first + capTriangles[t + 2];
                if (flip) builder.AddTriangle(submesh, a, c, b);
                else builder.AddTriangle(submesh, a, b, c);
            }
        }

        /// <summary>
        /// Ear-clips the loop [from, to) of <see cref="loopPoints"/> into <see cref="capTriangles"/>
        /// (indices relative to <paramref name="from"/>), counter-clockwise in the (tangent, bitangent) plane.
        /// </summary>
        static void TriangulateLoop(int from, int to, Vector3 tangent, Vector3 bitangent)
        {
            capTriangles.Clear();
            int count = to - from;
            loop2D.Clear();
            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = loopPoints[from + i];
                loop2D.Add(new Vector2(Vector3.Dot(p, tangent), Vector3.Dot(p, bitangent)));
            }
            for (int i = 0; i < count; i++)
            {
                Vector2 a = loop2D[i], b = loop2D[(i + 1) % count];
                area += a.x * b.y - b.x * a.y;
            }

            earPolygon.Clear();
            for (int i = 0; i < count; i++) earPolygon.Add(area >= 0f ? i : count - 1 - i);

            int cursor = 0, stalled = 0;
            while (earPolygon.Count > 3)
            {
                int n = earPolygon.Count;
                cursor %= n;
                int ip = earPolygon[(cursor + n - 1) % n], ic = earPolygon[cursor], ix = earPolygon[(cursor + 1) % n];
                Vector2 a = loop2D[ip], b = loop2D[ic], c = loop2D[ix];
                float turn = Cross(b - a, c - b);
                float scale = Mathf.Max((b - a).sqrMagnitude, (c - b).sqrMagnitude);

                if (Mathf.Abs(turn) <= 1e-6f * scale)
                {
                    earPolygon.RemoveAt(cursor); // collinear: adds no area
                    stalled = 0;
                }
                else if (turn > 0f && !AnyVertexInside(a, b, c, ip, ic, ix))
                {
                    capTriangles.Add(ip);
                    capTriangles.Add(ic);
                    capTriangles.Add(ix);
                    earPolygon.RemoveAt(cursor);
                    stalled = 0;
                }
                else if (++stalled > n)
                {
                    // No ear left (self-touching outline from bad input): fan the rest rather than loop forever.
                    for (int i = 1; i + 1 < earPolygon.Count; i++)
                    {
                        capTriangles.Add(earPolygon[0]);
                        capTriangles.Add(earPolygon[i]);
                        capTriangles.Add(earPolygon[i + 1]);
                    }
                    return;
                }
                else cursor++;
            }
            if (earPolygon.Count == 3)
            {
                capTriangles.Add(earPolygon[0]);
                capTriangles.Add(earPolygon[1]);
                capTriangles.Add(earPolygon[2]);
            }
        }

        static bool AnyVertexInside(Vector2 a, Vector2 b, Vector2 c, int ia, int ib, int ic)
        {
            foreach (int i in earPolygon)
            {
                if (i == ia || i == ib || i == ic) continue;
                Vector2 p = loop2D[i];
                if (Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f) return true;
            }
            return false;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        readonly struct CapUVFrame
        {
            readonly Vector3 origin, tangent, bitangent;
            readonly float scale;

            public CapUVFrame(Vector3 origin, Vector3 tangent, Vector3 bitangent, float scale)
            {
                this.origin = origin;
                this.tangent = tangent;
                this.bitangent = bitangent;
                this.scale = scale;
            }

            public Vector2 Project(Vector3 p)
            {
                Vector3 d = p - origin;
                return new Vector2(Vector3.Dot(d, tangent) * scale + 0.5f, Vector3.Dot(d, bitangent) * scale + 0.5f);
            }
        }

        /// <summary>Accumulates the geometry of one half.</summary>
        sealed class Builder
        {
            readonly List<Vector3> vertices = new();
            readonly List<Vector3> normals = new();
            readonly List<Vector2> uvs = new();
            readonly List<List<int>> submeshes = new();
            readonly Dictionary<long, int> edgeVertices = new();
            int[] vertexMap = new int[0];
            int submeshCount;

            public int VertexCount => vertices.Count;

            public bool HasTriangles
            {
                get
                {
                    for (int i = 0; i < submeshCount - 1; i++)
                        if (submeshes[i].Count > 0) return true;
                    return false;
                }
            }

            public void Begin(int sourceVertexCount, int sourceSubmeshCount)
            {
                vertices.Clear();
                normals.Clear();
                uvs.Clear();
                edgeVertices.Clear();
                if (vertexMap.Length < sourceVertexCount) vertexMap = new int[sourceVertexCount];
                System.Array.Fill(vertexMap, -1, 0, sourceVertexCount);

                submeshCount = sourceSubmeshCount + 1; // + cap
                while (submeshes.Count < submeshCount) submeshes.Add(new List<int>());
                for (int i = 0; i < submeshCount; i++) submeshes[i].Clear();
            }

            public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                return vertices.Count - 1;
            }

            public int MapVertex(int source)
            {
                int index = vertexMap[source];
                if (index < 0)
                {
                    index = AddVertex(srcVertices[source],
                        hasNormals ? srcNormals[source] : Vector3.zero,
                        hasUVs ? srcUVs[source] : Vector2.zero);
                    vertexMap[source] = index;
                }
                return index;
            }

            public int EdgeVertex(long key, Vector3 position, Vector3 normal, Vector2 uv)
            {
                if (!edgeVertices.TryGetValue(key, out int index))
                {
                    index = AddVertex(position, normal, uv);
                    edgeVertices.Add(key, index);
                }
                return index;
            }

            public void AddTriangle(int submesh, int a, int b, int c)
            {
                List<int> list = submeshes[submesh];
                list.Add(a);
                list.Add(b);
                list.Add(c);
            }

            public void WriteTo(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = submeshCount;
                for (int i = 0; i < submeshCount; i++) mesh.SetTriangles(submeshes[i], i, false);
                mesh.RecalculateBounds();
                if (!hasNormals) mesh.RecalculateNormals();
                if (hasTangents && hasUVs) mesh.RecalculateTangents();
            }
        }
    }
}
