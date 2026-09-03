using System.Collections.Generic;
using System.Linq;
using UnityEngine;
 
public enum ComboType
{
    HighCard, Pair, TwoPair, ThreeOfAKind,
    AscendingRun, AllRoyals, FullHouse, FourOfAKind, FiveOfAKind
}
 
/// <summary>
/// NEW FORMULA:
///   diff = sum(card.faceValue) - enemyCardValue
///
///   if diff < 0:
///     finalScore = -penaltyAmount  (quadratic below -10, stage-scaled exponential at -10+)
///
///   if diff >= 0:
///     points        = sum(PointValues) x decayMult
///     additiveMult  = 1 + comboBonus   (relics add to this in RelicSystem)
///     tierMult      = diff tier x1.00-x5.00
///     finalScore    = points x additiveMult x tierMult
///     (multiplicativeMult applied by RelicSystem.ApplyRelicModifiers after this call)
/// </summary>
public static class ComboEvaluator
{
    private const int LOW_CARD_THRESHOLD = 5;
 
    public static ComboResult Evaluate(
        List<Card> played,
        int enemyCardValue = 0,
        int minimumBase    = 0,
        int stageNumber    = 1)
    {
        if (played == null || played.Count == 0)
            return EmptyResult();
 
        var sorted = played.OrderBy(c => c.cardValue).ToList();
 
        // ── Points = sum of PointValues with decay mult ───────────────────────
        int   rawPoints    = played.Sum(c => c.PointValue);
        int   decayedCount = played.Count(c => c.isDecayed);
        float decayMult    = decayedCount > 0 ? Mathf.Pow(0.75f, decayedCount) : 1f;
        int   points       = Mathf.Max(minimumBase, Mathf.FloorToInt(rawPoints * decayMult));
 
        // ── Diff uses faceValue NOT cardValue ─────────────────────────────────
        int faceSum    = played.Sum(c => c.faceValue);
        int difference = faceSum - enemyCardValue;
 
        // ── Tier lookup ───────────────────────────────────────────────────────
        float tierMult;
        int   diffDots;
        string diffDotStr;
        int   nextTierAt;
        float nextTierMult;
 
        if (difference < 0)
        {
            tierMult = 0.25f; diffDots = 0; diffDotStr = "X";
            nextTierAt = -difference; nextTierMult = 1.00f;
        }
        else if (difference <= 2)  { tierMult=1.00f; diffDots=1; diffDotStr="*";        nextTierAt=3 -difference; nextTierMult=1.25f; }
        else if (difference <= 5)  { tierMult=1.25f; diffDots=2; diffDotStr="**";       nextTierAt=6 -difference; nextTierMult=1.50f; }
        else if (difference <= 9)  { tierMult=1.50f; diffDots=3; diffDotStr="***";      nextTierAt=10-difference; nextTierMult=2.00f; }
        else if (difference <= 14) { tierMult=2.00f; diffDots=4; diffDotStr="****";     nextTierAt=15-difference; nextTierMult=2.50f; }
        else if (difference <= 19) { tierMult=2.50f; diffDots=5; diffDotStr="*****";    nextTierAt=20-difference; nextTierMult=3.00f; }
        else if (difference <= 24) { tierMult=3.00f; diffDots=6; diffDotStr="******";   nextTierAt=25-difference; nextTierMult=3.75f; }
        else if (difference <= 30) { tierMult=3.75f; diffDots=7; diffDotStr="*******";  nextTierAt=31-difference; nextTierMult=5.00f; }
        else                       { tierMult=5.00f; diffDots=8; diffDotStr="********"; nextTierAt=-1;            nextTierMult=5.00f; }
 
        // ── Combo -> additive mult bonus ──────────────────────────────────────
        (ComboType combo, float comboAddBonus, string comboLabel) = DetectCombo(sorted);
        float additiveMult = 1f + comboAddBonus;
 
        // ── Penalty calculation ───────────────────────────────────────────────
        bool isPenalty     = difference < 0;
        int  penaltyAmount = 0;
        if (isPenalty)
        {
            int absDiff = Mathf.Abs(difference);
            if (absDiff < 10)
            {
                // Gentle quadratic: barely stings below 5, noticeable around 8-9
                penaltyAmount = Mathf.RoundToInt(absDiff * absDiff * 0.5f);
            }
            else
            {
                // Stage-scaled exponential wall past -10
                float stagePenaltyBase = GetStagePenaltyBase(stageNumber);
                float stageScale       = GetStageScale(stageNumber);
                float overage          = absDiff - 10f;
                penaltyAmount = Mathf.RoundToInt(stagePenaltyBase + overage * overage * stageScale);
            }
        }
 
        // ── Final score (multiplicativeMult applied later by RelicSystem) ─────
        int finalScore = isPenalty
            ? -penaltyAmount
            : Mathf.Max(minimumBase, Mathf.RoundToInt(points * additiveMult * tierMult));
 
        // ── Description ───────────────────────────────────────────────────────
        string decayNote = decayedCount > 0 ? $" [rot x{decayMult:F2}]" : "";
        string nextHint  = nextTierAt > 0
            ? $" (+{nextTierAt} -> x{nextTierMult:0.##})"
            : nextTierAt == -1 ? " [MAX]" : "";
        string desc = isPenalty
            ? $"{comboLabel} | PENALTY diff:{difference} | -{penaltyAmount}"
            : $"{comboLabel} | diff:+{difference}{diffDotStr} | {points} x {additiveMult:0.##} x {tierMult:0.##} = {finalScore}{decayNote}{nextHint}";
 
        return new ComboResult
        {
            combo               = combo,
            comboName           = comboLabel,
            scoringCards        = sorted,
            points              = points,
            additiveMult        = additiveMult,
            multiplicativeMult  = 1f,
            tierMult            = tierMult,
            difference          = difference,
            diffDots            = diffDots,
            diffDotStr          = diffDotStr,
            nextTierAt          = nextTierAt,
            nextTierMult        = nextTierMult,
            containsDecayedCard = decayedCount > 0,
            decayedCount        = decayedCount,
            decayPenaltyMult    = decayMult,
            isPenalty           = isPenalty,
            penaltyAmount       = penaltyAmount,
            finalScore          = finalScore,
            description         = desc,
        };
    }
 
