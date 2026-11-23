using UnityEngine;

namespace Nomad.Minimap
{
    // X/Z를 북쪽 고정 지도 좌표로 바꿉니다. 정사각 범위로 종횡비를 보존합니다.
    public readonly struct MinimapProjection
    {
        public Bounds Bounds { get; }

        public MinimapProjection(Bounds worldBounds) : this(worldBounds, 4) { }

        private MinimapProjection(Bounds worldBounds, float padding)
        {
            float side = Mathf.Max(1, Mathf.Max(worldBounds.size.x, worldBounds.size.z)) + padding;
            Bounds = new Bounds(worldBounds.center, new Vector3(side, worldBounds.size.y, side));
        }

        public MinimapProjection Window(Vector3 center, float diameter)
        {
            float side = Mathf.Clamp(diameter, 1, Bounds.size.x);
            float half = side * 0.5f;
            center.x = Mathf.Clamp(center.x, Bounds.min.x + half, Bounds.max.x - half);
            center.z = Mathf.Clamp(center.z, Bounds.min.z + half, Bounds.max.z - half);
            return new MinimapProjection(new Bounds(center, new Vector3(side, Bounds.size.y, side)), 0);
        }

        public bool Contains(Vector3 point) => point.x >= Bounds.min.x && point.x <= Bounds.max.x &&
            point.z >= Bounds.min.z && point.z <= Bounds.max.z;

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
