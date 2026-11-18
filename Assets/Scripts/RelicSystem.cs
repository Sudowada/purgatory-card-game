using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public enum RelicCategory { RuleChanger, DecayManipulation, ComboEngine, ScalingMultiplier, CardManipulation, RiskChallenge }
public enum RelicType
{
    FourthHand, DuelistsCode, PerfectGamble, ComboMachine, CardCycler, TacticalDelay,
    RotAlchemist, CorrosionEngine, SlowRot, RotBurst, Purifier, NecromancyRelic,
    PairAddict, LowCardGenius, HighRoller, OddCollector, EvenMaster, ComboBank,
    LoneWolf, TurnInvestor, GrindCore, PerfectHand, HighValueInvest, TimeEngine,
    AnchorCard, GoldenCard, DrawReactor, ValueLock,
    GlassCannon, DoubleDecay, InstantRot, PlagueAll,
    // Append new IDs so existing Inspector references retain their values.
    LoadedDice, RerollToken, CoinFlip,
    // Flexible scoring relics: append only to preserve serialized enum IDs.
    SpadeSeal, HeartSeal, DiamondSeal, ClubSeal,
    SmallMult, MediumMult, LargeMult,
}
public enum RelicRarity { Common, Uncommon, Rare }

[System.Serializable]
public class Relic
{
    public RelicType     type;
    public RelicCategory category;
    public string        displayName;
    public string        description;
    public string        flavorText;
    public RelicRarity rarity => RelicSystem.GetRarity(type);
    public Relic(RelicType t, RelicCategory cat, string name, string desc, string flavor = "")
    {
        type = t; category = cat; displayName = name; description = desc; flavorText = flavor;
    }
}
public class RelicSystem : MonoBehaviour
{
    public static RelicSystem Instance { get; private set; }
    public List<Relic> activeRelics = new List<Relic>();
    [SerializeField, Min(1)] private int relicCapacity = 5;
    public int RelicCapacity => relicCapacity;
    public bool CanAcquireRelic(RelicType type) => !HasRelic(type) && activeRelics.Count < relicCapacity;
    private int  _maxPlay             = 3;
    private int  _decayCount          = 3;
    private int  _minimumBase         = 0;
    private bool _decayEveryOtherTurn = false;
    private int  _decayTurnCounter    = 0;
    private bool _decayRotOnStack     = false;
    private bool _decayAll            = false;
    public float persistentMult = 1.0f;
    public float LoadedDiceMult { get; private set; }

    public void OnShopRerolled()
    {
        if (!HasRelic(RelicType.LoadedDice)) return;
        LoadedDiceMult += 3f;
        Debug.Log($"[Relics] Loaded Dice now gives +{LoadedDiceMult:0} Mult.");
    }

    public static RelicRarity GetRarity(RelicType type)
    {
        switch (type)
        {
            // xMult is intentionally rare: the difference tier remains the
            // reliable source of multiplicative power for every build.
            case RelicType.DuelistsCode:
            case RelicType.LoneWolf:
            case RelicType.GrindCore:
            case RelicType.HighValueInvest:
            case RelicType.TimeEngine:
            case RelicType.ComboBank:
            case RelicType.GoldenCard:
            case RelicType.GlassCannon:
            case RelicType.DoubleDecay:
                return RelicRarity.Rare;
            // Loaded Dice and Turn Investor give +Mult, not xMult.
            case RelicType.LoadedDice:
            case RelicType.TurnInvestor:
            case RelicType.RerollToken: return RelicRarity.Uncommon;
            case RelicType.CoinFlip: return RelicRarity.Common;
            // Suit-scoring relics are versatile but still reward deckbuilding.
            case RelicType.SpadeSeal:
            case RelicType.HeartSeal:
            case RelicType.DiamondSeal:
            case RelicType.ClubSeal:
            case RelicType.MediumMult: return RelicRarity.Uncommon;
            case RelicType.SmallMult: return RelicRarity.Common;
            case RelicType.LargeMult: return RelicRarity.Rare;
        }
        // Preserve the old shop's category buckets for legacy relics until
        // individual rarities are assigned during the catalogue overhaul.
        var category = MakeRelicStatic(type).category;
        if (category == RelicCategory.RuleChanger || category == RelicCategory.DecayManipulation)
            return RelicRarity.Common;
        if (category == RelicCategory.ComboEngine || category == RelicCategory.ScalingMultiplier)
            return RelicRarity.Uncommon;
        return RelicRarity.Rare;
    }

