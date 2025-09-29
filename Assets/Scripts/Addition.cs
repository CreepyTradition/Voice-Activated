using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Windows.Speech; // Windows-only speech API

public class VoiceMathGame : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI QuestionText;
    public TextMeshProUGUI StatusText;
    public TextMeshProUGUI ResultText;
    public TextMeshProUGUI ScoreText;
    public Button StartButton;
    public Button ListenButton;

    [Header("Game Settings")]
    [Tooltip("Include multiplication?")]
    public bool includeMultiply = false;
    [Tooltip("Operands are chosen from 1..maxOperand (inclusive).")]
    public int maxOperand = 10;

    private DictationRecognizer recognizer;

    private int a, b, answer;
    private char op;
    private int score = 0, total = 0;
    private System.Random rng = new System.Random();

    // Simple word map for 0..20 and common homophones
    private static readonly Dictionary<string, int> WORDS = new Dictionary<string, int>()
    {
        {"zero",0},{"one",1},{"two",2},{"three",3},{"four",4},{"for",4},{"five",5},
        {"six",6},{"seven",7},{"eight",8},{"ate",8},{"nine",9},{"ten",10},
        {"eleven",11},{"twelve",12},{"thirteen",13},{"fourteen",14},{"fifteen",15},
        {"sixteen",16},{"seventeen",17},{"eighteen",18},{"nineteen",19},{"twenty",20}
    };

    void Awake()
    {
        // Wire up buttons
        StartButton.onClick.AddListener(NewProblem);
        ListenButton.onClick.AddListener(StartListening);
        ListenButton.interactable = false; // enabled after first problem

        // Prepare recognizer
        if (!Application.isEditor && Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
        {
            StatusText.text = "Windows speech API required.";
            ListenButton.interactable = false;
            return;
        }

        recognizer = new DictationRecognizer(ConfidenceLevel.Low, DictationTopicConstraint.Dictation);
        recognizer.AutoSilenceTimeoutSeconds = 2.0f; // end after brief silence
        recognizer.InitialSilenceTimeoutSeconds = 6.0f;

        recognizer.DictationHypothesis += (text) =>
        {
            StatusText.text = $"Heard (…): {text}";
        };

        recognizer.DictationResult += OnDictationResult;
        recognizer.DictationComplete += OnDictationComplete;
        recognizer.DictationError += (error, hresult) =>
        {
            StatusText.text = $"Mic error: {error}";
        };

        UpdateScoreUI();
        QuestionText.text = "Press Start / Next";
        ResultText.text = "";
        StatusText.text = "Ready.";
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            if (recognizer.Status == SpeechSystemStatus.Running) recognizer.Stop();
            recognizer.Dispose();
        }
    }

    public void NewProblem()
    {
        // Choose operator
        char[] ops = includeMultiply ? new[] { '+', '-', '×' } : new[] { '+', '-' };
        op = ops[rng.Next(ops.Length)];

        a = rng.Next(1, Mathf.Max(2, maxOperand + 1));
        b = rng.Next(1, Mathf.Max(2, maxOperand + 1));

        if (op == '-') // keep non-negative
        {
            if (b > a) (a, b) = (b, a);
            answer = a - b;
        }
        else if (op == '×')
        {
            a = rng.Next(1, Mathf.Max(2, Mathf.Min(10, maxOperand) + 1));
            b = rng.Next(1, Mathf.Max(2, Mathf.Min(10, maxOperand) + 1));
            answer = a * b;
        }
        else // '+'
        {
            answer = a + b;
        }

        QuestionText.text = $"{a} {op} {b} = ?";
        ResultText.text = "";
        StatusText.text = "Tap 🎤 to answer by voice.";
        ListenButton.interactable = true;
    }

    public void StartListening()
    {
        if (recognizer == null) return;

        // Only one recognizer instance can run at a time
        try
        {
            if (recognizer.Status == SpeechSystemStatus.Running)
                recognizer.Stop();

            recognizer.Start();
            StatusText.text = "Listening… say the answer.";
        }
        catch (Exception e)
        {
            StatusText.text = $"Failed to start listening: {e.Message}";
        }
    }

    private void OnDictationResult(string text, ConfidenceLevel conf)
    {
        StatusText.text = $"Heard: \"{text}\"";
        var parsed = ParseNumber(text);
        if (parsed == null)
        {
            ResultText.color = new Color(0.70f, 0.45f, 0.0f);
            ResultText.text = "Didn't catch a number. Try again.";
            return;
        }
        Check(parsed.Value);
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        switch (cause)
        {
            case DictationCompletionCause.Complete:
                StatusText.text = "Stopped listening.";
                break;
            case DictationCompletionCause.TimeoutExceeded:
                StatusText.text = "Listening timed out. Tap 🎤 and try again.";
                break;
            case DictationCompletionCause.Canceled:
                StatusText.text = "Listening canceled.";
                break;
            default:
                StatusText.text = $"Stopped: {cause}";
                break;
        }
    }


    private void Check(int spoken)
    {
        total++;
        if (spoken == answer)
        {
            score++;
            ResultText.color = Color.green;
            ResultText.text = $"✅ Correct: {answer}";
        }
        else
        {
            ResultText.color = new Color(0.8f, 0.0f, 0.1f);
            ResultText.text = $"❌ You said {spoken}. Correct is {answer}.";
        }
        UpdateScoreUI();
        // Small quality-of-life: auto-advance after a short delay
        Invoke(nameof(NewProblem), 0.9f);
    }

    private void UpdateScoreUI()
    {
        ScoreText.text = $"Score: {score}/{total}";
    }

    // --- Speech parsing helpers ---

    private int? ParseNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string text = raw.ToLower().Trim();

        // 1) Try direct digits (handles "5", "I think it's 5", etc.)
        var m = Regex.Match(text, @"-?\d+");
        if (m.Success && int.TryParse(m.Value, out int d))
            return d;

        // 2) Try single word tokens present in WORDS
        foreach (var tok in Regex.Split(text, @"\s+|[-]"))
        {
            if (WORDS.TryGetValue(tok, out int val))
                return val;
        }

        // 3) Simple two-word combos like "twenty one" -> 21 (optional small range)
        int combined = TryCombineTwoWordsToNumber(text);
        if (combined != int.MinValue) return combined;

        return null;
    }

    private int TryCombineTwoWordsToNumber(string text)
    {
        // very small coverage for 21..29 as an example; expand if you raise difficulty
        string[] tokens = Regex.Split(text, @"\s+|[-]");
        if (tokens.Length < 2) return int.MinValue;

        int first = WORDS.ContainsKey(tokens[0]) ? WORDS[tokens[0]] : int.MinValue;
        int second = WORDS.ContainsKey(tokens[1]) ? WORDS[tokens[1]] : int.MinValue;

        if (first == 20 && second >= 1 && second <= 9)
            return 20 + second;

        return int.MinValue;
    }
}
