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

    public static SaveEnum<PanelScaleMode> screenScaleModeSave = new SaveEnum<PanelScaleMode>("ScreenScaleMode");

    //audio normalisation
    Toggle audioNormalisation;

    //song info display mode
    EnumField songInfoDisplayMode;

    public static SaveEnum<SongInfoDisplayMode> SIDisplayMode = new SaveEnum<SongInfoDisplayMode>("SongInfoDisplayMode", defaultValue: SongInfoDisplayMode.ArtDisplay);

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

        audioNormalisation = Root.Q<Toggle>("AudioNormalisation");

        songInfoDisplayMode = Root.Q<EnumField>("SongInfoDisplayMode");

        exitButton.clicked += ExitOptionsMenu;

        themeRed.value = MusicPlayerUIController.accent.Get().r;
        themeGreen.value = MusicPlayerUIController.accent.Get().g;
        themeBlue.value = MusicPlayerUIController.accent.Get().b;

        themeRed.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetRed(evt.newValue));
        themeGreen.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetGreen(evt.newValue));
        themeBlue.RegisterValueChangedCallback((evt) => MusicPlayerUIController.accent.SetBlue(evt.newValue));

        themeRed.RegisterValueChangedCallback((evt) => MusicPlayerUIController.ApplyAccent());
        themeGreen.RegisterValueChangedCallback((evt) => MusicPlayerUIController.ApplyAccent());
        themeBlue.RegisterValueChangedCallback((evt) => MusicPlayerUIController.ApplyAccent());

        resetAccent.clicked += () => MusicPlayerUIController.accent.Set(SystemTheme.GetAccentColour());

        screenScaleMode.value = screenScaleModeSave.Get();

        screenScaleMode.RegisterValueChangedCallback((evt) =>
        {
            screenScaleModeSave.Set((PanelScaleMode)evt.newValue);
            panelSettings.scaleMode = (PanelScaleMode)evt.newValue;
        });

        audioNormalisation.value = MusicPlayer.AudioNormalisationEnabled;

        audioNormalisation.RegisterValueChangedCallback((evt) => MusicPlayer.AudioNormalisationEnabled.Set(evt.newValue));

        songInfoDisplayMode.value = SIDisplayMode.Get();

        songInfoDisplayMode.RegisterValueChangedCallback((evt) =>
        {
            SIDisplayMode.Set((SongInfoDisplayMode)evt.newValue);
            MusicPlayerUIController.songInfoDisplayMode = (SongInfoDisplayMode)evt.newValue;
        });

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    public static void ExitOptionsMenu()
    {
        SaveManager.MainSave().Save();
        Root.style.display = DisplayStyle.None;
        visible = false;
    }

    public static void ShowOptionsMenu()
    {
        Root.style.display = DisplayStyle.Flex;
        visible = true;
    }
}

public enum SongInfoDisplayMode
{
    [Tooltip("much MUCH faster for loading entries")]
    FlatDisplay,
    [Tooltip("Slower, due to loading album art being expensive")]
    ArtDisplay
}