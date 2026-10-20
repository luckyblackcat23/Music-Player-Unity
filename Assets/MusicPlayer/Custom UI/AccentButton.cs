using UnityEngine.UIElements;
using UnityEngine;

[UxmlElement]
partial class AccentButton : Button
{
    Color _color;

    [UxmlAttribute]
    public Color color
    {
        get => _color;
        set
        {
            _color = value;
            SetAccent(value);
        }
    }

    [UxmlAttribute]
    new public Background iconImage
    {
        get => accentImage.style.backgroundImage.value;
        set => accentImage.style.backgroundImage = value;
    }

    public StyleBackground IconImage
    {
        get => iconImage;
        set => accentImage.style.backgroundImage = value;
    }



    private VisualElement accentImage;

    public AccentButton()
    {
        accentImage = new VisualElement();
        accentImage.style.flexGrow = 1;
        accentImage.style.aspectRatio = 1;
        accentImage.pickingMode = PickingMode.Ignore;
        accentImage.AddToClassList("accent-button-image");
        
        hierarchy.Add(accentImage);

        RegisterCallback<MouseEnterEvent>(OnMouseEnter);
        RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
        RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);

        style.paddingTop = 0;
        style.paddingBottom = 0;
        style.paddingLeft = 0;
        style.paddingRight = 0;

        color = new Color(1, 0.7490196f, 0);

        if (MusicPlayerUIController.accentLight.a != 0)
            color = MusicPlayerUIController.accentLight;
    }

    bool isHovered;

    private void SetAccent(Color accent)
    {
        accentImage.style.unityBackgroundImageTintColor = accent;
    }

    private void OnMouseEnter(MouseEnterEvent evt)
    {
        isHovered = true;
        SetAccent(Color.Lerp(color, Color.black, 0.3f));
    }

    private void OnMouseLeave(MouseLeaveEvent evt)
    {
        isHovered = false;
        SetAccent(color);
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        SetAccent(Color.Lerp(color, Color.black, 0.6f));
    }

    private void OnMouseUp(MouseUpEvent evt)
    {
        if (isHovered)
            SetAccent(Color.Lerp(color, Color.black, 0.3f));
        else
            SetAccent(color);
    }
}