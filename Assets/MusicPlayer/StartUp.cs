using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartUp : MonoBehaviour
{
    public Image background;
    public RawImage icon;

    float timeElapsed;

    public static bool debug;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Color SystemColour = SystemTheme.GetAccentColour();

        background.color = SystemColour;
        icon.color = Color.Lerp(SystemColour, Color.white, 0.8f);

        StartCoroutine(StartUpWait());
    }

    float fade = 1f;

    IEnumerator StartUpWait()
    {
        //gives up after 5 seconds. might wanna add some error screen or something if that triggers
        while (!MusicPlayer.songsCached && timeElapsed < 5f)
        {
            timeElapsed += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        if (debug)
        {
            while (timeElapsed < 5f)
            {
                timeElapsed += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
        }

        SceneManager.LoadScene(1, LoadSceneMode.Additive);

        while (fade > 0)
        {
            background.color = new Color(background.color.r, background.color.g, background.color.b, fade);
            icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, fade);

            fade -= Time.deltaTime * 1.1f;
            yield return new WaitForEndOfFrame();
        }

        SceneManager.UnloadSceneAsync(0);
    }
}