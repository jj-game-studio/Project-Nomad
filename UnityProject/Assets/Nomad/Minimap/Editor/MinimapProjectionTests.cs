using NUnit.Framework;
using UnityEngine;

namespace Nomad.Minimap.Editor
{
    public sealed class MinimapProjectionTests
    {
        [Test]
        public void LocalWindowTracksPlayerAndStaysInsideMap()
        {
            var full = new MinimapProjection(new Bounds(Vector3.zero, Vector3.one * 100));
            var local = full.Window(new Vector3(10, 0, 20), 36);
            Assert.That(local.Bounds.size.x, Is.EqualTo(36));
            Assert.That(local.Normalize(new Vector3(10, 0, 20)), Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(local.Contains(new Vector3(40, 0, 20)), Is.False);
            var edge = full.Window(Vector3.one * 1000, 36);
            Assert.That(edge.Bounds.max.x, Is.EqualTo(full.Bounds.max.x));
            Assert.That(full.Window(Vector3.zero, 1000).Bounds.size.x, Is.EqualTo(full.Bounds.size.x));
        }

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
