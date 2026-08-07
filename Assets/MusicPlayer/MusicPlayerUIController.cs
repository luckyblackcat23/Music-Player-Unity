using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using MyBox;
using Tools;

public class MusicPlayerUIController : MonoBehaviour
{
    public MusicPlayer musicPlayer;

    public bool playbackSliderDragged;

    [Header("UI")]
    [SerializeField] UIDocument document;
    [SerializeField] VisualTreeAsset songItemTemplate;
    [SerializeField] VisualTreeAsset PlaylistItemTemplate;

    ContextMenu contextMenu;

    [Header("Data")]
    List<SongInfo> songs = new();
    List<SongInfo> queue = new();
    string[] playlists;

    List<SongInfo> searchTempSongs = new();

    ListView songQueue;

    ListView playlistList;

    ListView songList;

    TextField searchBar;

    // Playback bar

    VisualElement playbackAlbumArt;
    Label songTitle;

    Button shuffleButton;
    Button previousButton;
    Button playButton;
    Button nextButton;
    Button loopButton;

    Slider volumeSlider;

    Slider playbackSlider;

    [Header("Resources")]
    public Sprite ShuffleIcon;
    public Sprite DontShuffleIcon;

    public Sprite DontLoopIcon;
    public Sprite LoopIcon;
    public Sprite LoopSingleIcon;

    void OnEnable()
    {
        SetupListView();
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
    }

