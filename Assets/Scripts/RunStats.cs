using System;

public static class RunStats
{
    public static int Score { get; private set; }
    public static int Total { get; private set; }
    public static int MaxLives { get; private set; }
    public static int LivesLeft { get; private set; }
    public static float DurationSeconds { get; private set; }

    private static DateTime _startTime;

    public static void BeginRun(int maxLives)
    {
        Score = 0;
        Total = 0;
        MaxLives = maxLives;
        LivesLeft = maxLives;
        _startTime = DateTime.UtcNow;
        DurationSeconds = 0f;
    }

    public static void RecordAnswer(bool correct)
    {
        Total++;
        if (correct) Score++;
    }

    public static void UseLife()
    {
        LivesLeft = Math.Max(0, LivesLeft - 1);
    }

    public static void EndRun()
    {
        DurationSeconds = (float)(DateTime.UtcNow - _startTime).TotalSeconds;
    }

    public static void OverrideScore(int s)
    {
        if (s < 0) s = 0;
        Score = s;
    }


    public static float Accuracy() => Total > 0 ? (float)Score / Total : 0f;
}