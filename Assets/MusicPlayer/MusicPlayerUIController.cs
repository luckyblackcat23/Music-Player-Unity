using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using MyBox;
using Tools;
using System;

public class MusicPlayerUIController : MonoBehaviour
{
    class ListItemData
    {
        public FileNode playlist;
        public SongInfo Song;
        public Action MetadataCallback;
    }

    public bool playbackSliderDragged;

    [Header("UI")]
    [SerializeField] PanelRenderer document;
    [SerializeField] VisualTreeAsset songItemTemplate;
    [SerializeField] VisualTreeAsset PlaylistItemTemplate;

    public static VisualElement root;

    ContextMenu contextMenu;

    [Header("Data")]
    List<SongInfo> songs = new();
    List<SongInfo> queue = new();
    FileNode playlistDirectory;
    FileNode currentPlaylistDirectory;

    List<SongInfo> searchTempSongs = new();
    List<SongInfo> searchTempSongsQueue = new();
    List<FileNode> searchTempSongsPlaylists = new();

    VisualElement popOutView;

    ListView songQueue;

    ListView playlistList;
    AccentButton playlistListBackButton;

    ListView songList;

    AccentButton shuffleAllButton;

    TextField searchBar;
    TextField searchBarQueue;
    TextField searchBarPlaylists;

    // Playback bar

    VisualElement playbackAlbumArt;
    Label songTitle;
    Label songArtist;

    AccentButton popOutButton;
    AccentButton settingsButton;

    AccentButton shuffleButton;
    AccentButton previousButton;
    AccentButton playButton;
    AccentButton nextButton;
    AccentButton loopButton;

    Slider volumeSlider;

    Slider playbackSlider;
    Label playbackTime;

    [Header("Resources")]
    public Sprite PopOutViewOutIcon;
    public Sprite PopOutViewInIcon;

    public Sprite ShuffleIcon;
    public Sprite DontShuffleIcon;

    public Sprite DontLoopIcon;
    public Sprite LoopIcon;
    public Sprite LoopSingleIcon;

    public Sprite FolderIcon;

