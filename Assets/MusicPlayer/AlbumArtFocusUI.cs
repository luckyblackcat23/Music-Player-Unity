using UnityEngine.UIElements;
using UnityEngine;

public class AlbumArtFocusUI : MonoBehaviour
{
    [SerializeField] PanelRenderer document;

    public static bool visible;
    static VisualElement Root;

    VisualElement Background;

    Image albumArt;

    AccentButton exitButton;

    AccentButton previousButton;
    AccentButton playButton;
    AccentButton nextButton;

    Label songArtist;
    Label songTitle;
    Label songAlbum;

    int uiVersion = 0;

    void OnEnable()
    {
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

        exitButton = root.Q<AccentButton>("Exit");

        previousButton = root.Q<AccentButton>("Previous");
        playButton = root.Q<AccentButton>("Play");
        nextButton = root.Q<AccentButton>("Next");

        songArtist = root.Q<Label>("Artist");
        songTitle = root.Q<Label>("Title");
        songAlbum = root.Q<Label>("Album");

        MusicPlayer.OnSongChange.AddListener(() =>
        {
            SongInfo currentSong = MusicPlayer.CurrentSong();

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
                    if (MusicPlayer .CurrentSong() != currentSong)
                        return;

                    albumArt.style.backgroundImage = new StyleBackground(currentSong.AlbumCover);
                };
            }

            songArtist.text = currentSong.Artist;
            songTitle.text = currentSong.Title;
            songAlbum.text = currentSong.Album;
        });

        exitButton.clicked += ExitFocusMenu;

        previousButton.clicked += PlayPrevious;
        playButton.clicked += TogglePause;
        nextButton.clicked += PlayNext;

        //hide on start
        Root.style.display = DisplayStyle.None;
    }

    void PlayNext()
    {
        MusicPlayer .PlayNext();
    }

    void TogglePause()
    {
        MusicPlayer .TogglePause();
    }

    void PlayPrevious()
    {
        MusicPlayer .PlayPrevious();
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
