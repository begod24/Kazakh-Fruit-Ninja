using UnityEngine;

namespace KazakhNinja
{
    /// <summary>
    /// Scales a fixed-size layout down (never up) so it fits its parent, e.g. the collection grid
    /// on 4:3 tablets, where the canvas is narrower than on phones.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFitInParent : MonoBehaviour
    {
        void LateUpdate()
        {
            var rect = (RectTransform)transform;
            if (rect.parent is not RectTransform parent) return;
            Vector2 size = rect.rect.size, room = parent.rect.size;
            float scale = Mathf.Min(1f, room.x / Mathf.Max(size.x, 1f), room.y / Mathf.Max(size.y, 1f));
            if (!Mathf.Approximately(rect.localScale.x, scale)) rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
