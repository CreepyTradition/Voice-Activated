// GameOverUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("Summary Texts")]
    public TextMeshProUGUI TitleText;     // e.g., "Game Over"
    public TextMeshProUGUI ScoreText;     // e.g., "Score: 12 / 18"
    public TextMeshProUGUI AccuracyText;  // e.g., "Accuracy: 67%"
    public TextMeshProUGUI LivesText;     // e.g., "Lives left: 0 / 5"
    public TextMeshProUGUI TimeText;      // e.g., "Time: 01:12"

    [Header("Buttons")]
    public Button PlayAgainButton;
    public Button QuitButton;

    [Header("Scenes")]
    [Tooltip("Main gameplay scene name to reload")]
    public string mainSceneName = "MainMenu"; // change to your gameplay scene

    void Start()
    {
        TitleText.text = "Game Over";

        ScoreText.text = $"Score: {RunStats.Score} / {RunStats.Total}";
        AccuracyText.text = $"Accuracy: {(RunStats.Accuracy() * 100f):0}%";
        LivesText.text = $"Lives left: {RunStats.LivesLeft} / {RunStats.MaxLives}";
        TimeText.text = "Time: " + FormatTime(RunStats.DurationSeconds);

        PlayAgainButton.onClick.AddListener(() => SceneManager.LoadScene(mainSceneName));
        QuitButton.onClick.AddListener(Application.Quit);
    }

    private string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int mins = s / 60;
        int secs = s % 60;
        return $"{mins:00}:{secs:00}";
    }
}
