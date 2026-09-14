using UnityEngine;
using UnityEngine.EventSystems;

// Pointer-to-grid translation for the editable map preview. Keeping input out
// of GameSetupUI lets that class focus on constructing and refreshing screens.
public sealed class MapDesignerCanvas : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private GameSetupUI designer;
    private RectTransform rect;
    private bool painting;

    public void Initialize(GameSetupUI owner)
    {
        designer = owner;
        rect = (RectTransform)transform;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        painting = true;
        Paint(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (painting) Paint(eventData);
    }

    public void OnPointerUp(PointerEventData eventData) => painting = false;

    private void Paint(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
            eventData.pressEventCamera, out Vector2 local)) return;
        Rect bounds = rect.rect;
        float u = Mathf.InverseLerp(bounds.xMin, bounds.xMax, local.x);
        float v = Mathf.InverseLerp(bounds.yMin, bounds.yMax, local.y);
        designer.PaintAtNormalized(u, v);
    }
}
