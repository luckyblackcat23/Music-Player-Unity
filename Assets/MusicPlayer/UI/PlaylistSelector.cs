using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PlaylistSelector
{
    VisualTreeAsset PlaylistSelectorTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MusicPlayer/UI/PlaylistSelector.uxml");
    VisualTreeAsset PlaylistItemTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/MusicPlayer/UI/PlaylistItem.uxml");

    public Sprite FolderIcon;

    TextField searchBarPlaylistSelector;

    ListView playlistSelectorList;
    
    FileNode playlistDirectory;
    FileNode currentPlaylistDirectory;

    List<FileNode> searchTempSongsPlaylists = new();

    VisualElement menu;

    Playlist selection;

    //this is not correct
    public PlaylistSelector(VisualElement root)
    {
        PlaylistSelectorTemplate.CloneTree(root);
        menu = root.Q<VisualElement>("PlaylistSelector");

        root.Add(menu);

        // Prevent clicking the menu from closing itself.
        menu.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
        });

        // Clicking anywhere else closes it.
        root.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!menu.ElementAt(0).worldBound.Contains(e.position))
            {
                Hide();
            }
        });

        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
                Hide();
        });

        searchBarPlaylistSelector = menu.Q<TextField>("SearchBarPlaylistSelector");
        playlistSelectorList = menu.Q<ListView>("PlaylistSelectorList");

        //maybe implement a way to differentiate between checking every folder and checking just the active one
        searchBarPlaylistSelector.RegisterCallback<ChangeEvent<string>>((x) =>
        {
            searchTempSongsPlaylists.Clear();

            foreach (FileNode playlist in playlistDirectory.GetAllChildren())
            {
                if (!playlist.IsDirectory)
                    if (playlist.Name.Contains(x.newValue, StringComparison.OrdinalIgnoreCase))
                        searchTempSongsPlaylists.Add(playlist);
            }

            if (string.IsNullOrEmpty(x.newValue))
                playlistSelectorList.itemsSource = playlistDirectory.Children;
            else
                playlistSelectorList.itemsSource = searchTempSongsPlaylists;

            playlistSelectorList.RefreshItems();
        });

        if (playlistSelectorList == null)
        {
            Debug.LogError("ListView 'PlaylistList' not found.");
            return;
        }

        playlistSelectorList.fixedItemHeight = 100;

        playlistDirectory = MusicPlayer.PlaylistDirectoryNode;
        currentPlaylistDirectory = playlistDirectory;

        playlistSelectorList.itemsSource = playlistDirectory.Children;

        playlistSelectorList.makeItem = PlaylistItemTemplate.Instantiate;

        playlistSelectorList.bindItem = BindPlaylistListItem;

        playlistSelectorList.selectionType = SelectionType.Single;

        playlistSelectorList.Rebuild();
    }

    public void Show()
    {
        menu.BringToFront();
        menu.style.display = DisplayStyle.Flex;
        selection = null;
    }

    public void Hide()
    {
        menu.style.display = DisplayStyle.None;
    }

    public async Awaitable<Playlist> ShowAndWaitForSelect()
    {
        Show();

        while (selection == null && menu.style.display == DisplayStyle.Flex)
            await Awaitable.NextFrameAsync();

        return selection;
    }

    void BindPlaylistListItem(VisualElement element, int index)
    {
        FileNode node = ((List<FileNode>)playlistSelectorList.itemsSource)[index];

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
                    song.OnMetaDataLoaded += () => playlistSelectorList.RefreshItem(index);
                }

                albumArt.style.backgroundImage = song.AlbumCover;
            }

            element.userData = playlist;

            Button thumbnailPlayButton = element.Q<Button>("thumbnailPlayButton");

            thumbnailPlayButton.clicked -= thumbnailPlayButton.userData as Action;

            Action action = () =>
            {
                selection = playlist;
                Hide();
            };

            thumbnailPlayButton.userData = action;
            thumbnailPlayButton.clicked += action;
        }
    }

    void ChangeDisplayedPlaylistDirectory(FileNode directory)
    {
        if (directory == null)
            return;

        currentPlaylistDirectory = directory;
        playlistSelectorList.itemsSource = currentPlaylistDirectory.Children;
        playlistSelectorList.Rebuild();
    }
}