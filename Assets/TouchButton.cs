using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool isPressed { get; private set; } = false;

    [Header("Visual Feedback")]
    public Image targetGraphic;
    public Color normalColor = new Color(1f, 1f, 1f, 0.4f);
    public Color pressedColor = new Color(1f, 1f, 1f, 0.85f);

    private void Awake()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Image>();

        UpdateVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        UpdateVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        UpdateVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPressed = false;
        UpdateVisual();
    }

    private void OnDisable()
    {
        isPressed = false;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (targetGraphic != null)
        {
            targetGraphic.color = isPressed ? pressedColor : normalColor;
        }
    }
}
