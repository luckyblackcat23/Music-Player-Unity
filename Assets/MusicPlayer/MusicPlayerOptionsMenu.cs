using System.Runtime.CompilerServices;
using UnityEngine.UIElements;
using UnityEngine;

public class MusicPlayerOptionsMenu : MonoBehaviour
{
    [SerializeField] PanelRenderer document;
    [SerializeField] PanelSettings panelSettings;

    public static bool visible;
    static VisualElement Root;

    Button exitButton;

    //Option Elements
    //accent theme
    Slider themeRed;
    Slider themeGreen;
    Slider themeBlue;

    Button resetAccent;

    //screen scale mode
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

        themeRed = Root.Q<Slider>("ThemeRed");
        themeGreen = Root.Q<Slider>("ThemeGreen");
        themeBlue = Root.Q<Slider>("ThemeBlue");

        resetAccent = Root.Q<Button>("ResetAccent");

        screenScaleMode = Root.Q<EnumField>("ScreenScaleMode");

        exitButton.clicked += ExitOptionsMenu;

        themeRed.value = MusicPlayerUIController.accent.Get().r;
        themeGreen.value = MusicPlayerUIController.accent.Get().g;
        themeBlue.value = MusicPlayerUIController.accent.Get().b;

        themeRed.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetRed(evt.newValue));
        themeGreen.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetGreen(evt.newValue));
        themeBlue.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetBlue(evt.newValue));

        resetAccent.clicked += () => MusicPlayerUIController.accent.Set(SystemTheme.GetAccentColour());

        screenScaleMode.RegisterValueChangedCallback((evt) => panelSettings.scaleMode = (PanelScaleMode)evt.newValue);

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    public static void ExitOptionsMenu()
    {
        Root.style.display = DisplayStyle.None;
        visible = false;
    }

    public static void ShowOptionsMenu()
    {
        Root.style.display = DisplayStyle.Flex;
        visible = true;
    }
}
