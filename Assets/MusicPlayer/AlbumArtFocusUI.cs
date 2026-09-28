using UnityEngine.UIElements;
using UnityEngine;

public class AlbumArtFocusUI : MonoBehaviour
{
    [SerializeField] PanelRenderer document;

    public static bool visible;
    static VisualElement Root;

    VisualElement Background;

    Image albumArt;
    
    AccentButton previousButton;
    AccentButton playButton;
    AccentButton nextButton;

    int uiVersion = 0;

    MusicPlayer musicPlayer;

    void OnEnable()
    {
        musicPlayer = MusicPlayer.instance;

        MusicPlayerUIController.OnAccentChange += UpdateAccents;

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

        Background = root.Q<VisualElement>("Background");

        albumArt = root.Q<Image>("AlbumArt");

        previousButton = root.Q<AccentButton>("Previous");
        playButton = root.Q<AccentButton>("Play");
        nextButton = root.Q<AccentButton>("Next");

        musicPlayer.OnSongChange.AddListener(() =>
        {
            SongInfo currentSong = musicPlayer.CurrentSong();

            Debug.Log(currentSong.Title);

            if (currentSong == null)
                return;

            if (currentSong.MetaDataLoaded)
            {
                albumArt.style.backgroundImage = new StyleBackground(currentSong.AlbumCover);
            }
            else
            {
                currentSong.GetSongInfo(true);

                currentSong.OnMetaDataLoaded += () =>
                {
                    // Make sure this is still the active song
                    if (musicPlayer.CurrentSong() != currentSong)
                        return;

                    albumArt.style.backgroundImage = new StyleBackground(currentSong.AlbumCover);
                };
            }
        });

        previousButton.clicked += PlayPrevious;
        playButton.clicked += TogglePause;
        nextButton.clicked += PlayNext;

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    void PlayNext()
    {
        musicPlayer.PlayNext();
    }

    void TogglePause()
    {
        musicPlayer.TogglePause();
    }

    void PlayPrevious()
    {
        musicPlayer.PlayPrevious();
    }

    public static void ExitFocusMenu()
    {
        Root.style.display = DisplayStyle.None;
        visible = false;
    }

    public static void ShowFocusMenu()
    {
        Root.style.display = DisplayStyle.Flex;
        visible = true;
    }

    public void UpdateAccents()
    {
        MaterialDefinition mat = Background.resolvedStyle.unityMaterial;
        mat.SetColor("_Color", MusicPlayerUIController.accent.Get());
        Background.style.unityMaterial = mat;

        foreach (AccentButton button in Root.Query<AccentButton>().ToList())
        {
            button.color = MusicPlayerUIController.accentLight;
        }

        foreach (VisualElement element in Root.Query(className: "accentImageTint").ToList())
        {
            if (!element.ClassListContains("ButtonHover"))
                element.style.unityBackgroundImageTintColor = MusicPlayerUIController.accentLight;
        }

        foreach (VisualElement element in Root.Query(className: "accentBackgroundColor").ToList())
        {
            element.style.backgroundColor = MusicPlayerUIController.accent.Get();
        }

        foreach (VisualElement element in Root.Query(className: "accentColor").ToList())
        {
            element.style.color = MusicPlayerUIController.accent.Get();
        }
    }
}
