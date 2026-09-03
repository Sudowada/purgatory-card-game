using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// CARD PATTERN SYSTEM
// Defines what conditions a played hand must meet to trigger a game event.
// All combos are data-defined here — no code changes needed to add new ones.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single hidden combo definition. Add new combos to the list in
/// CardPatternSystem.AllPatterns below.
/// </summary>
public class CardPattern
{
    // ── Match conditions ──────────────────────────────────────────────────────

    /// Card values that must be present. Uses cardValue (1=Ace, 13=King).
    public List<int>   RequiredValues     = new List<int>();

    /// If true, cards must be played in RequiredValues order.
    public bool        OrderMatters       = false;

    /// Minimum number of decayed cards required.
    public int         RequiredDecayed    = 0;

    /// If true, ALL cards in hand must be decayed (not just played cards).
    public bool        RequireFullHandRot = false;

    /// If set, card values must sum to this number.
    public int?        RequiredSum        = null;

    /// If set, all cards must be the same suit.
    public bool        RequireSameSuit    = false;

    /// Minimum number of cards that must be played.
    public int         MinCardCount       = 1;

    /// If set, only triggers when the player owns this relic.
    public RelicType?  RequiredRelic      = null;

    /// If set, cards must have these exact decay levels in this order.
    /// e.g. {1,2,3} means first card has decayLevel 1, second has 2, third has 3.
    public List<int>   RequiredDecayLevels = new List<int>();

    /// If set, only triggers from this round onwards.
    public int         UnlocksOnRound     = 0;

    // ── What this triggers ────────────────────────────────────────────────────
    public string      EventID;

    // ── Display ───────────────────────────────────────────────────────────────
    /// Internal name (for debugging). Players never see this.
    public string      DebugName;
}

