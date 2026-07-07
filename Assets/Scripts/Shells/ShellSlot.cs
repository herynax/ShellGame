using UnityEngine;

namespace ShellGame.Shells
{
    /// <summary>
    /// Маркер точки на столе, куда может быть поставлен наперсток.
    /// Расставляются вручную в сцене (дочерние объекты стола) и собираются
    /// контроллером через GetComponentsInChildren при старте раунда.
    /// </summary>
    [ExecuteAlways]
    public sealed class ShellSlot : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private Vector3 _gizmoSize = new Vector3(0.22f, 0.06f, 0.22f);
        [SerializeField] private LayerMask _surfaceMask = -1;
        public int Index => _index;

        /// <summary>Занимающий слот наперсток, если есть. Управляется контроллером стола.</summary>
        public Shell OccupyingShell { get; set; }

        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        public Vector3 SpawnPosition
        {
            get
            {
                var origin = transform.position + Vector3.up * 0.5f;
                if (Physics.Raycast(origin, Vector3.down, out var hit, 5f, _surfaceMask))
                    return hit.point;

                var collider = GetComponent<Collider>();
                if (collider != null)
                {
                    var bounds = collider.bounds;
                    return new Vector3(transform.position.x, bounds.min.y, transform.position.z);
                }

                var size = _gizmoSize;
                if (transform.lossyScale != Vector3.one)
                    size = new Vector3(size.x * transform.lossyScale.x, size.y * transform.lossyScale.y, size.z * transform.lossyScale.z);

                return transform.position + Vector3.down * (size.y * 0.5f);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawSlotGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            DrawSlotGizmo();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
                boxCollider = gameObject.AddComponent<BoxCollider>();

            boxCollider.size = _gizmoSize;
            boxCollider.center = Vector3.zero;
            boxCollider.isTrigger = true;
        }

        private void DrawSlotGizmo()
        {
            var size = _gizmoSize;
            if (transform.lossyScale != Vector3.one)
                size = new Vector3(size.x * transform.lossyScale.x, size.y * transform.lossyScale.y, size.z * transform.lossyScale.z);

            Gizmos.color = OccupyingShell != null ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position, size);

            var spawnPosition = SpawnPosition;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPosition, 0.03f);
            Gizmos.DrawLine(transform.position, spawnPosition);
        }
#endif
    }
}
