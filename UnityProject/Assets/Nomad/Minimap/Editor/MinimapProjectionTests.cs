using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Test]
        public void VillageRasterIgnoresCollidersFromOtherLoadedScenes()
        {
            Scene original = SceneManager.GetActiveScene();
            Scene first = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene other = default;
            Texture2D before = null, after = null;
            try
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(floor, first);
                floor.transform.position = new Vector3(1000, -0.5f, 1000);
                floor.transform.localScale = new Vector3(12, 1, 12);
                before = MinimapSurface.Village(first, out _);

                other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(roof, other);
                roof.transform.position = new Vector3(1000, 1, 1000);
                roof.transform.localScale = new Vector3(10, 1, 10);
                after = MinimapSurface.Village(first, out _);
                CollectionAssert.AreEqual(before.GetPixels32(), after.GetPixels32(), "다른 씬의 Collider를 지도에 섞지 않음");
            }
            finally
            {
                if (before != null) Object.DestroyImmediate(before);
                if (after != null) Object.DestroyImmediate(after);
                if (other.IsValid()) EditorSceneManager.CloseScene(other, true);
                EditorSceneManager.CloseScene(first, true);
                SceneManager.SetActiveScene(original);
            }
        }
    }
}
