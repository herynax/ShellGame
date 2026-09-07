using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    [ExecuteAlways]
    public sealed class ItemSpawnPoint : MonoBehaviour
    {
        [SerializeField] private TurnSide _owner = TurnSide.Player;
        [SerializeField] private int _index;
        [SerializeField] private Vector3 _gizmoSize = new Vector3(0.22f, 0.06f, 0.22f);

        public TurnSide Owner => _owner;
        public int Index => _index;
        public Vector3 SpawnPosition
        {
            get
            {
                return TableSurfacePlacement.GetSpawnPoint(transform.position);
            }
        }

        public Quaternion Rotation => transform.rotation;

#if UNITY_EDITOR
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

        private void OnDrawGizmos()
        {
            Gizmos.color = _owner == TurnSide.Player ? Color.cyan : Color.magenta;
            Gizmos.DrawWireCube(transform.position, _gizmoSize);
            Gizmos.DrawWireSphere(SpawnPosition, 0.03f);
            Gizmos.DrawLine(transform.position, SpawnPosition);
        }
#endif
    }
}