    void SetupListView()
    {
        //assign variables
        VisualElement root = document.rootVisualElement;

        contextMenu = new ContextMenu(root);

        songQueue = root.Q<ListView>("SongQueue");

        playlistList = root.Q<ListView>("PlaylistList");

        songList = root.Q<ListView>("SongList");

        searchBar = root.Q<TextField>("SearchBar");

        playbackAlbumArt = root.Q<VisualElement>("AlbumArt");
        songTitle = root.Q<Label>("SongTitle");

        shuffleButton = root.Q<Button>("Shuffle");
        previousButton = root.Q<Button>("Previous");
        playButton = root.Q<Button>("Play");
        nextButton = root.Q<Button>("Next");
        loopButton = root.Q<Button>("Loop");

        volumeSlider = root.Q<Slider>("VolumeSlider");

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
            return songItemTemplate.Instantiate();
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

        playlists = MusicPlayer.Playlists();

        playlistList.itemsSource = playlists;

        playlistList.makeItem = () =>
        {
            return PlaylistItemTemplate.Instantiate();
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

        songList.selectionType = SelectionType.Single;
        #endregion

        searchBar.RegisterCallback<ChangeEvent<string>>((x) =>
        {
            searchTempSongs.Clear();

            foreach (SongInfo song in songs)
            {
                //search Title
                if (song.Title != null)
                    if (song.Title.ToLower().Contains(x.newValue.ToLower()))
                        searchTempSongs.Add(song);
            }

            songList.itemsSource = searchTempSongs;
            RefreshSongList();
        });

        //initialize button events
        shuffleButton.clicked += () => 
        {
            musicPlayer.shuffle = !musicPlayer.shuffle;

            if (musicPlayer.shuffle)
                shuffleButton.style.backgroundImage = new StyleBackground(ShuffleIcon);
            else
                shuffleButton.style.backgroundImage = new StyleBackground(DontShuffleIcon);
        };
        previousButton.clicked += () => musicPlayer.PlayPrevious();
        playButton.clicked += () => musicPlayer.TogglePause();
        nextButton.clicked += () => musicPlayer.PlayNext();
        loopButton.clicked += () =>
        { 
            musicPlayer.incrementLoop();
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

        volumeSlider.value = musicPlayer.UserVolume; // change to saved value later

        playbackSlider.RegisterCallback<MouseCaptureEvent>(OnPlaybackDrag);
        playbackSlider.RegisterCallback<MouseCaptureOutEvent>(OnPlaybackRelease);
    }

    private void Update()
    {
        if (!musicPlayer.paused && !playbackSliderDragged)
            playbackSlider.value = musicPlayer.playbackTime;

        musicPlayer.UserVolume = volumeSlider.value;
    }

    void BindQueueItem(VisualElement element, int index)
    {
        SongInfo song = queue[index];

        if (!song.MetaDataLoaded)
        {
            song.GetSongInfo();
            song.OnMetaDataLoaded += () => songQueue.RefreshItem(index);
        }

        element.Q<Label>("title").text = song.Title;
        element.Q<Label>("artist").text = song.Artist;
        element.Q<Label>("duration").text =
            song.MetaDataLoaded ? song.Duration.ToString() : "--:--";

        VisualElement albumArt = element.Q<VisualElement>("albumArt");
        albumArt.style.backgroundImage = song.AlbumCover;

        Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

        thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as System.Action;

        System.Action action = () => PlaySongFromQueue(index);

        thumbnailPlayButton.userData = action;
        thumbnailPlayButton.clicked += action;

        if (index == musicPlayer.currentSongIndex)
            element.AddToClassList("CurrentSong");
        else
            element.RemoveFromClassList("CurrentSong");
    }

    void BindPlaylistListItem(VisualElement element, int index)
    {
        Playlist playlist = Playlist.GetFromPath(playlists[index]);

        element.Q<Label>("title").text = playlist.playlistName;

        VisualElement albumArt = element.Q<VisualElement>("albumArt");

        SongInfo[] songs = playlist.GetSongs();

        if (!songs[0].MetaDataLoaded)
        {
            songs[0].GetSongInfo();
            songs[0].OnMetaDataLoaded += () => { RefreshPlaylistList(); };
        }

        albumArt.style.backgroundImage = songs[0].AlbumCover;

        Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

        thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as System.Action;

        System.Action action = () => PlayPlaylist(playlist);

        thumbnailPlayButton.userData = action;
        thumbnailPlayButton.clicked += action;
    }

    void BindSongListItem(VisualElement element, int index)
    {
        SongInfo song = ((List<SongInfo>)songList.itemsSource)[index];

        if (!song.MetaDataLoaded)
        {
            song.GetSongInfo();
            song.OnMetaDataLoaded += () => songList.RefreshItem(index);
        }

        element.Q<Label>("title").text = song.Title;
        element.Q<Label>("artist").text = song.Artist;
        element.Q<Label>("duration").text =
            song.SearchMetaDataLoaded ? song.Duration.ToString() : "--:--";

        element.userData = song;

        VisualElement albumArt = element.Q<VisualElement>("albumArt");
        albumArt.style.backgroundImage = song.AlbumCover;

        Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

        thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as System.Action;

        System.Action action = () => PlaySong(song);

        thumbnailPlayButton.userData = action;
        thumbnailPlayButton.clicked += action;
    }

    void ShowSongContextMenu(Vector2 position, SongInfo song)
    {
        contextMenu.Show(

            position,

            new ContextMenuItem(
                "Play",
                () => PlaySong(song)
            ),

            new ContextMenuItem(
                "Queue Next",
                () =>
                {
                    musicPlayer.AddNext(song);
                    RefreshSongQueue();
                }
            ),

            new ContextMenuItem(
                "Add to Playlist",
                () =>
                {
                    Debug.Log("Open playlist picker");
                }
            ),

            new ContextMenuItem(
                "Remove",
                () =>
                {
                    musicPlayer.musicQueue.Remove(song);
                    RefreshSongQueue();
                }
            )
        );
    }

    void OnSongItemRightClick(PointerDownEvent evt)
    {
        if (evt.button != 1)
            return;

        var song = (SongInfo)((VisualElement)evt.currentTarget).userData;

        ShowSongContextMenu(evt.position, song);

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

    void RefreshPlaylistList()
    {
        playlistList.RefreshItems();
    }
}
