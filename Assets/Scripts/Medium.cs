using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Windows.Speech; // Windows-only speech API

public class Medium : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI QuestionText;
    public TextMeshProUGUI StatusText;
    public TextMeshProUGUI ResultText;
    public TextMeshProUGUI ScoreText;  // shows "Score x/y (y of maxQuestions)"
    public TextMeshProUGUI LivesText;
    public Button StartButton;

    [Header("Game Settings")]
    public bool includeMultiply = false;
    public int maxOperand = 10;

    [Header("Lives / Health")]
    public int maxLives = 5;
    private int lives;

    [Header("Run Limits")]
    [Tooltip("Maximum number of questions in a run")]
    public int maxQuestions = 20;

    [Header("Scenes")]
    [Tooltip("Name of the GameOver scene in Build Settings")]
    public string gameOverSceneName = "GameOver";

    private DictationRecognizer recognizer;
    private int a, b, answer;
    private char op;
    private int score = 0, total = 0; // questions attempted so far
    private int mistakeCount = 0;      // counts mistakes to trigger every-other penalty

    private System.Random rng = new System.Random();
    private bool awaitingAnswer = false;
    private float listenTimer = 0f;
    private float listenDuration = 6f; // 6 seconds max per question

    private static readonly Dictionary<string, int> WORDS = new Dictionary<string, int>()
    {
        {"zero",0},{"one",1},{"two",2},{"three",3},{"four",4},{"for",4},{"five",5},
        {"six",6},{"seven",7},{"eight",8},{"ate",8},{"nine",9},{"ten",10},
        {"eleven",11},{"twelve",12},{"thirteen",13},{"fourteen",14},{"fifteen",15},
        {"sixteen",16},{"seventeen",17},{"eighteen",18},{"nineteen",19},{"twenty",20}
    };

    void Awake()
    {
        StartButton.onClick.AddListener(NewProblem);

        if (Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
        {
            StatusText.text = "Windows Speech API required.";
            return;
        }

        recognizer = new DictationRecognizer(ConfidenceLevel.Low, DictationTopicConstraint.Dictation);
        recognizer.AutoSilenceTimeoutSeconds = 2.0f;
        recognizer.InitialSilenceTimeoutSeconds = 6.0f;
        recognizer.DictationHypothesis += (t) => StatusText.text = $"Heard (…): {t}";
        recognizer.DictationResult += OnDictationResult;
        recognizer.DictationComplete += OnDictationComplete;
        recognizer.DictationError += (err, h) => StatusText.text = $"Mic error: {err}";

        ResetGame();
    }

    void Update()
    {
        if (awaitingAnswer)
        {
            listenTimer += Time.deltaTime;
            if (listenTimer >= listenDuration)
            {
                awaitingAnswer = false;
                StatusText.text = "⏰ Time's up!";
                CountAsMiss();
            }
        }
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            if (recognizer.Status == SpeechSystemStatus.Running) recognizer.Stop();
            recognizer.Dispose();
        }
    }

    public void ResetGame()
    {
        score = 0;
        total = 0;
        lives = maxLives;
        mistakeCount = 0;

        RunStats.BeginRun(maxLives);

        UpdateScoreUI();
        UpdateLivesUI();
        QuestionText.text = "Press Start";
        ResultText.text = "";
        StatusText.text = "Ready.";
        StartButton.interactable = true;
    }

    private void GoToGameOver()
    {
        // Sync the final score into RunStats so GameOver shows the adjusted value
        // (Add RunStats.OverrideScore(int) as shown below)
        RunStats.OverrideScore(score);

        RunStats.EndRun();
        if (recognizer != null && recognizer.Status == SpeechSystemStatus.Running)
            recognizer.Stop();

        SceneManager.LoadScene(gameOverSceneName);
    }

    public void NewProblem()
    {
        if (total >= maxQuestions || lives <= 0)
        {
            GoToGameOver();
            return;
        }

        char[] ops = includeMultiply ? new[] { '+', '-', '×' } : new[] { '+', '-' };
        op = ops[rng.Next(ops.Length)];
        a = rng.Next(1, maxOperand + 1);
        b = rng.Next(1, maxOperand + 1);
        if (op == '-' && b > a) (a, b) = (b, a);

        answer = (op == '+') ? a + b : (op == '-') ? a - b : a * b;

        QuestionText.text = $"{a} {op} {b} = ?";
        ResultText.text = "";
        StatusText.text = "Listening for your answer...";
        listenTimer = 0f;
        awaitingAnswer = true;

        StartListening();
    }

    private void StartListening()
    {
        if (recognizer == null) return;

        try
        {
            if (recognizer.Status == SpeechSystemStatus.Running)
                recognizer.Stop();

            recognizer.Start();
        }
        catch (Exception e)
        {
            StatusText.text = $"Failed to start listening: {e.Message}";
        }
    }

    private void StopListening()
    {
        if (recognizer != null && recognizer.Status == SpeechSystemStatus.Running)
            recognizer.Stop();
    }

    private void OnDictationResult(string text, ConfidenceLevel conf)
    {
        if (!awaitingAnswer) return;

        awaitingAnswer = false;
        StopListening();
        StatusText.text = $"Heard: \"{text}\"";
        var parsed = ParseNumber(text);

        if (parsed == null)
        {
            CountAsMiss();
            return;
        }

        Check(parsed.Value);
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        if (cause == DictationCompletionCause.TimeoutExceeded && awaitingAnswer)
        {
            awaitingAnswer = false;
            CountAsMiss();
        }
    }

    // --- Mistake handling with "every other" penalty ---

    private void ApplyMistakePenaltyIfNeeded()
    {
        mistakeCount++;

        // Every 2nd, 4th, 6th... mistake deducts 1 point (but not below 0)
        if (mistakeCount % 2 == 0)
        {
            int oldScore = score;
            score = Mathf.Max(0, score - 1);

            // Optional: brief visual cue
            if (score < oldScore)
            {
                // Append a small note to the last result line
                ResultText.text += "  (-1 point penalty)";
            }
        }
    }

    private void CountAsMiss()
    {
        RunStats.RecordAnswer(false); // counts toward Total in summary
        total++;

        // Life loss
        lives = Mathf.Max(0, lives - 1);
        RunStats.UseLife();

        // Penalty logic (counts this miss toward the every-other rule)
        ApplyMistakePenaltyIfNeeded();

        UpdateScoreUI();
        UpdateLivesUI();

        ResultText.color = Color.red;
        ResultText.text = $"❌ No valid answer! ({lives}/{maxLives} lives left)";

        if (lives <= 0 || total >= maxQuestions)
        {
            GoToGameOver();
        }
        else
        {
            Invoke(nameof(NewProblem), 1.0f);
        }
    }

    private void Check(int spoken)
    {
        bool correct = (spoken == answer);
        RunStats.RecordAnswer(correct);
        total++;

        if (correct)
        {
            score++;
            ResultText.color = Color.green;
            ResultText.text = $"✅ Correct: {answer}";
        }
        else
        {
            // Wrong answer costs a life and may apply the "every other" -1 penalty
            lives = Mathf.Max(0, lives - 1);
            RunStats.UseLife();

            ApplyMistakePenaltyIfNeeded();

            ResultText.color = Color.red;
            ResultText.text = $"❌ You said {spoken}. Correct is {answer}. ({lives}/{maxLives})";
        }

        UpdateScoreUI();
        UpdateLivesUI();

        if (lives <= 0 || total >= maxQuestions)
        {
            GoToGameOver();
        }
        else
        {
            Invoke(nameof(NewProblem), 1.0f);
        }
    }

    private void UpdateScoreUI()
    {
        // Example: "Score: 7/12 (12 of 20)"
        ScoreText.text = $"Score: {score}/{total}  ({Mathf.Min(total, maxQuestions)} of {maxQuestions})";
    }

    private void UpdateLivesUI()
    {
        string hearts = "";
        for (int i = 0; i < maxLives; i++)
            hearts += i < lives ? "1" : "0";
        LivesText.text = $"Lives: {hearts}";
    }

    private int? ParseNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string text = raw.ToLower().Trim();

        // digits inside the phrase
        var m = Regex.Match(text, @"-?\d+");
        if (m.Success && int.TryParse(m.Value, out int d))
            return d;

        // words
        foreach (var tok in Regex.Split(text, @"\s+|[-]"))
            if (WORDS.TryGetValue(tok, out int val))
                return val;

        return null;
    }
}
