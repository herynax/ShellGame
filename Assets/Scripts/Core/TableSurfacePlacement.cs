using UnityEngine;

namespace ShellGame.Core
{
    public static class TableSurfacePlacement
    {
        private const float RayStartHeight = 1000f;
        private const float RayDistance = 2000f;
        private static readonly int TableLayer = LayerMask.NameToLayer("Table");

        public static bool TryGetSurfacePoint(Vector3 position, out Vector3 surfacePoint)
        {
            surfacePoint = position;
            if (TableLayer < 0)
            {
                Debug.LogError("[TableSurfacePlacement] Слой 'Table' не найден в настройках проекта.");
                return false;
            }

            int tableMask = 1 << TableLayer;
            Vector3 origin = new Vector3(position.x, position.y + RayStartHeight, position.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayDistance, tableMask, QueryTriggerInteraction.Ignore))
                return false;

            surfacePoint = hit.point;
            return true;
        }

        public static Vector3 GetSpawnPoint(Vector3 position)
        {
            return TryGetSurfacePoint(position, out Vector3 surfacePoint) ? surfacePoint : position;
        }

        public static void PlaceObjectOnSurface(Transform objectTransform, Vector3 surfacePoint)
        {
            if (objectTransform == null)
                return;

            if (!TryGetWorldBounds(objectTransform, out Bounds bounds))
                return;

            objectTransform.position += Vector3.up * (surfacePoint.y - bounds.min.y);
        }

        public static Vector3 GetObjectPositionOnSurface(Transform objectTransform, Vector3 surfacePoint)
        {
            if (objectTransform == null || !TryGetWorldBounds(objectTransform, out Bounds bounds))
                return surfacePoint;

            float verticalCorrection = surfacePoint.y - bounds.min.y;
            return new Vector3(
                surfacePoint.x,
                objectTransform.position.y + verticalCorrection,
                surfacePoint.z);
        }

        private static bool TryGetWorldBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            bounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            foreach (Collider collider in colliders)
            {
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }
    }
}