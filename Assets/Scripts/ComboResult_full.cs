using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Full scoring breakdown for Points x AdditiveMult x MultiplicativeMult x TierMult.
/// Legacy properties kept for backward compat with AnimateScorePreview.
/// </summary>
public class ComboResult
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public ComboType  combo;
    public string     comboName;
    public List<Card> scoringCards;

    // ── Scoring components (shown in HUD) ─────────────────────────────────────
    public int   points;              // Sum of PointValues after decay mult
    public float additiveMult;        // 1 + combo bonus (relics add to this)
    public float multiplicativeMult;  // Relic multiplicative stack
    public float tierMult;            // Diff tier x0.25 penalty -> x5.00

    // ── Diff info ─────────────────────────────────────────────────────────────
    public int    difference;         // faceValue sum - enemyValue
    public int    diffDots;           // 0-8 for HUD dots
    public string diffDotStr;
    public int    nextTierAt;         // face-value points needed for next tier
    public float  nextTierMult;

    // ── Penalty ───────────────────────────────────────────────────────────────
    public bool isPenalty;
    public int  penaltyAmount;

    // ── Decay ─────────────────────────────────────────────────────────────────
    public bool  containsDecayedCard;
    public int   decayedCount;
    public float decayPenaltyMult;

    // ── Final ─────────────────────────────────────────────────────────────────
    public int    finalScore;         // net change (negative if penalty)
    public string description;

    // ── Legacy aliases (keeps AnimateScorePreview and RefreshScorePreview working) ──
    public int   rawBase      => points;
    public int   adjustedBase => Mathf.FloorToInt(points * tierMult);
    public int   decayedBase  => Mathf.FloorToInt(points * decayPenaltyMult);
    public float multiplier   => tierMult;
    public bool  wasSquared   => false;
}
