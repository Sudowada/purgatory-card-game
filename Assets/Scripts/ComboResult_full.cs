using System.Collections.Generic;
using UnityEngine;

public class ComboResult
{
    public ComboType combo;
    public string comboName;
    public List<Card> scoringCards;
    public int points;
    public float additiveMult;
    public float multiplicativeMult;
    public float tierMult;
    public int difference;
    public int diffDots;
    public string diffDotStr;
    public int nextTierAt;
    public float nextTierMult;
    public bool isPenalty;
    public int penaltyAmount;
    public bool containsDecayedCard;
    public int decayedCount;
    public float decayPenaltyMult;
    public int finalScore;
    public string description;
    public int flatBonus;
    public float CombinedMultiplier => additiveMult * multiplicativeMult * tierMult;
    public int rawBase => points;
    public int adjustedBase => Mathf.FloorToInt(points * tierMult);
    public int decayedBase => Mathf.FloorToInt(points * decayPenaltyMult);
    public float multiplier => tierMult;
    public bool wasSquared => false;
}
