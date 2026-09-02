using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using MyBox;
using Tools;
using System;

public class MusicPlayerUIController : MonoBehaviour
{
    public MusicPlayer musicPlayer;

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

    ListView songQueue;

    ListView playlistList;
    Button playlistListBackButton;

    ListView songList;

    TextField searchBar;
    TextField searchBarQueue;
    TextField searchBarPlaylists;

    // Playback bar

    VisualElement playbackAlbumArt;
    Label songTitle;
    Label songArtist;

    Button settingsButton;

    Button shuffleButton;
    Button previousButton;
    Button playButton;
    Button nextButton;
    Button loopButton;

    Slider volumeSlider;

    Slider playbackSlider;
    Label playbackTime;

    [Header("Resources")]
    public Sprite ShuffleIcon;
    public Sprite DontShuffleIcon;

    public Sprite DontLoopIcon;
    public Sprite LoopIcon;
    public Sprite LoopSingleIcon;

    public Sprite FolderIcon;

    void OnEnable()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(SetupListView);
    }

    void OnDisable()
    {
        accent.onSet -= ApplyAccentToClasses;

        if (songQueue != null)
            songQueue.itemsSource = null;

        if (playlistList != null)
            playlistList.itemsSource = null;

        if (songList != null)
            songList.itemsSource = null;

        playbackSlider.UnregisterCallback<MouseCaptureEvent>(OnPlaybackDrag);
        playbackSlider.UnregisterCallback<MouseCaptureOutEvent>(OnPlaybackRelease);

        musicPlayer.OnSongShuffle.RemoveListener(RefreshSongQueue);
        musicPlayer.OnSongChange.RemoveListener(RefreshSongQueue);

        GetComponent<PanelRenderer>().UnregisterUIReloadCallback(SetupListView);
    }

    private int lastDisplayedSecond = -1;

    private void Update()
    {
        if (!musicPlayer.paused && !playbackSliderDragged)
            playbackSlider.value = musicPlayer.playbackTime;

        int currentSecond = Mathf.FloorToInt(musicPlayer.playbackTime);

        if (currentSecond != lastDisplayedSecond && playbackTime != null)
        {
            lastDisplayedSecond = currentSecond;

            TimeSpan time = TimeSpan.FromSeconds(currentSecond);

            playbackTime.text = time.ToString(@"mm\:ss");
        }
    }

    int uiVersion = 0;
    void SetupListView(PanelRenderer panelRenderer, VisualElement root_, int version)
    {
        // The version number only changes when the UI actually reloads, 
        // so this checks prevents duplicated elements.
        if (uiVersion == version)
            return;

        uiVersion = version;

        root = root_;

        accent.onSet += ApplyAccentToClasses;

        ApplyAccentToClasses();

        contextMenu = new ContextMenu(root);

        songQueue = root.Q<ListView>("SongQueue");

        playlistList = root.Q<ListView>("PlaylistList");
        playlistListBackButton = root.Q<Button>("PlaylistListBack");

        songList = root.Q<ListView>("SongList");

        searchBarQueue = root.Q<TextField>("SearchBarQueue");
        searchBarPlaylists = root.Q<TextField>("SearchBarPlaylists");
        searchBar = root.Q<TextField>("SearchBar");

        playbackAlbumArt = root.Q<VisualElement>("AlbumArt");
        songTitle = root.Q<Label>("SongTitle");
        songArtist = root.Q<Label>("SongArtist");

        settingsButton = root.Q<Button>("SettingsButton");

        shuffleButton = root.Q<Button>("Shuffle");
        previousButton = root.Q<Button>("Previous");
        playButton = root.Q<Button>("Play");
        nextButton = root.Q<Button>("Next");
        loopButton = root.Q<Button>("Loop");

        volumeSlider = root.Q<Slider>("VolumeSlider");

        playbackTime = root.Q<Label>("PlaybackTime");
        playbackSlider = root.Q<Slider>("PlaybackSlider");

        //initialize song change event
        musicPlayer.OnSongChange.AddListener(() =>
        {
            SongInfo currentSong = musicPlayer.CurrentSong();

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
                    if (musicPlayer.CurrentSong() != currentSong)
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

        queue = musicPlayer.musicQueue;

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
            if (musicPlayer.currentSongIndex == oldIndex)
            {
                musicPlayer.currentSongIndex = newIndex;
            }
            else if (oldIndex < musicPlayer.currentSongIndex && newIndex >= musicPlayer.currentSongIndex)
            {
                // An item before the current song moved after it.
                // Current song shifts left.
                musicPlayer.currentSongIndex--;
            }
            else if (oldIndex > musicPlayer.currentSongIndex && newIndex <= musicPlayer.currentSongIndex)
            {
                // An item after the current song moved before it.
                // Current song shifts right.
                musicPlayer.currentSongIndex++;
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

        settingsButton.clicked += MusicPlayerOptionsMenu.ShowOptionsMenu;

        shuffleButton.clicked += () =>
        {
            musicPlayer.ShuffleQueue();
            RefreshSongQueue();
        };
        previousButton.clicked += musicPlayer.PlayPrevious;
        playButton.clicked += musicPlayer.TogglePause;
        nextButton.clicked += musicPlayer.PlayNext;
        loopButton.clicked += () =>
        {
            musicPlayer.IncrementLoop();
            switch (musicPlayer.Loop)
            {
                case MusicPlayer.LoopOptions.dontLoop:
                    loopButton.style.backgroundImage = new StyleBackground(DontLoopIcon);
                    break;
                case MusicPlayer.LoopOptions.loop:
                    loopButton.style.backgroundImage = new StyleBackground(LoopIcon);
                    break;
                case MusicPlayer.LoopOptions.loopSingle:
                    loopButton.style.backgroundImage = new StyleBackground(LoopSingleIcon);
                    break;
            }
        };

        volumeSlider.RegisterValueChangedCallback(evt => musicPlayer.UserVolume = evt.newValue);

        musicPlayer.OnSongShuffle.AddListener(RefreshSongQueue);
        musicPlayer.OnSongChange.AddListener(RefreshSongQueue);

        volumeSlider.value = musicPlayer.UserVolume; // change to saved value later

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

            if (index == musicPlayer.currentSongIndex)
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
            Playlist playlist = Playlist.GetFromPath(node.Path);

            element.Q<Label>("title").text = playlist.playlistName;

            VisualElement albumArt = element.Q<VisualElement>("albumArt");

            SongInfo[] songs = playlist.GetSongs();

            if (songs.Length > 0)
            {
                if (!songs[0].MetaDataLoaded)
                {
                    songs[0].GetSongInfo();
                    songs[0].OnMetaDataLoaded += () => RefreshPlaylistList();
                }

                albumArt.style.backgroundImage = songs[0].AlbumCover;
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

        if (data.Song != null && data.Song != musicPlayer.CurrentSong())
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
            musicPlayer.AddNext(song);
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
            musicPlayer.AddNext(song);
            RefreshSongQueue();
        });

        contextMenu.AddItem("Add To Playlist", () =>
        {
            Debug.Log("Open playlist picker");
        });

        contextMenu.AddItem("Remove", () =>
        {
            musicPlayer.musicQueue.Remove(song);
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
                    musicPlayer.AddPlaylistNext(playlist);
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
        musicPlayer.SetSongTime(playbackSlider.value);
        playbackSliderDragged = false;
    }

    void PlaySong(SongInfo song)
    {
        Debug.Log($"Playing: {song.Title}");

        musicPlayer.PlayNow(song);

        RefreshSongQueue();
    }

    void PlaySongFromQueue(int index)
    {
        Debug.Log($"Playing: {musicPlayer.musicQueue[index].Title}");

        musicPlayer.PlayIndex(index);

        RefreshSongQueue();
    }

    void PlayPlaylist(Playlist playlist)
    {
        musicPlayer.PlayPlaylist(playlist);

        RefreshPlaylistList();
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

    public static SaveColor accent = new("accentColour", defaultValue: SystemTheme.GetAccentColour());

    static void ApplyAccentToClasses()
    {
        Color accentDark = Color.Lerp(accent, Color.black, 0.3f);
        Color accentLight = Color.Lerp(accent, Color.white, 0.3f);

        foreach (VisualElement element in root.Query(className: "accentImageTint").ToList())
        {
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
    }
}