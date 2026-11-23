using NUnit.Framework;
using UnityEngine;

namespace Nomad.Minimap.Editor
{
    public sealed class MinimapProjectionTests
    {
        [Test]
        public void ProjectionKeepsCenterAspectAndNorth()
        {
            var map = new MinimapProjection(new Bounds(new Vector3(20, 3, -10), new Vector3(40, 2, 10)));
            Assert.That(map.Bounds.size.x, Is.EqualTo(map.Bounds.size.z));
            Assert.That(map.Normalize(map.Bounds.center), Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(map.Normalize(map.Bounds.center + Vector3.forward).y, Is.GreaterThan(0.5f));
            Assert.That(map.Normalize(map.Bounds.center + Vector3.right).x, Is.GreaterThan(0.5f));
            Assert.That(map.Normalize(map.Bounds.center + Vector3.up * 100), Is.EqualTo(Vector2.one * 0.5f));
        }

        [Test]
        public void MarkersStayInsideFrameAndWorldCoordinatesRoundTrip()
        {
            var map = new MinimapProjection(new Bounds(Vector3.zero, Vector3.one * 20));
            Assert.That(map.Marker(Vector3.one * 1000, 188), Is.EqualTo(Vector2.one * 181));
            Assert.That(map.Marker(Vector3.one * -1000, 188), Is.EqualTo(Vector2.one * 7));
            Vector2 point = map.Normalize(map.World(0.3f, 0.8f));
            Assert.That(point.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(point.y, Is.EqualTo(0.8f).Within(0.0001f));
        }

    }
}
