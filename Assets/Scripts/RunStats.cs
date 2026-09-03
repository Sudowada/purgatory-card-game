using UnityEngine;
using System;

/// <summary>
/// Singleton that accumulates run statistics.
/// Lives on GameManager's GameObject. Reset on new run.
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    // ── Tracked stats ─────────────────────────────────────────────────────────
    public float RunStartTime   { get; private set; }
    public float RunDuration    => Time.time - RunStartTime;

    public int   TotalScore     { get; private set; }
    public int   RoundsSurvived { get; private set; }
    public int   HandsPlayed    { get; private set; }
    public int   CardsPlayed    { get; private set; }
    public int   HighestHand    { get; private set; }
    public float BestMultiplier { get; private set; }
    public string BestComboName { get; private set; } = "None";
    public int   DecayedCardsPlayed { get; private set; }

    void Awake()
    {
        if (Instance == null) { Instance = this; StartRun(); }
        else Destroy(this);
    }

    public void StartRun()
    {
        RunStartTime        = Time.time;
        TotalScore          = 0;
        RoundsSurvived      = 0;
        HandsPlayed         = 0;
        CardsPlayed         = 0;
        HighestHand         = 0;
        BestMultiplier      = 0f;
        BestComboName       = "None";
        DecayedCardsPlayed  = 0;
    }

    public void RecordHand(ComboResult result, int cardCount, int decayedCount)
    {
        HandsPlayed++;
        CardsPlayed        += cardCount;
        DecayedCardsPlayed += decayedCount;
        TotalScore         += result.finalScore;

        if (result.finalScore > HighestHand)
            HighestHand = result.finalScore;

        if (result.multiplier > BestMultiplier)
        {
            BestMultiplier = result.multiplier;
            BestComboName  = result.comboName ?? BestComboName;
        }
    }

    public void RecordRoundCleared() => RoundsSurvived++;

    public string FormattedDuration()
    {
        TimeSpan t = TimeSpan.FromSeconds(RunDuration);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes:D2}m {t.Seconds:D2}s"
            : $"{t.Minutes}m {t.Seconds:D2}s";
    }
}
