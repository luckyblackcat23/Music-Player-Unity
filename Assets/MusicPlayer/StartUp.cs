using MyBox;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_WINMD_SUPPORT
using Windows.UI.ViewManagement;
#endif

public class StartUp : MonoBehaviour
{
    [SerializeField] PanelRenderer document;

    VisualElement root;

    int uiVersion = 0;

    VisualElement background;
    Image icon;
    
    float timeElapsed;

    public static bool debug;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void Initialization()
    {

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        document.RegisterUIReloadCallback(OnUIReload);

        Application.targetFrameRate = 30;

        StartCoroutine(StartUpWait());

        SaveManager.LoadAll();
    }

    private void OnUIReload(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        // The version number only changes when the UI actually reloads, 
        // so this checks prevents duplicated elements.
        if (uiVersion == version)
            return;

        uiVersion = version;

        this.root = root;

        background = root.Q<VisualElement>("Background");
        icon = root.Q<Image>("Icon");

        Color SystemColour = SystemTheme.GetAccentColour();

        background.style.backgroundColor = SystemColour;
        icon.style.unityBackgroundImageTintColor = Color.Lerp(SystemColour, Color.white, 0.8f);
    }
    
    float fade = 1f;

    IEnumerator StartUpWait()
    {
        // Wait for the UI Toolkit document to initialise.
        while (background == null || icon == null)
            yield return null;

        // Wait for songs to finish caching, with a 10 second timeout.
        timeElapsed = 0f;

        while (!MusicPlayer.songsCached && timeElapsed < 10f)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Debug startup delay
        if (debug)
        {
            while (timeElapsed < 10f)
            {
                timeElapsed += Time.deltaTime;
                yield return null;
            }
        }

        WindowsBorderlessWindow.RemoveBorder();

        // Load the main scene.
        SceneManager.LoadScene(1, LoadSceneMode.Additive);

        // Fade out the startup UI.
        fade = 1f;

        while (fade > 0f)
        {
            fade -= Time.deltaTime * 1.1f;

            background.style.opacity = fade;
            icon.style.opacity = fade;

            yield return null;
        }

        // Make sure it reaches exactly zero.
        background.style.opacity = 0f;
        icon.style.opacity = 0f;

        SceneManager.UnloadSceneAsync(0);
    }

    [ButtonMethod]
    void openSave()
    {
        Globals.OpenSavePath();
    }
}