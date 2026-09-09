using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [SerializeField, Range(0.05f, 0.6f)] private float scrollZoomStrength = 0.28f;
    [SerializeField, Range(1f, 8f)] private float dragSpeed = 3.2f;
    [SerializeField] private float keyboardSpeed = 1.8f;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 24f;
    private Camera mapCamera;
    private Bounds mapBounds;
    private bool hasBounds;

    private void Awake() => mapCamera = GetComponent<Camera>();

    public void SetMapBounds(Bounds bounds)
    {
        mapBounds = bounds;
        hasBounds = true;
        FrameMap();
    }

    public void FrameMap()
    {
        if (!hasBounds) return;
        float size = Mathf.Max(mapBounds.extents.y,
            mapBounds.extents.x / mapCamera.aspect) * 1.08f;
        maxZoom = Mathf.Max(maxZoom, size * 1.3f);
        mapCamera.orthographicSize = Mathf.Clamp(size, minZoom, maxZoom);
        transform.position = new Vector3(mapBounds.center.x, mapBounds.center.y, transform.position.z);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
            {
                // Input System versions/platforms report either ticks or ~120 units/tick.
                float ticks = Mathf.Abs(scroll) >= 100f ? scroll / 120f : scroll;
                Vector3 screen = mouse.position.ReadValue();
                screen.z = Mathf.Abs(transform.position.z - mapBounds.center.z);
                Vector3 before = mapCamera.ScreenToWorldPoint(screen);
                mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize *
                    Mathf.Exp(-Mathf.Clamp(ticks, -6f, 6f) * scrollZoomStrength), minZoom, maxZoom);
                transform.position += before - mapCamera.ScreenToWorldPoint(screen);
            }
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float unitsPerPixel = 2f * mapCamera.orthographicSize / mapCamera.pixelHeight;
                transform.position -= (transform.right * delta.x + transform.up * delta.y) *
                    unitsPerPixel * dragSpeed;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            Vector2 direction = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x++;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.y--;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.y++;
            float boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 3f : 1f;
            Vector2 move = direction.normalized * (mapCamera.orthographicSize * keyboardSpeed * boost * Time.deltaTime);
            transform.position += new Vector3(move.x, move.y, 0f);
            if (keyboard.fKey.wasPressedThisFrame) FrameMap();
        }
        if (hasBounds)
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, mapBounds.min.x, mapBounds.max.x);
            position.y = Mathf.Clamp(position.y, mapBounds.min.y, mapBounds.max.y);
            transform.position = position;
        }
    }
}
