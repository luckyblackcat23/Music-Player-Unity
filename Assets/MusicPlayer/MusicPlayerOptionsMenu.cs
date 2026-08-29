using System.Runtime.CompilerServices;
using UnityEngine.UIElements;
using UnityEngine;

public class MusicPlayerOptionsMenu : MonoBehaviour
{
    [SerializeField] PanelRenderer document;

    void Awake()
    {
        document.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDestroy()
    {
        document.UnregisterUIReloadCallback(OnUIReload);
    }

    VisualElement Root;
    VisualElement TopBar;
    Button ExitButton;
    int uiVersion = 0;

    private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        // The version number only changes when the UI actually reloads, 
        // so this checks prevents duplicated elements.
        if (uiVersion == version)
            return;

        uiVersion = version;

        Root = root;

        TopBar = Root.Q<VisualElement>("topBar");
        ExitButton = Root.Q<Button>("exitButton");

        /* maybe reimplement later
        if (TopBar != null)
        {
            TopBar.RegisterCallback<PointerDownEvent>(startDragging);
            TopBar.RegisterCallback<PointerUpEvent>(endDragging);
            TopBar.RegisterCallback<PointerMoveEvent>(moveWindow);
            TopBar.RegisterCallback<PointerOutEvent>(outWindow);
        }
        */

        ExitButton.clicked += ExitOptionsMenu;

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    private void ExitOptionsMenu()
    {
        Root.style.display = DisplayStyle.None;
    }

    Vector3 dragPosition;
    bool isDragging;
    
    /*
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
}