    // ── Stage penalty scaling ─────────────────────────────────────────────────
 
    private static float GetStagePenaltyBase(int stage) => stage switch
    {
        1 or 2 => 50f,
        3 or 4 => 100f,
        5 or 6 => 200f,
        _      => 400f
    };
 
    private static float GetStageScale(int stage) => stage switch
    {
        1 or 2 => 0.5f,
        3 or 4 => 1.0f,
        5 or 6 => 2.0f,
        _      => 4.0f
    };
 
    // ── Combo Detection ───────────────────────────────────────────────────────
    // Returns additive mult bonus (added to 1.0 base)
    // e.g. Pair returns +0.5 -> additiveMult = 1.5
 
    private static (ComboType, float, string) DetectCombo(List<Card> sorted)
    {
        if (IsFiveOfAKind(sorted))  return (ComboType.FiveOfAKind,  4.0f, "Five of a Kind");
        if (IsFourOfAKind(sorted))  return (ComboType.FourOfAKind,  2.5f, "Four of a Kind");
        if (IsFullHouse(sorted))    return (ComboType.FullHouse,    2.0f, "Full House");
        if (IsAllRoyals(sorted))    return (ComboType.AllRoyals,    1.5f, "All Royals");
        if (IsAscendingRun(sorted)) return (ComboType.AscendingRun, 1.0f, "Run");
        if (IsThreeOfAKind(sorted)) return GetThreeOfAKindBonus(sorted);
        if (IsTwoPair(sorted))      return (ComboType.TwoPair,      0.8f, "Two Pair");
        if (IsPair(sorted))         return (ComboType.Pair,         0.5f, "Pair");
        return                             (ComboType.HighCard,     0.0f, "High Card");
    }
 
    private static (ComboType, float, string) GetThreeOfAKindBonus(List<Card> sorted)
    {
        int v = sorted.GroupBy(c => c.cardValue).First(g => g.Count() >= 3).Key;
        return v < LOW_CARD_THRESHOLD
            ? (ComboType.ThreeOfAKind, 1.8f, $"Three {v}s (High Risk)")
            : (ComboType.ThreeOfAKind, 1.2f, "Three of a Kind");
    }
 
    // ── Combo Checks ──────────────────────────────────────────────────────────
 
    private static bool IsPair(List<Card> c)         => c.GroupBy(x => x.cardValue).Any(g => g.Count() >= 2);
    private static bool IsTwoPair(List<Card> c)      => c.GroupBy(x => x.cardValue).Count(g => g.Count() >= 2) >= 2;
    private static bool IsThreeOfAKind(List<Card> c) => c.GroupBy(x => x.cardValue).Any(g => g.Count() >= 3);
    private static bool IsFourOfAKind(List<Card> c)  => c.GroupBy(x => x.cardValue).Any(g => g.Count() >= 4);
    private static bool IsFiveOfAKind(List<Card> c)  => c.GroupBy(x => x.cardValue).Any(g => g.Count() >= 5);
 
    private static bool IsFullHouse(List<Card> c)
    {
        var g = c.GroupBy(x => x.cardValue).ToList();
        return g.Any(x => x.Count() >= 3) && g.Count(x => x.Count() >= 2) >= 2;
    }
 
    private static bool IsAscendingRun(List<Card> c)
    {
        var v = c.Select(x => x.cardValue).OrderBy(x => x).ToList();
        for (int i = 1; i < v.Count; i++) if (v[i] != v[i-1]+1) return false;
        return true;
    }
 
    private static bool IsAllRoyals(List<Card> c) => c.All(x => x.cardValue >= 11);
 
    private static ComboResult EmptyResult() => new ComboResult
    {
        combo = ComboType.HighCard, comboName = "No cards",
        points = 0, additiveMult = 1f, multiplicativeMult = 1f,
        tierMult = 1f, finalScore = 0, description = "No cards played"
    };
}