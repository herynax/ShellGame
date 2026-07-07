using UnityEngine;

namespace ShellGame.Shells
{
    public sealed class Marker : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetWorldPosition(Vector3 position)
        {
            transform.position = position;
        }

        public void PlaceAtSurface(Vector3 surfacePoint)
        {
            transform.position = surfacePoint;
        }
    }
}
