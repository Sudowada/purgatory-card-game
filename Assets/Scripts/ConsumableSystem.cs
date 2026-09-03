using System.Collections.Generic;
using UnityEngine;

public enum ConsumableType
{
    ScoreBoost,         // +25 flat to next play's final score
    DecayFreeze,        // No decay this round
    DecayRemoval,       // Remove all decay from one card
    AceSwap,            // Lowest card scores as Ace this play
    DiffBoost,          // +4 to difference calculation this play (push to next tier)
    HandRefresh,        // Redraw entire hand (costs a turn)
    RotConverter,       // Fully rotted cards score their full point value this play
    GoldRush,           // Gain +3 gold immediately
}

[System.Serializable]
public class Consumable
{
    public ConsumableType type;
    public string displayName;
    public string description;
    public int cost;
    public Sprite icon;

    public Consumable(ConsumableType t, string name, string desc, int cost)
    {
        this.type        = t;
        this.displayName = name;
        this.description = desc;
        this.cost        = cost;
    }
}

/// <summary>
/// Tracks active consumable effects and applies them.
/// GameManager checks these flags each round/play.
/// </summary>
public class ConsumableSystem : MonoBehaviour
{
    public static ConsumableSystem Instance { get; private set; }

    // Active one-shot effects
    public bool scoreBoostedNextPlay   = false;
    public int  scoreBoostAmount       = 0;
    public bool decayFrozenThisRound   = false;
    public bool aceSwapNextPlay        = false;
    public bool diffBoostNextPlay      = false;
    public int  diffBoostAmount        = 4;
    public bool rotConverterNextPlay   = false;

    // Cards pending decay removal (set by shop UI when player picks a card)
    public Card cardPendingDecayRemoval = null;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // -------------------------------------------------------
    // Apply Consumable
    // -------------------------------------------------------

    public void UseConsumable(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.ScoreBoost:
                scoreBoostedNextPlay = true;
                scoreBoostAmount     = 20; // flat bonus — could be made variable
                Debug.Log("Consumable: Score Boost active (+20 next play)");
                break;

            case ConsumableType.DecayFreeze:
                decayFrozenThisRound = true;
                Debug.Log("Consumable: Decay frozen this round");
                break;

            case ConsumableType.DecayRemoval:
                // GameManager will prompt player to pick a card
                Debug.Log("Consumable: Decay Removal — pick a card to cleanse");
                break;

            case ConsumableType.AceSwap:
                aceSwapNextPlay = true;
                break;
            case ConsumableType.DiffBoost:
                diffBoostNextPlay = true;
                break;
            case ConsumableType.HandRefresh:
                // Handled directly by GameManager — flag is checked there
                break;
            case ConsumableType.RotConverter:
                rotConverterNextPlay = true;
                break;
            case ConsumableType.GoldRush:
                // Gold added immediately in GameManager
                break;
        }
    }

    public bool ConsumeDiffBoost()
    {
        if (!diffBoostNextPlay) return false;
        diffBoostNextPlay = false;
        return true;
    }

    public bool ConsumeRotConverter()
    {
        if (!rotConverterNextPlay) return false;
        rotConverterNextPlay = false;
        return true;
    }

    /// <summary>
    /// Removes all decay from a specific card.
    /// </summary>
    public void ApplyDecayRemoval(Card card)
    {
        if (card == null) return;
        card.decayLevel = 0;
        card.isDecayed  = false;
        cardPendingDecayRemoval = null;
        Debug.Log($"Decay removed from {card.GetDisplayValue()}{card.cardSuit}");
    }

    /// <summary>
    /// Called by GameManager after a play that consumed the score boost.
    /// </summary>
    public int ConsumeScoreBoost()
    {
        if (!scoreBoostedNextPlay) return 0;
        scoreBoostedNextPlay = false;
        int amount = scoreBoostAmount;
        scoreBoostAmount = 0;
        return amount;
    }

    /// <summary>
    /// Called by GameManager after decay phase to reset freeze.
    /// </summary>
    public void ConsumeDecayFreeze()
    {
        decayFrozenThisRound = false;
    }

    /// <summary>
    /// Called by ComboEvaluator path in GameManager when Ace Swap is active.
    /// Replaces the lowest card's value with 11 (Ace treated as high = 11 for scoring)
    /// but keeps the three-of-a-kind combo recognition using the real values.
    /// Returns the bonus score to add.
    /// </summary>
    public int ConsumeAceSwap(List<Card> playedCards)
    {
        if (!aceSwapNextPlay) return 0;
        aceSwapNextPlay = false;

        // Find the lowest value card and calculate what swapping it to Ace (11) adds
        Card lowest = null;
        foreach (Card c in playedCards)
        {
            if (lowest == null || c.cardValue < lowest.cardValue)
                lowest = c;
        }

        if (lowest == null) return 0;

        int bonus = 11 - lowest.cardValue; // difference between Ace and actual value
        Debug.Log($"Ace Swap: {lowest.GetDisplayValue()} treated as Ace, +{bonus} to base");
        return bonus;
    }

    // -------------------------------------------------------
    // Factory
    // -------------------------------------------------------

    public static Consumable MakeConsumable(ConsumableType type)
    {
        return type switch
        {
            ConsumableType.ScoreBoost   => new Consumable(type, "Score Boost",    "+25 flat to your next play's score.", 3),
            ConsumableType.DecayFreeze  => new Consumable(type, "Decay Freeze",   "No cards decay this round.", 4),
            ConsumableType.DecayRemoval => new Consumable(type, "Cleanse",        "Remove all decay from one card in hand.", 5),
            ConsumableType.AceSwap      => new Consumable(type, "Ace Swap",       "Your lowest card scores as an Ace this play.", 5),
            ConsumableType.DiffBoost    => new Consumable(type, "Diff Boost",     "+4 to your difference this play. May unlock the next tier.", 4),
            ConsumableType.HandRefresh  => new Consumable(type, "Hand Refresh",   "Discard your hand and draw fresh cards. Costs one turn.", 3),
            ConsumableType.RotConverter => new Consumable(type, "Rot Converter",  "Rotten cards score their full point value this play.", 6),
            ConsumableType.GoldRush     => new Consumable(type, "Gold Rush",      "Immediately gain +3 gold.", 2),
            _ => new Consumable(type, "Unknown", "???", 0)
        };
    }

    public static List<ConsumableType> AllTypes() => new List<ConsumableType>
    {
        ConsumableType.ScoreBoost,
        ConsumableType.DecayFreeze,
        ConsumableType.DecayRemoval,
        ConsumableType.AceSwap,
        ConsumableType.DiffBoost,
        ConsumableType.HandRefresh,
        ConsumableType.RotConverter,
        ConsumableType.GoldRush,
    };
}