/// <summary>
/// Checks played cards against all defined patterns and fires events.
/// Attach to GameManager or call from OnPlayPressed.
/// </summary>
public static class CardPatternSystem
{
    // ─────────────────────────────────────────────────────────────────────────
    // ALL HIDDEN COMBOS
    // Add new ones here. They are checked every time the player plays a hand.
    // ─────────────────────────────────────────────────────────────────────────
    public static readonly List<CardPattern> AllPatterns = new List<CardPattern>
    {
        // ── A. SEQUENCE CODES — feel like discovered passwords ────────────────

        new CardPattern
        {
            DebugName     = "The Code",
            RequiredValues = new List<int> { 1, 9, 4, 5 },
            OrderMatters  = true,
            EventID       = "evt_unlock_document_01",
        },
        new CardPattern
        {
            DebugName     = "Prison Code",
            RequiredValues = new List<int> { 3, 1, 3, 7 },
            OrderMatters  = true,
            EventID       = "evt_unlock_document_02",
        },
        new CardPattern
        {
            DebugName     = "Year Mark",
            RequiredValues = new List<int> { 2, 0, 2, 4 },
            OrderMatters  = true,
            EventID       = "evt_meta_hint_01",
        },
        new CardPattern
        {
            DebugName     = "Lucky Trap",
            RequiredValues = new List<int> { 7, 7, 7 },
            OrderMatters  = false,
            EventID       = "evt_lucky_trap",
        },
        new CardPattern
        {
            DebugName     = "Door Click",
            RequiredValues = new List<int> { 4, 4, 2 },
            OrderMatters  = true,
            EventID       = "evt_door_sound",
        },
        new CardPattern
        {
            DebugName     = "The Last Hand",
            RequiredValues = new List<int> { 13, 1 },
            OrderMatters  = true,
            EventID       = "evt_king_and_ace",
        },
        new CardPattern
        {
            DebugName     = "Countdown",
            RequiredValues = new List<int> { 3, 2, 1 },
            OrderMatters  = true,
            EventID       = "evt_countdown",
        },
        new CardPattern
        {
            DebugName     = "Mirror",
            RequiredValues = new List<int> { 1, 2, 1 },
            OrderMatters  = true,
            EventID       = "evt_mirror",
        },

        // ── B. DECAY / ROT COMBOS — risk = reward ────────────────────────────

        new CardPattern
        {
            DebugName      = "Triple Six Rot",
            RequiredValues  = new List<int> { 6, 6, 6 },
            RequiredDecayed = 3,
            EventID        = "evt_rot_666",
        },
        new CardPattern
        {
            DebugName      = "Rotten Kings",
            RequiredValues  = new List<int> { 13, 13 },
            RequiredDecayed = 2,
            EventID        = "evt_cursed_kings",
        },
        new CardPattern
        {
            DebugName      = "Full Rot Hand",
            RequireFullHandRot = true,
            MinCardCount   = 3,
            EventID        = "evt_full_rot_hand",
        },
        new CardPattern
        {
            DebugName      = "Rotten Ace",
            RequiredValues  = new List<int> { 1 },
            RequiredDecayed = 1,
            EventID        = "evt_rotten_ace",
        },
        new CardPattern
        {
            DebugName      = "Infection",
            RequiredValues  = new List<int>(),     // any values
            RequiredDecayed = 2,
            MinCardCount   = 3,
            EventID        = "evt_infection",
        },
        new CardPattern
        {
            DebugName      = "Death's Hand",
            RequiredValues  = new List<int> { 13, 12, 11 },
            RequiredDecayed = 3,
            OrderMatters   = true,
            EventID        = "evt_deaths_hand",
        },

        // ── C. MATHEMATICAL PATTERNS — make players feel smart ────────────────

        new CardPattern
        {
            DebugName    = "Blackjack",
            RequiredSum  = 21,
            MinCardCount = 2,
            EventID      = "evt_blackjack",
        },
        new CardPattern
        {
            DebugName     = "Perfect Ascend",
            RequiredValues = new List<int> { 2, 3, 4, 5 },
            OrderMatters  = true,
            EventID       = "evt_perfect_ascend",
        },
        new CardPattern
        {
            DebugName     = "Perfect Descend",
            RequiredValues = new List<int> { 10, 9, 8 },
            OrderMatters  = true,
            EventID       = "evt_perfect_descend",
        },
        new CardPattern
        {
            DebugName     = "All Evens",
            RequiredValues = new List<int> { 2, 4, 6, 8 },
            OrderMatters  = false,
            EventID       = "evt_all_evens",
        },
        new CardPattern
        {
            DebugName     = "Prime Hand",
            RequiredValues = new List<int> { 2, 3, 5, 7 },
            OrderMatters  = false,
            EventID       = "evt_prime_hand",
        },
        new CardPattern
        {
            DebugName    = "Baker's Dozen",
            RequiredSum  = 13,
            MinCardCount = 3,
            EventID      = "evt_bakers_dozen",
        },
        new CardPattern
        {
            DebugName    = "Zero Sum",
            RequiredSum  = 0,
            MinCardCount = 2,
            EventID      = "evt_zero_sum",
        },
        new CardPattern
        {
            DebugName     = "Fibonacci",
            RequiredValues = new List<int> { 1, 1, 2, 3, 5 },
            OrderMatters  = false,
            EventID       = "evt_fibonacci",
        },

        // ── D. SUIT PATTERNS ──────────────────────────────────────────────────

        new CardPattern
        {
            DebugName      = "Full Hearts",
            RequireSameSuit = true,
            MinCardCount   = 3,
            EventID        = "evt_full_suit",     // handler checks which suit
        },
        new CardPattern
        {
            DebugName      = "Rotten Flush",
            RequireSameSuit = true,
            RequiredDecayed = 2,
            MinCardCount   = 3,
            EventID        = "evt_rotten_flush",
        },

        // ── E. BREAKING THE RULES ─────────────────────────────────────────────

        new CardPattern
        {
            DebugName     = "Triple Duplicate",
            RequiredValues = new List<int>(),     // any value
            MinCardCount  = 3,
            OrderMatters  = false,
            // Special: all same value — checked separately in MatchesPattern
            EventID       = "evt_triple_duplicate",
        },
        new CardPattern
        {
            DebugName    = "Overkill",
            RequiredSum  = 52,                     // max possible hand
            MinCardCount = 4,
            EventID      = "evt_overkill",
        },
        new CardPattern
        {
            DebugName     = "Royal Court",
            RequiredValues = new List<int> { 11, 12, 13 },
            OrderMatters  = false,
            EventID       = "evt_royal_court",
        },
        new CardPattern
        {
            DebugName     = "Peasant's Hand",
            RequiredValues = new List<int> { 2, 2, 2 },
            OrderMatters  = false,
            EventID       = "evt_peasants_hand",
        },

        // ── F. RELIC-GATED COMBOS — only possible with certain builds ─────────

        new CardPattern
        {
            DebugName      = "Paradox",
            RequiredValues  = new List<int> { 1, 13 },
            RequiredDecayed = 1,
            RequiredRelic  = RelicType.AnchorCard,
            EventID        = "evt_paradox",
        },
        new CardPattern
        {
            DebugName     = "Jackpot",
            RequiredValues = new List<int> { 7, 7 },
            RequiredRelic = RelicType.GoldenCard,
            EventID       = "evt_jackpot",
        },
        new CardPattern
        {
            DebugName      = "Plague Bloom",
            RequiredDecayed = 4,
            RequiredRelic  = RelicType.PlagueAll,
            MinCardCount   = 4,
            EventID        = "evt_plague_bloom",
        },

        // ── G. LATE GAME / ROUND-GATED ────────────────────────────────────────

        new CardPattern
        {
            DebugName      = "The Offering",
            RequiredValues  = new List<int> { 1, 11, 12, 13 },
            OrderMatters   = false,
            UnlocksOnRound = 5,
            EventID        = "evt_the_offering",
        },
        // ── H. PERSISTENT META PUZZLES — reward carries across runs ──────────

        new CardPattern
        {
            DebugName     = "The Ascent",
            RequiredValues = new List<int> { 1, 2, 3 },
            OrderMatters  = false,
            EventID       = "evt_ascent_gold",
        },
        new CardPattern
        {
            DebugName          = "The Rot Sequence",
            RequiredDecayLevels = new List<int> { 1, 2, 3 },
            OrderMatters        = true,
            EventID             = "evt_rot_sequence_gold",
        },

        new CardPattern
        {
            DebugName      = "Endgame",
            RequiredValues  = new List<int> { 10, 10, 10 },
            UnlocksOnRound = 8,
            EventID        = "evt_endgame",
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // PATTERN MATCHING
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this after every hand is played. Returns all event IDs that matched.
    /// </summary>
    public static List<string> CheckPatterns(List<Card> played, List<Card> fullHand, int currentRound)
    {
        var fired = new List<string>();
        foreach (var pattern in AllPatterns)
        {
            if (Matches(pattern, played, fullHand, currentRound))
                fired.Add(pattern.EventID);
        }
        return fired;
    }

    static bool Matches(CardPattern p, List<Card> played, List<Card> fullHand, int round)
    {
        // Round gate
        if (round < p.UnlocksOnRound) return false;

        // Minimum card count
        if (played.Count < p.MinCardCount) return false;

        // Relic requirement
        if (p.RequiredRelic.HasValue && RelicSystem.Instance != null)
            if (!RelicSystem.Instance.HasRelic(p.RequiredRelic.Value)) return false;

        // Full hand rot
        if (p.RequireFullHandRot && !fullHand.All(c => c.isDecayed)) return false;

        // Decayed count in PLAYED cards
        if (p.RequiredDecayed > 0)
        {
            int rotCount = played.Count(c => c.isDecayed);
            if (rotCount < p.RequiredDecayed) return false;
        }

        // Sum check
        if (p.RequiredSum.HasValue)
        {
            int sum = played.Sum(c => c.cardValue);
            if (sum != p.RequiredSum.Value) return false;
        }

        // Same suit
        if (p.RequireSameSuit)
        {
            string suit = played[0].cardSuit;
            if (!played.All(c => c.cardSuit == suit)) return false;
        }

        // Triple duplicate special case (all same value)
        if (p.EventID == "evt_triple_duplicate")
        {
            int first = played[0].cardValue;
            if (played.Count < 3 || !played.All(c => c.cardValue == first)) return false;
            return true;
        }

        // Decay level sequence matching
        if (p.RequiredDecayLevels != null && p.RequiredDecayLevels.Count > 0)
        {
            if (played.Count < p.RequiredDecayLevels.Count) return false;
            // Always order-sensitive — decay levels are a sequence
            for (int i = 0; i < p.RequiredDecayLevels.Count; i++)
                if (played[i].decayLevel != p.RequiredDecayLevels[i]) return false;
        }

        // Value matching
        if (p.RequiredValues.Count > 0)
        {
            if (played.Count < p.RequiredValues.Count) return false;

            if (p.OrderMatters)
            {
                for (int i = 0; i < p.RequiredValues.Count; i++)
                    if (played[i].cardValue != p.RequiredValues[i]) return false;
            }
            else
            {
                var vals = played.Select(c => c.cardValue).ToList();
                foreach (int v in p.RequiredValues)
                {
                    if (!vals.Contains(v)) return false;
                    vals.Remove(v);     // consume so duplicates are handled correctly
                }
            }
        }

        return true;
    }
}