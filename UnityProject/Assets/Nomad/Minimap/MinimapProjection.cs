using UnityEngine;

namespace Nomad.Minimap
{
    // X/Z를 북쪽 고정 지도 좌표로 바꿉니다. 정사각 범위로 종횡비를 보존합니다.
    public readonly struct MinimapProjection
    {
        public Bounds Bounds { get; }

        public MinimapProjection(Bounds worldBounds)
        {
            float side = Mathf.Max(1, Mathf.Max(worldBounds.size.x, worldBounds.size.z)) + 4;
            Bounds = new Bounds(worldBounds.center, new Vector3(side, worldBounds.size.y, side));
        }

        public Vector2 Normalize(Vector3 world) => new Vector2(
            (world.x - Bounds.min.x) / Bounds.size.x,
            (world.z - Bounds.min.z) / Bounds.size.z);

        public Vector2 Marker(Vector3 world, float size, float inset = 7)
        {
            Vector2 point = Normalize(world) * size;
            return new Vector2(Mathf.Clamp(point.x, inset, size - inset), Mathf.Clamp(point.y, inset, size - inset));
        }

        public Vector3 World(float u, float v) => new Vector3(
            Mathf.Lerp(Bounds.min.x, Bounds.max.x, u), Bounds.center.y,
            Mathf.Lerp(Bounds.min.z, Bounds.max.z, v));
    }
}