    void OnEnable()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(Initialize);
    }

    void OnDisable()
    {
        if (songQueue != null)
            songQueue.itemsSource = null;

        if (playlistList != null)
            playlistList.itemsSource = null;

        if (songList != null)
            songList.itemsSource = null;

        playbackSlider.UnregisterCallback<MouseCaptureEvent>(OnPlaybackDrag);
        playbackSlider.UnregisterCallback<MouseCaptureOutEvent>(OnPlaybackRelease);

        MusicPlayer.OnSongShuffle.RemoveListener(RefreshSongQueue);
        MusicPlayer.OnSongChange.RemoveListener(RefreshSongQueue);

        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(Initialize);
    }

    private int lastDisplayedSecond = -1;

    private void Update()
    {
        if (!MusicPlayer.paused && !playbackSliderDragged)
            playbackSlider.value = MusicPlayer.playbackTime.Get();

        int currentSecond = Mathf.FloorToInt(MusicPlayer.playbackTime);

        if (currentSecond != lastDisplayedSecond && playbackTime != null)
        {
            lastDisplayedSecond = currentSecond;

            TimeSpan time = TimeSpan.FromSeconds(currentSecond);

            playbackTime.text = time.ToString(@"mm\:ss");
        }
    }

    int uiVersion = 0;
    void Initialize(PanelRenderer panelRenderer, VisualElement root_, int version)
    {
        // The version number only changes when the UI actually reloads, 
        // so this checks prevents duplicated elements.
        if (uiVersion == version)
            return;

        uiVersion = version;

        root = root_;

        ApplyAccent();

        contextMenu = new ContextMenu(root);

        popOutView = root.Q<VisualElement>("PopOutView");

        songQueue = root.Q<ListView>("SongQueue");

        playlistList = root.Q<ListView>("PlaylistList");
        playlistListBackButton = root.Q<AccentButton>("PlaylistListBack");

        songList = root.Q<ListView>("SongList");

        shuffleAllButton = root.Q<AccentButton>("ShuffleAll");

        searchBarQueue = root.Q<TextField>("SearchBarQueue");
        searchBarPlaylists = root.Q<TextField>("SearchBarPlaylists");
        searchBar = root.Q<TextField>("SearchBar");

        playbackAlbumArt = root.Q<VisualElement>("AlbumArt");
        songTitle = root.Q<Label>("SongTitle");
        songArtist = root.Q<Label>("SongArtist");

        popOutButton = root.Q<AccentButton>("PopOutButton");
        settingsButton = root.Q<AccentButton>("SettingsButton");

        shuffleButton = root.Q<AccentButton>("Shuffle");
        previousButton = root.Q<AccentButton>("Previous");
        playButton = root.Q<AccentButton>("Play");
        nextButton = root.Q<AccentButton>("Next");
        loopButton = root.Q<AccentButton>("Loop");

        volumeSlider = root.Q<Slider>("VolumeSlider");

        playbackTime = root.Q<Label>("PlaybackTime");
        playbackSlider = root.Q<Slider>("PlaybackSlider");

        //initialize song change event
        MusicPlayer.OnSongChange.AddListener(() =>
        {
            SongInfo currentSong = MusicPlayer.CurrentSong();

            Debug.Log(currentSong.Title);

            if (currentSong == null)
                return;

            playbackSlider.highValue = (float)currentSong.Duration;

            if (currentSong.MetaDataLoaded)
            {
                playbackAlbumArt.style.backgroundImage = new StyleBackground(currentSong.AlbumCover);

                songTitle.text = currentSong.Title;
                songArtist.text = currentSong.Artist;
            }
            else
            {
                currentSong.GetSongInfo(true);

                currentSong.OnMetaDataLoaded += () =>
                {
                    // Make sure this is still the active song
                    if (MusicPlayer.CurrentSong() != currentSong)
                        return;

                    playbackAlbumArt.style.backgroundImage = new StyleBackground(currentSong.AlbumCover);

                    songTitle.text = currentSong.Title;
                    songArtist.text = currentSong.Artist;
                };
            }

            RefreshSongQueue();
        });

        #region queue
        if (songQueue == null)
        {
            Debug.LogError("ListView 'SongQueue' not found.");
            return;
        }

        songQueue.fixedItemHeight = 100;

        queue = MusicPlayer.musicQueue;

        songQueue.itemsSource = queue;

        songQueue.makeItem = () =>
        {
            VisualElement item = songItemTemplate.Instantiate();

            item.RegisterCallback<PointerDownEvent>(OnQueueItemRightClick);

            return item;
        };

        songQueue.bindItem = BindQueueItem;

        songQueue.selectionType = SelectionType.Single;

        songQueue.itemIndexChanged += (oldIndex, newIndex) =>
        {
            if (MusicPlayer.currentSongIndex == oldIndex)
            {
                MusicPlayer.currentSongIndex = newIndex;
            }
            else if (oldIndex < MusicPlayer.currentSongIndex && newIndex >= MusicPlayer.currentSongIndex)
            {
                // An item before the current song moved after it.
                // Current song shifts left.
                MusicPlayer.currentSongIndex--;
            }
            else if (oldIndex > MusicPlayer.currentSongIndex && newIndex <= MusicPlayer.currentSongIndex)
            {
                // An item after the current song moved before it.
                // Current song shifts right.
                MusicPlayer.currentSongIndex++;
            }

            RefreshSongQueue();
        };
        #endregion

        #region playlistList
        if (playlistList == null)
        {
            Debug.LogError("ListView 'PlaylistList' not found.");
            return;
        }

        playlistList.fixedItemHeight = 100;

        playlistDirectory = MusicPlayer.PlaylistDirectoryNode;
        currentPlaylistDirectory = playlistDirectory;

        playlistList.itemsSource = playlistDirectory.Children;

        playlistList.makeItem = () =>
        {
            VisualElement item = PlaylistItemTemplate.Instantiate();

            item.RegisterCallback<PointerDownEvent>(OnPlaylistItemRightClick);

            return item;
        };

        playlistList.bindItem = BindPlaylistListItem;

        playlistList.selectionType = SelectionType.Single;
        #endregion

        #region songList
        if (songList == null)
        {
            Debug.LogError("ListView 'SongList' not found.");
            return;
        }

        songList.fixedItemHeight = 100;

        songs = MusicPlayer.cachedSongs.ToList();

        songList.itemsSource = songs;

        songList.makeItem = () =>
        {
            VisualElement item = songItemTemplate.Instantiate();

            item.RegisterCallback<PointerDownEvent>(OnSongItemRightClick);

            return item;
        };

        songList.bindItem = BindSongListItem;
        songList.unbindItem = UnbindSongListItem;

        songList.selectionType = SelectionType.Single;

        playlistListBackButton.clicked += () => ChangeDisplayedPlaylistDirectory(currentPlaylistDirectory.Parent);
        #endregion

        searchBar.RegisterCallback<ChangeEvent<string>>((x) =>
        {
            searchTempSongs.Clear();

            foreach (SongInfo song in songs)
            {
                //search Title
                if (song.Title != null)
                    if (song.Title.Contains(x.newValue, StringComparison.OrdinalIgnoreCase))
                        searchTempSongs.Add(song);
            }

            if (x.newValue == string.Empty)
                songList.itemsSource = songs;
            else
                songList.itemsSource = searchTempSongs;

            RefreshSongList();
        });

        shuffleAllButton.clicked += ShuffleAll;

        searchBarQueue.RegisterCallback<ChangeEvent<string>>((x) =>
        {
            searchTempSongsQueue.Clear();

            foreach (SongInfo song in queue)
            {
                //search Title
                if (song.Title != null)
                    if (song.Title.Contains(x.newValue, System.StringComparison.OrdinalIgnoreCase))
                        searchTempSongsQueue.Add(song);
            }

            if (string.IsNullOrEmpty(x.newValue))
                songQueue.itemsSource = queue;
            else
                songQueue.itemsSource = searchTempSongsQueue;

            songQueue.Rebuild();
        });

        //maybe implement a way to differentiate between checking every folder and checking just the active one
        searchBarPlaylists.RegisterCallback<ChangeEvent<string>>((x) =>
        {
            searchTempSongsPlaylists.Clear();

            foreach (FileNode playlist in playlistDirectory.GetAllChildren())
            {
                if (!playlist.IsDirectory)
                    if (playlist.Name.Contains(x.newValue, StringComparison.OrdinalIgnoreCase))
                        searchTempSongsPlaylists.Add(playlist);
            }

            if (string.IsNullOrEmpty(x.newValue))
                playlistList.itemsSource = playlistDirectory.Children;
            else
                playlistList.itemsSource = searchTempSongsPlaylists;

            RefreshPlaylistList();
        });

        playbackAlbumArt.RegisterCallback<MouseUpEvent>(evt => AlbumArtFocusUI.ShowFocusMenu());

        popOutButton.clicked += TogglePopOutView;
        settingsButton.clicked += MusicPlayerOptionsMenu.ShowOptionsMenu;

        shuffleButton.clicked += () =>
        {
            MusicPlayer.ShuffleQueue();
            RefreshSongQueue();
        };
        previousButton.clicked += PlayPrevious;
        playButton.clicked += MusicPlayer.TogglePause;
        nextButton.clicked += PlayNext;
        loopButton.clicked += () =>
        {
            MusicPlayer.IncrementLoop();
            switch (MusicPlayer.Loop)
            {
                case MusicPlayer.LoopOptions.dontLoop:
                    loopButton.IconImage = new StyleBackground(DontLoopIcon);
                    break;
                case MusicPlayer.LoopOptions.loop:
                    loopButton.IconImage = new StyleBackground(LoopIcon);
                    break;
                case MusicPlayer.LoopOptions.loopSingle:
                    loopButton.IconImage = new StyleBackground(LoopSingleIcon);
                    break;
            }
        };

        volumeSlider.RegisterValueChangedCallback(evt => MusicPlayer.UserVolume = evt.newValue);

        MusicPlayer.OnSongShuffle.AddListener(RefreshSongQueue);
        MusicPlayer.OnSongChange.AddListener(RefreshSongQueue);

        volumeSlider.value = MusicPlayer.UserVolume; // change to saved value later

        playbackSlider.RegisterCallback<MouseCaptureEvent>(OnPlaybackDrag);
        playbackSlider.RegisterCallback<MouseCaptureOutEvent>(OnPlaybackRelease);
    }

    void BindQueueItem(VisualElement element, int index)
    {
        SongInfo song = ((List<SongInfo>)songQueue.itemsSource)[index];

        if (song != null)
        {
            if (!song.MetaDataLoaded)
            {
                song.GetSongInfo();
                song.OnMetaDataLoaded += () => songQueue.RefreshItem(index);
            }

            element.Q<Label>("title").text = song.Title;
            element.Q<Label>("artist").text = song.Artist;
            element.Q<Label>("duration").text =
                song.MetaDataLoaded ? song.Duration.ToString() : "--:--";

            element.userData = song;

            VisualElement albumArt = element.Q<VisualElement>("albumArt");
            albumArt.style.backgroundImage = song.AlbumCover;

            Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

            thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as Action;

            Action action = () => PlaySongFromQueue(index);

            thumbnailPlayButton.userData = action;
            thumbnailPlayButton.clicked += action;

            if (index == MusicPlayer.currentSongIndex)
                element.AddToClassList("CurrentSong");
            else
                element.RemoveFromClassList("CurrentSong");
        }
        else
        {
            Debug.Log("error while loading song " + index);
        }
    }

    void BindPlaylistListItem(VisualElement element, int index)
    {
        FileNode node = ((List<FileNode>)playlistList.itemsSource)[index];

        if (node.IsDirectory)
        {
            element.AddToClassList("Directory");

            element.Q<Label>("title").text = node.Name;

            VisualElement albumArt = element.Q<VisualElement>("albumArt");

            albumArt.style.backgroundImage = new StyleBackground(FolderIcon);

            element.userData = node;

            Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

            thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as Action;

            Action action = () => ChangeDisplayedPlaylistDirectory(node);

            thumbnailPlayButton.userData = action;
            thumbnailPlayButton.clicked += action;
        }
        else
        {
            Playlist playlist = PlaylistManager.GetPlaylist(node.Path);

            element.Q<Label>("title").text = playlist.Name;

            VisualElement albumArt = element.Q<VisualElement>("albumArt");

            if (playlist.TryGetFirstSong(out SongInfo song))
            {
                if (!song.MetaDataLoaded)
                {
                    song.GetSongInfo();
                    song.OnMetaDataLoaded += () => playlistList.RefreshItem(index);
                }

                albumArt.style.backgroundImage = song.AlbumCover;
            }

            element.userData = playlist;

            Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

            thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as Action;

            Action action = () => PlayPlaylist(playlist);

            thumbnailPlayButton.userData = action;
            thumbnailPlayButton.clicked += action;
        }
    }

    void BindSongListItem(VisualElement element, int index)
    {
        SongInfo song = ((List<SongInfo>)songList.itemsSource)[index];

        ListItemData data = element.userData as ListItemData;

        if (data == null)
        {
            data = new ListItemData();
            element.userData = data;
        }

        data.Song = song;

        if (!song.MetaDataLoaded)
        {
            song.GetSongInfo();

            data.MetadataCallback = () =>
            {
                songList.RefreshItem(index);
            };

            song.OnMetaDataLoaded += data.MetadataCallback;
        }

        element.Q<Label>("title").text = song.Title;
        element.Q<Label>("artist").text = song.Artist;
        element.Q<Label>("duration").text =
            song.SearchMetaDataLoaded ? song.Duration.ToString() : "--:--";

        VisualElement albumArt = element.Q<VisualElement>("albumArt");
        albumArt.style.backgroundImage = song.AlbumCover;

        Button thumbnailPlayButton =
            element.Q<Button>("thumbnailPlayButton");

        thumbnailPlayButton.clicked -=
            thumbnailPlayButton.userData as Action;

        Action action = () => PlaySong(song);

        thumbnailPlayButton.userData = action;
        thumbnailPlayButton.clicked += action;
    }

    void UnbindSongListItem(VisualElement element, int index)
    {
        ListItemData data = element.userData as ListItemData;

        if (data == null)
            return;

        if (data.Song != null && data.MetadataCallback != null)
        {
            data.Song.OnMetaDataLoaded -= data.MetadataCallback;
            data.MetadataCallback = null;
        }

        if (data.Song != null && data.Song != MusicPlayer.CurrentSong())
        {
            data.Song.DisposeAlbumCover();
        }

        data.Song = null;
    }

    void OnSongItemRightClick(PointerDownEvent evt)
    {
        if (evt.button != 1)
            return;

        ListItemData songData = (ListItemData)((VisualElement)evt.currentTarget).userData;
        SongInfo song = songData.Song;

        contextMenu.AddItem("Play", () => PlaySong(song));

        contextMenu.AddItem("Queue Next", () =>
        {
            MusicPlayer.AddNext(song);
            RefreshSongQueue();
        });

        contextMenu.AddItem("Add To Playlist", () =>
        {
            Debug.Log("Open playlist picker");
        });

        contextMenu.Show(evt.position);

        evt.StopPropagation();
    }

    void OnQueueItemRightClick(PointerDownEvent evt)
    {
        if (evt.button != 1)
            return;

        SongInfo song = (SongInfo)((VisualElement)evt.currentTarget).userData;

        contextMenu.AddItem("Play", () => PlaySong(song));

        contextMenu.AddItem("Queue Next", () =>
        {
            MusicPlayer.AddNext(song);
            RefreshSongQueue();
        });

        contextMenu.AddItem("Add To Playlist", () =>
        {
            Debug.Log("Open playlist picker");
        });

        contextMenu.AddItem("Remove", () =>
        {
            MusicPlayer.musicQueue.Remove(song);
            RefreshSongQueue();
        });

        contextMenu.Show(evt.position);

        evt.StopPropagation();
    }

    void OnPlaylistItemRightClick(PointerDownEvent evt)
    {
        if (evt.button != 1)
            return;

        var userData = ((VisualElement)evt.currentTarget).userData;

        switch (userData)
        {
            case Playlist playlist:

                contextMenu.AddItem("Play", () => PlayPlaylist(playlist));

                contextMenu.AddItem("Queue Next", () =>
                {
                    MusicPlayer.AddPlaylistNext(playlist);
                    RefreshSongQueue();
                });

                contextMenu.AddItem("Add To Playlist", () =>
                {
                    Debug.Log("Open playlist picker");
                });

                break;

            case FileNode fileNode:
                contextMenu.AddItem("Open", () => ChangeDisplayedPlaylistDirectory(fileNode));

                // add delete option later? make sure to add a confirmation message first
                break;
        }

        contextMenu.Show(evt.position);

        evt.StopPropagation();
    }

    void OnPlaybackDrag(MouseCaptureEvent e)
    {
        playbackSliderDragged = true;
    }

    void OnPlaybackRelease(MouseCaptureOutEvent e)
    {
        MusicPlayer.SetSongTime(playbackSlider.value);
        playbackSliderDragged = false;
    }

    void PlaySong(SongInfo song)
    {
        Debug.Log($"Playing: {song.Title}");

        MusicPlayer.PlayNow(song);

        RefreshSongQueue();
    }

    void PlaySongFromQueue(int index)
    {
        Debug.Log($"Playing: {MusicPlayer.musicQueue[index].Title}");

        MusicPlayer.PlayIndex(index);

        RefreshSongQueue();
    }

    void PlayPlaylist(Playlist playlist)
    {
        MusicPlayer.PlayPlaylist(playlist);

        RefreshPlaylistList();
    }

    void PlayNext()
    {
        MusicPlayer.PlayNext();
    }

    void PlayPrevious()
    {
        MusicPlayer.PlayPrevious();
    }

    void ShuffleAll()
    {
        MusicPlayer.PlayAll(true);
        queue = MusicPlayer.musicQueue;
        songQueue.itemsSource = queue;
        songQueue.Rebuild();
    }

    void ChangeDisplayedPlaylistDirectory(FileNode directory)
    {
        if (directory == null)
            return;

        currentPlaylistDirectory = directory;
        playlistList.itemsSource = currentPlaylistDirectory.Children;
        playlistList.Rebuild();
    }

    void ResetDisplayedPlaylistDirectory()
    {
        currentPlaylistDirectory = playlistDirectory;
        playlistList.itemsSource = currentPlaylistDirectory.Children;
        playlistList.Rebuild();
    }

    bool popOutDisplayed = true;

    void TogglePopOutView()
    {
        if (popOutDisplayed)
        {
            popOutButton.IconImage = new StyleBackground(PopOutViewInIcon);
            popOutView.style.display = DisplayStyle.None;
            popOutDisplayed = false;
        }
        else
        {
            popOutButton.IconImage = new StyleBackground(PopOutViewOutIcon);
            popOutView.style.display = DisplayStyle.Flex;
            popOutDisplayed = true;
        }
    }

    [ButtonMethod]
    void RefreshSongQueue()
    {
        songQueue.RefreshItems();
    }

    // Call this if you change the song list at runtime
    [ButtonMethod]
    void RefreshSongList()
    {
        songList.RefreshItems();
    }

    [ButtonMethod]
    void RefreshPlaylistList()
    {
        playlistList.RefreshItems();
    }

    public static SaveColor accent = new SaveColor("accentColour", MusicPlayer.saveData, SystemTheme.GetAccentColour());
    public static Color accentDark;
    public static Color accentLight;

    public static Action OnAccentChange;

    public static void ApplyAccent()
    {
        accentDark = Color.Lerp(accent, Color.black, 0.3f);
        accentLight = Color.Lerp(accent, Color.white, 0.3f);

        foreach (VisualElement element in root.Query(className: "accentImageTint").ToList())
        {
            if (!element.ClassListContains("ButtonHover"))
                element.style.unityBackgroundImageTintColor = accentLight;
        }

        foreach (VisualElement element in root.Query(className: "accentBackgroundColor").ToList())
        {
            element.style.backgroundColor = accent.Get();
        }

        foreach (VisualElement element in root.Query(className: "accentColor").ToList())
        {
            element.style.color = accent.Get();
        }

        foreach (VisualElement element in root.Query(className: "unity-base-slider__dragger").ToList())
        {
            element.style.backgroundColor = accentLight;
        }

        foreach (VisualElement element in root.Query(className: "unity-base-slider__tracker").ToList())
        {
            element.style.backgroundColor = accentDark;
        }

        foreach (AccentButton button in root.Query<AccentButton>().ToList())
        {
            button.color = accentLight;
        }

        OnAccentChange?.Invoke();
    }
}