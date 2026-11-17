using UnityEngine;

namespace Nomad.Terrain
{
    [DisallowMultipleComponent]
    public sealed class TerrainConnection : MonoBehaviour
    {
        [SerializeField] private string connectionType = "Walk";
        [SerializeField, Min(2f)] private float width = 2f;
        [SerializeField, Min(2.2f)] private float clearance = 2.2f;
        [SerializeField, Min(0f)] private float seamDepth = 0.02f;

        public string ConnectionType => connectionType;
        public float Width => width;
        public float Clearance => clearance;
        public Vector3 Position => transform.position;
        public Vector3 Outward => transform.forward;

        // 연결부 예외는 통로 폭과 경계 양쪽 1cm 안으로만 제한합니다.
        public Bounds LocalSeamBounds => new Bounds(
            Vector3.up * (clearance * 0.5f), new Vector3(width, clearance, seamDepth));

        public bool Matches(TerrainConnection other)
        {
            return other != null && connectionType == other.connectionType
                && Mathf.Abs(width - other.width) <= 0.01f
                && Mathf.Abs(clearance - other.clearance) <= 0.01f
                && Vector3.Distance(Position, other.Position) <= 0.01f
                && Vector3.Angle(Outward, -other.Outward) <= 0.1f
                && Vector3.Angle(transform.up, other.transform.up) <= 0.1f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * clearance * 0.5f, new Vector3(width, clearance, 0.02f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward);
        }
    }
}
