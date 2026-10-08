using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class MeshSlicerTests
    {
        Mesh positive;
        Mesh negative;

        [SetUp]
        public void SetUp()
        {
            positive = new Mesh();
            negative = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(positive);
            Object.DestroyImmediate(negative);
        }

        // An oblique plane that does not pass through the origin, so a wrong cap changes the volumes.
        static Plane ObliquePlane => new(new Vector3(1f, 0.3f, 0.2f), new Vector3(0.05f, 0.02f, 0f));

        [TestCase(Shape.Sphere)]
        [TestCase(Shape.Capsule)]
        [TestCase(Shape.Disc)]
        [TestCase(Shape.Prism)]
        [TestCase(Shape.Box)]
        [TestCase(Shape.Torus)]
        [TestCase(Shape.Pepper)]
        [TestCase(Shape.Torsyk)]
        [TestCase(Shape.Bowl)]
        public void Slice_ProducesTwoClosedHalvesThatAddUpToTheWhole(Shape shape)
        {
            Mesh source = TestMeshes.Create(shape);

            Assert.That(MeshSlicer.Slice(source, ObliquePlane, positive, negative), Is.True);

            float whole = TestMeshes.SignedVolume(source);
            float a = TestMeshes.SignedVolume(positive);
            float b = TestMeshes.SignedVolume(negative);
            Assert.That(a, Is.GreaterThan(0f));
            Assert.That(b, Is.GreaterThan(0f));
            Assert.That(a + b, Is.EqualTo(whole).Within(whole * 1e-3f));
            Assert.That(TestMeshes.AreaVectorSum(positive).magnitude, Is.LessThan(1e-4f), "positive half is not closed");
            Assert.That(TestMeshes.AreaVectorSum(negative).magnitude, Is.LessThan(1e-4f), "negative half is not closed");

            Object.DestroyImmediate(source);
        }

        [Test]
        public void Slice_AppendsTheCapAsItsOwnSubmesh()
        {
            Mesh source = TestMeshes.Create(Shape.Sphere);

            MeshSlicer.Slice(source, ObliquePlane, positive, negative);

            Assert.That(positive.subMeshCount, Is.EqualTo(source.subMeshCount + 1));
            Assert.That(negative.subMeshCount, Is.EqualTo(source.subMeshCount + 1));
            Assert.That(positive.GetTriangles(source.subMeshCount), Is.Not.Empty);
            Assert.That(negative.GetTriangles(source.subMeshCount), Is.Not.Empty);

            Object.DestroyImmediate(source);
        }

        [Test]
        public void Slice_KeepsEveryMaterialSlotOfAMultiMaterialMesh()
        {
            Mesh source = TestMeshes.Create(Shape.Pepper);
            // Horizontal cut through the pepper's shoulder, crossing both the body and the stem.
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.38f, 0f));

            Assert.That(MeshSlicer.Slice(source, plane, positive, negative), Is.True);

            Assert.That(source.subMeshCount, Is.EqualTo(2));
            Assert.That(positive.subMeshCount, Is.EqualTo(3));
            for (int submesh = 0; submesh < 3; submesh++)
            {
                Assert.That(positive.GetTriangles(submesh), Is.Not.Empty, $"top half, submesh {submesh}");
                Assert.That(negative.GetTriangles(submesh), Is.Not.Empty, $"bottom half, submesh {submesh}");
            }

            Object.DestroyImmediate(source);
        }

        [Test]
        public void Slice_KeepsEachHalfOnItsOwnSideOfThePlane()
        {
            Mesh source = TestMeshes.Create(Shape.Capsule);
            Plane plane = ObliquePlane;

            MeshSlicer.Slice(source, plane, positive, negative);

            foreach (Vector3 v in positive.vertices) Assert.That(plane.GetDistanceToPoint(v), Is.GreaterThanOrEqualTo(-1e-5f));
            foreach (Vector3 v in negative.vertices) Assert.That(plane.GetDistanceToPoint(v), Is.LessThanOrEqualTo(1e-5f));

            Object.DestroyImmediate(source);
        }

        [Test]
        public void Slice_ThroughTheRingOfTheTorus_CapsBothTubeSections()
        {
            Mesh source = TestMeshes.Create(Shape.Torus);
            // Vertical plane through the hole: crosses the tube twice, above and below the hole.
            var plane = new Plane(Vector3.right, new Vector3(0.08f, 0f, 0f));

            Assert.That(MeshSlicer.Slice(source, plane, positive, negative), Is.True);

            float whole = TestMeshes.SignedVolume(source);
            Assert.That(TestMeshes.SignedVolume(positive) + TestMeshes.SignedVolume(negative), Is.EqualTo(whole).Within(whole * 1e-3f));
            Assert.That(TestMeshes.AreaVectorSum(positive).magnitude, Is.LessThan(1e-4f));
            Assert.That(TestMeshes.AreaVectorSum(negative).magnitude, Is.LessThan(1e-4f));

            Object.DestroyImmediate(source);
        }

        [Test]
        public void Slice_ReturnsFalseWhenThePlaneMissesTheMesh()
        {
            Mesh source = TestMeshes.Create(Shape.Box);

            Assert.That(MeshSlicer.Slice(source, new Plane(Vector3.up, new Vector3(0f, 5f, 0f)), positive, negative), Is.False);
            Assert.That(positive.vertexCount, Is.Zero);

            Object.DestroyImmediate(source);
        }
    }
}
