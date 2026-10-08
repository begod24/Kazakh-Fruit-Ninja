using NUnit.Framework;
using UnityEngine;

namespace KazakhNinja.Tests
{
    public class PlaceholderMeshTests
    {
        [TestCase(Shape.Sphere)]
        [TestCase(Shape.Capsule)]
        [TestCase(Shape.Disc)]
        [TestCase(Shape.Prism)]
        [TestCase(Shape.Box)]
        [TestCase(Shape.Torus)]
        [TestCase(Shape.Pepper)]
        [TestCase(Shape.Torsyk)]
        [TestCase(Shape.Bowl)]
        public void GeneratedMesh_IsClosedAndFacesOutward(Shape shape)
        {
            Mesh mesh = TestMeshes.Create(shape);

            Assert.That(mesh.isReadable, Is.True);
            Assert.That(TestMeshes.AreaVectorSum(mesh).magnitude, Is.LessThan(1e-4f), "surface is not closed");
            Assert.That(TestMeshes.SignedVolume(mesh), Is.GreaterThan(0f), "faces point inward");

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Sphere_VolumeMatchesTheRealSphere()
        {
            Mesh mesh = PlaceholderMeshes.Sphere(0.5f);
            float expected = 4f / 3f * Mathf.PI * 0.125f;

            Assert.That(TestMeshes.SignedVolume(mesh), Is.EqualTo(expected).Within(expected * 0.05f));

            Object.DestroyImmediate(mesh);
        }
    }
}
