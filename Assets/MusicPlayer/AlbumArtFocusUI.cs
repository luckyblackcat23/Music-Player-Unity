using System.Runtime.CompilerServices;
using UnityEngine.UIElements;
using UnityEngine;

public class AlbumArtFocusUI : MonoBehaviour
{
    [SerializeField] PanelRenderer document;
    [SerializeField] PanelSettings panelSettings;

    public static bool visible;
    static VisualElement Root;

    AccentButton previousButton;
    AccentButton playButton;
    AccentButton nextButton;

    int uiVersion = 0;

    MusicPlayer musicPlayer;

    void Awake()
    {
        musicPlayer = MusicPlayer.instance;
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

        previousButton = root.Q<AccentButton>("Previous");
        playButton = root.Q<AccentButton>("Play");
        nextButton = root.Q<AccentButton>("Next");

        previousButton.clicked += PlayPrevious;
        playButton.clicked += musicPlayer.TogglePause;
        nextButton.clicked += PlayNext;

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    void PlayNext()
    {
        musicPlayer.PlayNext();
    }

    void PlayPrevious()
    {
        musicPlayer.PlayPrevious();
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
