using UnityEngine;

namespace KazakhNinja
{
    /// <summary>The game is played on a vertical XY plane in front of the camera.</summary>
    public static class Playfield
    {
        /// <summary>Where a screen point lands on the plane at depth <paramref name="z"/>.</summary>
        public static Vector3 ScreenToPlane(Camera camera, Vector2 screenPosition, float z = 0f)
        {
            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (Mathf.Abs(ray.direction.z) < 1e-6f) return ray.origin;
            return ray.GetPoint((z - ray.origin.z) / ray.direction.z);
        }

        /// <summary>The part of the plane at depth <paramref name="z"/> that is visible on screen.</summary>
        public static Rect GetVisibleRect(Camera camera, float z = 0f)
        {
            Vector3 min = ScreenToPlane(camera, Vector2.zero, z);
            Vector3 max = ScreenToPlane(camera, new Vector2(camera.pixelWidth, camera.pixelHeight), z);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
