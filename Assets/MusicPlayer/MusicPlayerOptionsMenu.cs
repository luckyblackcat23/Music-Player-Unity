using System.Runtime.CompilerServices;
using UnityEngine.UIElements;
using UnityEngine;

public class MusicPlayerOptionsMenu : MonoBehaviour
{
    [SerializeField] PanelRenderer document;
    [SerializeField] PanelSettings panelSettings;

    bool visible;

    VisualElement Root;
    //VisualElement topBar;
    Button exitButton;

    //Option Elements
    FloatField themeRed;
    FloatField themeGreen;
    FloatField themeBlue;

    EnumField screenScaleMode;
    
    int uiVersion = 0;

    void Awake()
    {
        document.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDestroy()
    {
        document.UnregisterUIReloadCallback(OnUIReload);
    }

    private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        // The version number only changes when the UI actually reloads, 
        // so this checks prevents duplicated elements.
        if (uiVersion == version)
            return;

        uiVersion = version;

        Root = root;

        //topBar = Root.Q<VisualElement>("TopBar");
        exitButton = Root.Q<Button>("ExitButton");

        themeRed = Root.Q<FloatField>("ThemeRed");
        themeGreen = Root.Q<FloatField>("ThemeGreen");
        themeBlue = Root.Q<FloatField>("ThemeBlue");

        screenScaleMode = Root.Q<EnumField>("ScreenScaleMode");

        /* maybe reimplement later
        if (TopBar != null)
        {
            TopBar.RegisterCallback<PointerDownEvent>(startDragging);
            TopBar.RegisterCallback<PointerUpEvent>(endDragging);
            TopBar.RegisterCallback<PointerMoveEvent>(moveWindow);
            TopBar.RegisterCallback<PointerOutEvent>(outWindow);
        }
        */

        exitButton.clicked += ExitOptionsMenu;

        themeRed.RegisterValueChangedCallback((evt) => accentRed = evt.newValue);
        themeGreen.RegisterValueChangedCallback((evt) => accentGreen = evt.newValue);
        themeBlue.RegisterValueChangedCallback((evt) => accentBlue = evt.newValue);

        screenScaleMode.RegisterValueChangedCallback((evt) => panelSettings.scaleMode = (PanelScaleMode)evt.newValue);

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    private void ExitOptionsMenu()
    {
        Root.style.display = DisplayStyle.None;
        visible = false;
    }

    public void ShowOptionsMenu()
    {
        Root.style.display = DisplayStyle.Flex;
        visible = true;
    }

    /*
    Vector3 dragPosition;
    bool isDragging;

    private void outWindow(PointerOutEvent evt)
    {
        if (isDragging)
        {
            var diff = evt.localPosition - dragPosition;
            Root.style.left = Root.resolvedStyle.left + diff.x;
            Root.style.top = Root.resolvedStyle.top + diff.y;
        }
    }

    private void moveWindow(PointerMoveEvent evt)
    {
        if (isDragging)
        {
            var diff = evt.localPosition - dragPosition;
            Root.style.left = Root.resolvedStyle.left + diff.x;
            Root.style.top = Root.resolvedStyle.top + diff.y;
        }
    }

    private void endDragging(PointerUpEvent evt)
    {
        isDragging = false;
    }

    private void startDragging(PointerDownEvent evt)
    {
        isDragging = true;
        dragPosition = evt.localPosition;
    }
    */

    float accentRed;
    float accentGreen;
    float accentBlue;

    public void UpdateAccentColour()
    {

    }
}
