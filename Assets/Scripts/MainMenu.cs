using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Button References")]
    public Button playButton;
    public Button quitButton;
    public Button level1Button;
    public Button level2Button;
    public Button level3Button;

    [Header("Scene Names")]
    public string level1Scene = "Level1";
    public string level2Scene = "Level2";
    public string level3Scene = "Level3";

    private void Start()
    {
        // Assign listeners only if buttons are assigned
        if (playButton != null)
            playButton.onClick.AddListener(Play);

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        if (level1Button != null)
            level1Button.onClick.AddListener(Level1);

        if (level2Button != null)
            level2Button.onClick.AddListener(Level2);

        if (level3Button != null)
            level3Button.onClick.AddListener(Level3);
    }

    public void Play()
    {
        // Loads next scene in Build Settings order
        SceneManager.LoadScene("Difficulty");
    }

    public void Quit()
    {
        Debug.Log("Player has quit the game.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // so it works in Editor
#endif
    }

    public void Level1()
    {
        SceneManager.LoadScene(level1Scene);
    }

    public void Level2()
    {
        SceneManager.LoadScene(level2Scene);
    }

    public void Level3()
    {
        SceneManager.LoadScene(level3Scene);
    }
}
