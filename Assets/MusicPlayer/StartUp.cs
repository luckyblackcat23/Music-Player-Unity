using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartUp : MonoBehaviour
{
    public Image background;

    float timeElapsed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        background.color = WindowsTheme.GetAccentColor();

        StartCoroutine(StartUpWait());
    }

    IEnumerator StartUpWait()
    {
        //gives up after 5 seconds. might wanna add some error screen or something if that triggers
        while (!MusicPlayer.songsCached && timeElapsed < 5f)
        {
            timeElapsed += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }

        SceneManager.LoadScene(1);
    }
}