    private readonly Dictionary<RelicType, float> earnedMult = new Dictionary<RelicType, float>();
    private void GainMult(RelicType type, float amount)
    {
        earnedMult[type] = (earnedMult.TryGetValue(type, out float old) ? old : 0f) + amount;
        persistentMult += amount;
    }
    private int  _winStreak     = 0;
    private int  _turnsSurvived = 0;
    private HashSet<ComboType> _combosSeenThisRun = new HashSet<ComboType>();
    private float _comboBankBonus = 0f;
    public bool gamblerDiscardAvailable = false;
    public int  goldenCardIndex         = -1;
    private bool _goldenCardInPlay      = false;
    public void MarkGoldenCardInPlay() => _goldenCardInPlay = true;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }
    public void AddRelic(RelicType type)
    {
        if (HasRelic(type)) return;
        activeRelics.Add(MakeRelicStatic(type));
        OnRelicAdded(type);
        Recalculate();
    }
    public void RemoveRelic(RelicType type)
    {
        activeRelics.RemoveAll(r => r.type == type);
        if (type == RelicType.LoadedDice) LoadedDiceMult = 0f;
        ShopManager.Instance?.OnEconomyRelicRemoved(type);
        if (earnedMult.TryGetValue(type, out float earned))
        { persistentMult = Mathf.Max(1f, persistentMult - earned); earnedMult.Remove(type); }
        if (type == RelicType.ComboBank) _comboBankBonus = 0f;
        if (type == RelicType.PerfectGamble) gamblerDiscardAvailable = false;
        if (type == RelicType.GoldenCard) { goldenCardIndex = -1; _goldenCardInPlay = false; }
        Recalculate();
    }
    public bool HasRelic(RelicType type) => activeRelics.Exists(r => r.type == type);

    /// <summary>
    /// Add points only; never alter face value, identity, combos, or difference.
    /// Called by Card.PointValue, so the on-card value and final Points agree.
    /// </summary>
    public int GetSuitPointBonus(string suit)
    {
        switch (suit)
        {
            case "♠": return HasRelic(RelicType.SpadeSeal) ? 15 : 0;
            case "♥": return HasRelic(RelicType.HeartSeal) ? 15 : 0;
            case "♦": return HasRelic(RelicType.DiamondSeal) ? 15 : 0;
            case "♣": return HasRelic(RelicType.ClubSeal) ? 15 : 0;
            default: return 0;
        }
    }
    private void OnRelicAdded(RelicType type)
    {
        ShopManager.Instance?.OnEconomyRelicAcquired(type);
        if (type == RelicType.PerfectGamble)
            gamblerDiscardAvailable = true;
    }
    private void Recalculate()
    {
        _maxPlay = 3; _decayCount = 3; _minimumBase = 0;
        _decayEveryOtherTurn = false; _decayRotOnStack = false; _decayAll = false;
        foreach (var r in activeRelics)
        {
            switch (r.type)
            {
                case RelicType.FourthHand:   _maxPlay = Mathf.Max(_maxPlay, 4); break;
                case RelicType.DuelistsCode: _maxPlay = 1; break;
                case RelicType.SlowRot:      _decayEveryOtherTurn = true; break;
                case RelicType.DoubleDecay:  _decayCount *= 2; break;
                case RelicType.InstantRot:   _decayRotOnStack = true; break;
                case RelicType.PlagueAll:    _decayAll = true; break;
            }
        }
    }
    public int  GetMaxPlay()         => _maxPlay;
    public int  GetDecayCount()      => _decayCount;
    public bool GetDecayRotOnStack() => _decayRotOnStack;
    public bool GetDecayAll()        => _decayAll;
    public int  GetMinimumBase()     => _minimumBase;
    public bool ShouldDecayThisTurn()
    {
        _decayTurnCounter++;
        if (!_decayEveryOtherTurn) return true;
        return (_decayTurnCounter % 2 == 0);
    }
    public void OnRoundStart(int roundNumber)
    {
        if (HasRelic(RelicType.PerfectGamble))
            gamblerDiscardAvailable = true;
        if (HasRelic(RelicType.GoldenCard))
            goldenCardIndex = Random.Range(0, 7);
        else
            goldenCardIndex = -1;
        if (HasRelic(RelicType.TimeEngine))
            GainMult(RelicType.TimeEngine, 0.08f * (roundNumber - 1));
        if (HasRelic(RelicType.ComboBank))
        {
            int unusedCount = System.Enum.GetValues(typeof(ComboType)).Length - _combosSeenThisRun.Count;
            _comboBankBonus += unusedCount * 0.1f;
        }
    }
    /// <summary>
    /// Award the Lone Wolf stack once for a committed single-card play.
    /// Called after score calculation so this stack affects subsequent hands.
    /// Kept separate from OnHandPlayed until the other scaling relic hooks are
    /// intentionally connected; don't activate unrelated relic effects here.
    /// </summary>
    public void OnSingleCardPlayed(int cardCount)
    {
        if (cardCount != 1 || !HasRelic(RelicType.LoneWolf)) return;
        GainMult(RelicType.LoneWolf, 0.2f);
    }

    public void OnHandPlayed(ComboResult result, bool playerWon)
    {
        _turnsSurvived++;
        if (HasRelic(RelicType.HighValueInvest) && result.scoringCards != null)
            foreach (var c in result.scoringCards)
                if (c.cardValue >= 10) GainMult(RelicType.HighValueInvest, 0.05f);
        if (playerWon)
        {
            _winStreak++;
            if (HasRelic(RelicType.GrindCore))
                GainMult(RelicType.GrindCore, 0.1f * _winStreak);
        }
        else _winStreak = 0;
        _combosSeenThisRun.Add(result.combo);
    }
    public bool IsDecayProtected(Card card, List<Card> allCards)
    {
        if (HasRelic(RelicType.AnchorCard))
        {
            int minVal = allCards.Min(c => c.cardValue);
            if (card.cardValue == minVal) return true;
        }
        if (HasRelic(RelicType.ValueLock))
        {
            int maxVal = allCards.Max(c => c.cardValue);
            if (card.cardValue == maxVal) return true;
        }
        return false;
    }
    public int GetEffectiveCardValue(Card card, List<Card> allCards)
    {
        int val = card.cardValue;
        if (HasRelic(RelicType.CorrosionEngine) && card.decayLevel > 0)
            val += card.decayLevel * 2;
        if (HasRelic(RelicType.NecromancyRelic) && card.isDecayed)
            val += 8;
        if (HasRelic(RelicType.HighRoller) && val >= 10)
            val += 5;
        return val;
    }
    public int OnCardFullyRotted(Card card)
    {
        if (HasRelic(RelicType.RotBurst))
        {
            AudioManager.Instance?.Play("combo_big");
            return 10;
        }
        return 0;
    }
    public void OnDecayPhaseEnd(List<Card> handCards)
    {
        if (!HasRelic(RelicType.Purifier)) return;
        var mostDecayed = handCards
            .Where(c => c.decayLevel > 0 && !c.isDecayed)
            .OrderByDescending(c => c.decayLevel)
            .FirstOrDefault();
        if (mostDecayed != null && mostDecayed.decayLevel > 0)
        {
            mostDecayed.decayLevel--;
            AudioManager.Instance?.Play("ui_click");
        }
    }
    // ─────────────────────────────────────────────────────────────────────────
    // SCORE MODIFIERS — NEW Points × AdditiveMult × MultiplicativeMult × TierMult
    // ─────────────────────────────────────────────────────────────────────────
    public int ApplyRelicModifiers(ComboResult result, bool consumeOneShotEffects = true)
    {
        // Penalty hands — relics don't reduce penalty except GlassCannon doubles it
        if (result.isPenalty)
        {
            int penalty = result.penaltyAmount;
            if (HasRelic(RelicType.GlassCannon)) penalty = Mathf.RoundToInt(penalty * 2f);
            result.penaltyAmount = penalty;
            result.finalScore = -penalty;
            return result.finalScore;
        }
        var   cards   = result.scoringCards;
        float addMult = result.additiveMult;
        if (HasRelic(RelicType.LoadedDice)) addMult += LoadedDiceMult;
        // General-purpose additive Mult: compatible with any combo and suit.
        if (HasRelic(RelicType.SmallMult)) addMult += 3f;
        if (HasRelic(RelicType.MediumMult)) addMult += 6f;
        if (HasRelic(RelicType.LargeMult)) addMult += 9f;
        // Turn Investor: dynamic +Mult from gold HELD, not spent or earned
        // lifetime gold. Uncapped, recalculated for both preview and scoring.
        if (HasRelic(RelicType.TurnInvestor) && ShopManager.Instance != null)
            addMult += (ShopManager.Instance.playerGold / 5) * 2f;
        // Starts with base 1 + combo bonus.
        float mulMult = 1f;                    // relic multiplicative stack
        // ── Persistent xMult: harder-to-find, run-scaling relics ─────────────
        mulMult *= persistentMult;
        // ── ComboBank ─────────────────────────────────────────────────────────
        if (HasRelic(RelicType.ComboBank) && _comboBankBonus > 0)
            mulMult *= (1f + _comboBankBonus);
        // ── DuelistsCode: ×3 multiplicative if 1 card ────────────────────────
        if (HasRelic(RelicType.DuelistsCode) && cards?.Count == 1)
            mulMult *= 3f;
        // ── GlassCannon: ×3 multiplicative ───────────────────────────────────
        if (HasRelic(RelicType.GlassCannon))
            mulMult *= 3f;
        // ── DoubleDecay: ×2 multiplicative ───────────────────────────────────
        if (HasRelic(RelicType.DoubleDecay))
            mulMult *= 2f;
        // ── RotAlchemist: each decay stack adds +15% to additiveMult ─────────
        if (HasRelic(RelicType.RotAlchemist) && cards != null)
            addMult += cards.Sum(c => c.decayLevel) * 0.15f;
        // ── PairAddict: triples additive mult for pairs ───────────────────────
        if (HasRelic(RelicType.PairAddict) &&
            (result.combo == ComboType.Pair || result.combo == ComboType.TwoPair))
            addMult += result.additiveMult * 2f;
        // ── ComboMachine: pair → three-of-a-kind additive bridge ─────────────
        if (HasRelic(RelicType.ComboMachine) && result.combo == ComboType.Pair)
            addMult += 0.7f;
        // ── LowCardGenius: all cards ≤4 → double additive mult ───────────────
        if (HasRelic(RelicType.LowCardGenius) && cards != null)
            if (cards.All(c => c.cardValue <= 4))
                addMult *= 2f;
        // ── OddCollector: +20% additive per odd card ──────────────────────────
        if (HasRelic(RelicType.OddCollector) && cards != null)
            addMult += cards.Count(c => c.cardValue % 2 != 0) * 0.2f;
        // ── EvenMaster: +20% additive per even card ───────────────────────────
        if (HasRelic(RelicType.EvenMaster) && cards != null)
            addMult += cards.Count(c => c.cardValue % 2 == 0) * 0.2f;
        // ── PerfectHand: no decayed cards → +10 flat after formula ───────────
        int flatBonus = 0;
        if (HasRelic(RelicType.PerfectHand) && cards != null && !cards.Any(c => c.isDecayed))
            flatBonus += 10;
        // ── GoldenCard: ×10 multiplicative ───────────────────────────────────
        if (HasRelic(RelicType.GoldenCard) && _goldenCardInPlay)
        {
            mulMult *= 10f;
            if (consumeOneShotEffects) _goldenCardInPlay = false;
        }
        // ── Store back for HUD display ────────────────────────────────────────
        result.additiveMult       = addMult;
        result.multiplicativeMult = mulMult;
        result.flatBonus = flatBonus;
        // ── FINAL: Points × +Mult × xMult × Difference xMult + flat ──────────
        int finalScore = Mathf.Max(0,
            Mathf.RoundToInt(result.points * addMult * mulMult * result.tierMult) + flatBonus);
        result.finalScore = finalScore;
        // Keep the in-world OFFER result message aligned with the four
        // distinct scoring ingredients shown in ScoreHUD.
        result.description =
            $"{result.comboName} | DIFF +{result.difference} | " +
            $"{result.points:N0} POINTS · +{addMult:0.##} +MULT · " +
            $"x{mulMult:0.##} xMULT · x{result.tierMult:0.##} DIFF xMULT";
        return finalScore;
    }
    // ── Rot threshold modifier (called from Card.InitCardStats) ───────────────
    public int GetRotThreshold(int baseThreshold)
    {
        // Future relics that buff rot threshold go here
        // e.g. if (HasRelic(RelicType.ThickBark)) return baseThreshold + 2;
        return Mathf.Max(1, baseThreshold);
    }
    public int GetExtraDrawCount(ComboResult result)
    {
        int extra = 0;
        if (HasRelic(RelicType.CardCycler) && result.scoringCards?.Count >= 3)
            extra += 1;
        if (HasRelic(RelicType.DrawReactor) && result.combo >= ComboType.Pair)
            extra += 1;
        return extra;
    }
    public int ModifyEnemyValue(int baseVal)
    {
        if (HasRelic(RelicType.GlassCannon)) return baseVal * 2;
        return baseVal;
    }
    public static Relic MakeRelicStatic(RelicType type) => type switch
    {
        RelicType.LoadedDice => new Relic(type, RelicCategory.ScalingMultiplier,
            "Loaded Dice", "Gain +3 Mult per successful shop reroll while owned, including free rerolls.",
            "Every second chance carries a weight."),
        RelicType.RerollToken => new Relic(type, RelicCategory.RuleChanger,
            "Reroll Token", "One free reroll per shop visit. Unused visit rerolls expire when you leave.",
            "Present this for another chance."),
        RelicType.CoinFlip => new Relic(type, RelicCategory.RuleChanger,
            "Coin Flip", "50% chance after each completed round (3 encounters) to earn a saved free reroll.",
            "Heads, you get another chance."),
        RelicType.SpadeSeal => new Relic(type, RelicCategory.ComboEngine,
            "Spade Seal", "Each Spade card gains +15 Points while this relic is owned.",
            "Steel remembers every cut."),
        RelicType.HeartSeal => new Relic(type, RelicCategory.ComboEngine,
            "Heart Seal", "Each Heart card gains +15 Points while this relic is owned.",
            "Even sacrifice has a pulse."),
        RelicType.DiamondSeal => new Relic(type, RelicCategory.ComboEngine,
            "Diamond Seal", "Each Diamond card gains +15 Points while this relic is owned.",
            "Harder than the cell walls."),
        RelicType.ClubSeal => new Relic(type, RelicCategory.ComboEngine,
            "Club Seal", "Each Club card gains +15 Points while this relic is owned.",
            "The mark of a survivor."),
        RelicType.SmallMult => new Relic(type, RelicCategory.ComboEngine,
            "Cracked Abacus", "Always gain +3 Mult.",
            "A little more than before."),
        RelicType.MediumMult => new Relic(type, RelicCategory.ComboEngine,
            "Brass Ledger", "Always gain +6 Mult.",
            "The figures never lie."),
        RelicType.LargeMult => new Relic(type, RelicCategory.ComboEngine,
            "Gilded Ledger", "Always gain +9 Mult.",
            "Every debt comes due."),
        RelicType.FourthHand      => new Relic(type, RelicCategory.RuleChanger,       "Fourth Hand",      "Play up to 4 cards per turn.",                                        "More hands, more suffering."),
        RelicType.DuelistsCode    => new Relic(type, RelicCategory.RuleChanger,       "Duelist's Code",   "Can offer only 1 card per hand. Gain x3 xMult on that card.",                        "One blow, decisive."),
        RelicType.PerfectGamble   => new Relic(type, RelicCategory.RuleChanger,       "Perfect Gamble",   "Once per round, discard your entire hand and redraw.",                "Burn it all. Start clean."),
        RelicType.ComboMachine    => new Relic(type, RelicCategory.RuleChanger,       "Combo Machine",    "Pairs score as Three of a Kind.",                                     "Two becomes three in purgatory."),
        RelicType.CardCycler      => new Relic(type, RelicCategory.RuleChanger,       "Card Cycler",      "Playing 3+ cards draws 1 additional card.",                           "Motion is survival."),
        RelicType.TacticalDelay   => new Relic(type, RelicCategory.RuleChanger,       "Tactical Delay",   "Skip a turn to draw 2 cards instead.",                                "Patience sharpens the blade."),
        RelicType.RotAlchemist    => new Relic(type, RelicCategory.DecayManipulation, "Rot Alchemist",    "Each decay stack on played cards adds +0.15 Mult.",           "Ruin is a resource."),
        RelicType.CorrosionEngine => new Relic(type, RelicCategory.DecayManipulation, "Corrosion Engine", "Decay stacks increase face value instead of decreasing it.",           "Corruption breeds power."),
        RelicType.SlowRot         => new Relic(type, RelicCategory.DecayManipulation, "Slow Rot",         "Cards only decay every other turn.",                                   "Time moves differently here."),
        RelicType.RotBurst        => new Relic(type, RelicCategory.DecayManipulation, "Rot Burst",        "When a card fully rots, gain +10 flat score.",                        "Death is an offering."),
        RelicType.Purifier        => new Relic(type, RelicCategory.DecayManipulation, "Purifier",         "After each turn, remove 1 decay from your most-decayed card.",        "Something resists the rot."),
        RelicType.NecromancyRelic => new Relic(type, RelicCategory.DecayManipulation, "Necromancy",       "Fully rotten cards gain +8 point value when scored.",                 "The dead serve willingly."),
        RelicType.PairAddict      => new Relic(type, RelicCategory.ComboEngine,       "Pair Addict",      "Playing a Pair or Two Pair triples the base +Mult.",                                "Symmetry is sacred."),
        RelicType.LowCardGenius   => new Relic(type, RelicCategory.ComboEngine,       "Low Card Genius",  "If all played cards are ≤4, double your +Mult.",         "Weakness weaponised."),
        RelicType.HighRoller      => new Relic(type, RelicCategory.ComboEngine,       "High Roller",      "Cards with face value ≥10 each contribute +5 bonus points.",          "Only royalty survives."),
        RelicType.OddCollector    => new Relic(type, RelicCategory.ComboEngine,       "Odd Collector",    "Each odd-valued card played adds +0.2 Mult.",                    "The odd ones hold the power."),
        RelicType.EvenMaster      => new Relic(type, RelicCategory.ComboEngine,       "Even Master",      "Each even-valued card played adds +0.2 Mult.",                   "Balance is strength."),
        RelicType.ComboBank       => new Relic(type, RelicCategory.ComboEngine,       "Combo Bank",       "Each unused combo type grows your xMult by 0.1 per round.",          "Patience compounds interest."),
        RelicType.LoneWolf        => new Relic(type, RelicCategory.ScalingMultiplier, "Lone Wolf",        "Each single-card play adds +0.2 to persistent xMult. No cap.", "Alone, unbowed."),
        RelicType.TurnInvestor    => new Relic(type, RelicCategory.ScalingMultiplier, "Turn Investor",    "Gain +2 Mult for every 5 gold held. No cap; updates when gold changes.",                     "Every breath is compounding interest."),
        RelicType.GrindCore       => new Relic(type, RelicCategory.ScalingMultiplier, "Grind Core",       "Win streaks add +0.1 to persistent xMult per consecutive win.",            "Momentum is everything."),
        RelicType.PerfectHand     => new Relic(type, RelicCategory.ScalingMultiplier, "Perfect Hand",     "No decayed cards played this hand = +10 flat score.",                 "Purity has its rewards."),
        RelicType.HighValueInvest => new Relic(type, RelicCategory.ScalingMultiplier, "High Value Fund",  "Each card ranked 10+ played adds +0.05 to persistent xMult.",       "Invest in excellence."),
        RelicType.TimeEngine      => new Relic(type, RelicCategory.ScalingMultiplier, "Time Engine",      "Persistent xMult grows +0.08 each round.",                               "Purgatory is patient."),
        RelicType.AnchorCard      => new Relic(type, RelicCategory.CardManipulation,  "Anchor Card",      "Your lowest-value card is immune to decay.",                           "One thing holds."),
        RelicType.GoldenCard      => new Relic(type, RelicCategory.CardManipulation,  "Golden Card",      "One random card each round grants x10 xMult if played.",     "Fortune favours the desperate."),
        RelicType.DrawReactor     => new Relic(type, RelicCategory.CardManipulation,  "Draw Reactor",     "Playing a Pair or better draws 1 extra card.",                        "Momentum feeds momentum."),
        RelicType.ValueLock       => new Relic(type, RelicCategory.CardManipulation,  "Value Lock",       "Your highest-value card cannot decay.",                                "The crown is protected."),
        RelicType.GlassCannon     => new Relic(type, RelicCategory.RiskChallenge,     "Glass Cannon",     "Enemy face value doubles each round — gain x3 xMult.",         "Everything to gain. Everything to lose."),
        RelicType.DoubleDecay     => new Relic(type, RelicCategory.RiskChallenge,     "Double Decay",     "Decay attacks deal double decay — gain x2 xMult.",            "Rot faster. Score harder."),
        _ => new Relic(type, RelicCategory.RuleChanger, "Unknown", "???")
    };
    public string GetSynergyHint(RelicType type)
    {
        bool has(RelicType t) => HasRelic(t);
        return type switch
        {
            RelicType.RotAlchemist    => has(RelicType.DoubleDecay)    ? "Synergy: Double Decay!" :
                                         has(RelicType.SlowRot)        ? "Synergy: Slow Rot steady income" : "",
            RelicType.CorrosionEngine => has(RelicType.RotAlchemist)   ? "Synergy: Rot Alchemist" : "",
            RelicType.PairAddict      => has(RelicType.ComboMachine)   ? "Synergy: Combo Machine pairs!" :
                                         has(RelicType.DrawReactor)    ? "Synergy: Draw on every pair" : "",
            RelicType.DrawReactor     => has(RelicType.PairAddict)     ? "Synergy: Pair Addict" : "",
            RelicType.LoneWolf        => has(RelicType.DuelistsCode)   ? "Synergy: Duelist stacks fast!" : "",
            RelicType.DuelistsCode    => has(RelicType.LoneWolf)       ? "Synergy: Lone Wolf" :
                                         has(RelicType.GoldenCard)     ? "Synergy: Golden Card x10!" : "",
            RelicType.GoldenCard      => has(RelicType.DuelistsCode)   ? "Synergy: Duelist triple!" : "",
            RelicType.GlassCannon     => has(RelicType.PerfectHand)    ? "Synergy: Keep cards clean!" :
                                         has(RelicType.LoneWolf)       ? "Synergy: Solo wolf slays" : "",
            RelicType.DoubleDecay     => has(RelicType.RotAlchemist)   ? "Synergy: Rot Alchemist" :
                                         has(RelicType.NecromancyRelic)? "Synergy: Necromancy scores rot" : "",
            RelicType.NecromancyRelic => has(RelicType.DoubleDecay)    ? "Synergy: Double Decay" : "",
            RelicType.ComboBank       => has(RelicType.TurnInvestor)   ? "Synergy: Gold-backed +Mult with scaling xMult" : "",
            RelicType.TimeEngine      => has(RelicType.TurnInvestor)   ? "Synergy: +Mult from gold, xMult from time" :
                                         has(RelicType.GrindCore)      ? "Synergy: Win streak + time" : "",
            RelicType.HighRoller      => has(RelicType.PairAddict)     ? "Note: Royals are low-pt cards" : "",
            RelicType.AnchorCard      => has(RelicType.ValueLock)      ? "Synergy: Double protection!" : "",
            RelicType.ValueLock       => has(RelicType.AnchorCard)     ? "Synergy: Anchor + Lock!" : "",
            _ => ""
        };
    }
}
