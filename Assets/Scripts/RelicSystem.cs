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
}
 
[System.Serializable]
public class Relic
{
    public RelicType     type;
    public RelicCategory category;
    public string        displayName;
    public string        description;
    public string        flavorText;
 
    public Relic(RelicType t, RelicCategory cat, string name, string desc, string flavor = "")
    {
        type = t; category = cat; displayName = name; description = desc; flavorText = flavor;
    }
}
 
public class RelicSystem : MonoBehaviour
{
    public static RelicSystem Instance { get; private set; }
 
    public List<Relic> activeRelics = new List<Relic>();
 
    private int  _maxPlay             = 3;
    private int  _decayCount          = 3;
    private int  _minimumBase         = 0;
    private bool _decayEveryOtherTurn = false;
    private int  _decayTurnCounter    = 0;
    private bool _decayRotOnStack     = false;
    private bool _decayAll            = false;
 
    public float persistentMult = 1.0f;
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
        Recalculate();
    }
 
    public bool HasRelic(RelicType type) => activeRelics.Exists(r => r.type == type);
 
    private void OnRelicAdded(RelicType type)
    {
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
            persistentMult += 0.08f * (roundNumber - 1);
 
        if (HasRelic(RelicType.ComboBank))
        {
            int unusedCount = System.Enum.GetValues(typeof(ComboType)).Length - _combosSeenThisRun.Count;
            _comboBankBonus += unusedCount * 0.1f;
        }
    }
 
    public void OnHandPlayed(ComboResult result, bool playerWon)
    {
        _turnsSurvived++;
 
        if (HasRelic(RelicType.TurnInvestor))
            persistentMult += 0.05f;
 
        if (HasRelic(RelicType.LoneWolf) && result.scoringCards?.Count == 1)
            persistentMult += 0.1f;
 
        if (HasRelic(RelicType.HighValueInvest) && result.scoringCards != null)
            foreach (var c in result.scoringCards)
                if (c.cardValue >= 10) persistentMult += 0.05f;
 
        if (playerWon)
        {
            _winStreak++;
            if (HasRelic(RelicType.GrindCore))
                persistentMult += 0.1f * _winStreak;
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
 
    public int ApplyRelicModifiers(ComboResult result)
    {
        // Penalty hands — relics don't reduce penalty except GlassCannon doubles it
        if (result.isPenalty)
        {
            int penalty = result.penaltyAmount;
            if (HasRelic(RelicType.GlassCannon)) penalty = Mathf.RoundToInt(penalty * 2f);
            return -penalty;
        }
 
        var   cards   = result.scoringCards;
        float addMult = result.additiveMult;   // starts at 1 + comboBonus
        float mulMult = 1f;                    // relic multiplicative stack
 
        // ── Persistent multiplier ─────────────────────────────────────────────
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
            _goldenCardInPlay = false;
        }
 
        // ── Store back for HUD display ────────────────────────────────────────
        result.additiveMult       = addMult;
        result.multiplicativeMult = mulMult;
 
        // ── FINAL: points × addMult × mulMult × tierMult + flat ──────────────
        int finalScore = Mathf.Max(0,
            Mathf.RoundToInt(result.points * addMult * mulMult * result.tierMult) + flatBonus);
 
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
        RelicType.FourthHand      => new Relic(type, RelicCategory.RuleChanger,       "Fourth Hand",      "Play up to 4 cards per turn.",                                        "\"More hands, more suffering.\""),
        RelicType.DuelistsCode    => new Relic(type, RelicCategory.RuleChanger,       "Duelist's Code",   "Play only 1 card — but its score is tripled.",                        "\"One blow, decisive.\""),
        RelicType.PerfectGamble   => new Relic(type, RelicCategory.RuleChanger,       "Perfect Gamble",   "Once per round, discard your entire hand and redraw.",                "\"Burn it all. Start clean.\""),
        RelicType.ComboMachine    => new Relic(type, RelicCategory.RuleChanger,       "Combo Machine",    "Pairs score as Three of a Kind.",                                     "\"Two becomes three in purgatory.\""),
        RelicType.CardCycler      => new Relic(type, RelicCategory.RuleChanger,       "Card Cycler",      "Playing 3+ cards draws 1 additional card.",                           "\"Motion is survival.\""),
        RelicType.TacticalDelay   => new Relic(type, RelicCategory.RuleChanger,       "Tactical Delay",   "Skip a turn to draw 2 cards instead.",                                "\"Patience sharpens the blade.\""),
        RelicType.RotAlchemist    => new Relic(type, RelicCategory.DecayManipulation, "Rot Alchemist",    "Each total decay stack in play adds +15% to additive mult.",           "\"Ruin is a resource.\""),
        RelicType.CorrosionEngine => new Relic(type, RelicCategory.DecayManipulation, "Corrosion Engine", "Decay stacks increase face value instead of decreasing it.",           "\"Corruption breeds power.\""),
        RelicType.SlowRot         => new Relic(type, RelicCategory.DecayManipulation, "Slow Rot",         "Cards only decay every other turn.",                                   "\"Time moves differently here.\""),
        RelicType.RotBurst        => new Relic(type, RelicCategory.DecayManipulation, "Rot Burst",        "When a card fully rots, gain +10 flat score.",                        "\"Death is an offering.\""),
        RelicType.Purifier        => new Relic(type, RelicCategory.DecayManipulation, "Purifier",         "After each turn, remove 1 decay from your most-decayed card.",        "\"Something resists the rot.\""),
        RelicType.NecromancyRelic => new Relic(type, RelicCategory.DecayManipulation, "Necromancy",       "Fully rotten cards gain +8 point value when scored.",                 "\"The dead serve willingly.\""),
        RelicType.PairAddict      => new Relic(type, RelicCategory.ComboEngine,       "Pair Addict",      "Pairs triple the additive multiplier.",                                "\"Symmetry is sacred.\""),
        RelicType.LowCardGenius   => new Relic(type, RelicCategory.ComboEngine,       "Low Card Genius",  "If all played cards are ≤4, double the additive multiplier.",         "\"Weakness weaponised.\""),
        RelicType.HighRoller      => new Relic(type, RelicCategory.ComboEngine,       "High Roller",      "Cards with face value ≥10 each contribute +5 bonus points.",          "\"Only royalty survives.\""),
        RelicType.OddCollector    => new Relic(type, RelicCategory.ComboEngine,       "Odd Collector",    "Each odd-valued card adds +20% to additive mult.",                    "\"The odd ones hold the power.\""),
        RelicType.EvenMaster      => new Relic(type, RelicCategory.ComboEngine,       "Even Master",      "Each even-valued card adds +20% to additive mult.",                   "\"Balance is strength.\""),
        RelicType.ComboBank       => new Relic(type, RelicCategory.ComboEngine,       "Combo Bank",       "Each unused combo type adds ×10% multiplicative per round.",          "\"Patience compounds interest.\""),
        RelicType.LoneWolf        => new Relic(type, RelicCategory.ScalingMultiplier, "Lone Wolf",        "Playing a single card permanently increases persistent mult by +10%.", "\"Alone, unbowed.\""),
        RelicType.TurnInvestor    => new Relic(type, RelicCategory.ScalingMultiplier, "Turn Investor",    "Each turn survived adds +5% to persistent mult.",                     "\"Every breath is compounding interest.\""),
        RelicType.GrindCore       => new Relic(type, RelicCategory.ScalingMultiplier, "Grind Core",       "Win streaks add +10% persistent mult per consecutive win.",            "\"Momentum is everything.\""),
        RelicType.PerfectHand     => new Relic(type, RelicCategory.ScalingMultiplier, "Perfect Hand",     "No decayed cards played this hand = +10 flat score.",                 "\"Purity has its rewards.\""),
        RelicType.HighValueInvest => new Relic(type, RelicCategory.ScalingMultiplier, "High Value Fund",  "Each card ≥10 played permanently adds +5% to persistent mult.",       "\"Invest in excellence.\""),
        RelicType.TimeEngine      => new Relic(type, RelicCategory.ScalingMultiplier, "Time Engine",      "Persistent mult grows +8% each round.",                               "\"Purgatory is patient.\""),
        RelicType.AnchorCard      => new Relic(type, RelicCategory.CardManipulation,  "Anchor Card",      "Your lowest-value card is immune to decay.",                           "\"One thing holds.\""),
        RelicType.GoldenCard      => new Relic(type, RelicCategory.CardManipulation,  "Golden Card",      "One random card each round scores ×10 multiplicative if played.",     "\"Fortune favours the desperate.\""),
        RelicType.DrawReactor     => new Relic(type, RelicCategory.CardManipulation,  "Draw Reactor",     "Playing a Pair or better draws 1 extra card.",                        "\"Momentum feeds momentum.\""),
        RelicType.ValueLock       => new Relic(type, RelicCategory.CardManipulation,  "Value Lock",       "Your highest-value card cannot decay.",                                "\"The crown is protected.\""),
        RelicType.GlassCannon     => new Relic(type, RelicCategory.RiskChallenge,     "Glass Cannon",     "Enemy face value doubles — your multiplicative mult triples.",         "\"Everything to gain. Everything to lose.\""),
        RelicType.DoubleDecay     => new Relic(type, RelicCategory.RiskChallenge,     "Double Decay",     "Cards decay twice per turn — multiplicative mult doubled.",            "\"Rot faster. Score harder.\""),
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
            RelicType.ComboBank       => has(RelicType.TurnInvestor)   ? "Synergy: Patience pays double" : "",
            RelicType.TimeEngine      => has(RelicType.TurnInvestor)   ? "Synergy: Turn Investor stack!" :
                                         has(RelicType.GrindCore)      ? "Synergy: Win streak + time" : "",
            RelicType.HighRoller      => has(RelicType.PairAddict)     ? "Note: Royals are low-pt cards" : "",
            RelicType.AnchorCard      => has(RelicType.ValueLock)      ? "Synergy: Double protection!" : "",
            RelicType.ValueLock       => has(RelicType.AnchorCard)     ? "Synergy: Anchor + Lock!" : "",
            _ => ""
        };
    }
}
 