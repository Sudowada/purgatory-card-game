using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using DG.Tweening;

// ─────────────────────────────────────────────────────────────────────────────
// EVENT MANAGER
// Central brain. Pattern system fires event IDs → EventManager resolves them.
// Players never see event IDs — they see world reactions.
// ─────────────────────────────────────────────────────────────────────────────

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────
    private HashSet<string> _firedOnce   = new HashSet<string>(); // one-shot events
    private Dictionary<string, int> _fireCounts = new Dictionary<string, int>(); // repeat events
    private List<string>   _discoveryLog = new List<string>();    // what player has found

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Feedback")]
    [Tooltip("World-space canvas for event messages. Leave empty to use ShowMessage.")]
    [SerializeField] private TextMeshProUGUI eventMessageText;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip rareSoundEffect;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fire a list of event IDs (returned by CardPatternSystem.CheckPatterns).
    /// Call this immediately after resolving a hand.
    /// </summary>
    public void TriggerEvents(List<string> eventIDs, List<Card> playedCards)
    {
        foreach (var id in eventIDs)
            StartCoroutine(ResolveEvent(id, playedCards));
    }

    /// <summary>How many times a given event has fired this run.</summary>
    public int GetFireCount(string eventID) =>
        _fireCounts.TryGetValue(eventID, out int n) ? n : 0;

    /// <summary>Has this one-shot event already fired?</summary>
    public bool HasFired(string eventID) => _firedOnce.Contains(eventID);

    /// <summary>Full list of discovered events (for a discovery log UI).</summary>
    public IReadOnlyList<string> DiscoveryLog => _discoveryLog;

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT RESOLUTION
    // Add new event IDs here. Each case is a self-contained world reaction.
    // ─────────────────────────────────────────────────────────────────────────

    IEnumerator ResolveEvent(string id, List<Card> played)
    {
        // Track fire count
        _fireCounts.TryGetValue(id, out int count);
        _fireCounts[id] = count + 1;
        bool firstTime = count == 0;

        if (firstTime)
            _discoveryLog.Add(id);

        yield return new WaitForSeconds(0.4f); // let the score show first

        switch (id)
        {
            // ── SEQUENCE CODES ─────────────────────────────────────────────

            case "evt_unlock_document_01":
                if (!firstTime) yield break;
                ShowEventMessage("Something stirs in the dark...", rare: true);
                DocumentSystem.Instance?.Unlock("doc_01", "A torn page flutters down.\n\"The sequence was always 1-9-4-5.\"");
                break;

            case "evt_unlock_document_02":
                if (!firstTime) yield break;
                ShowEventMessage("A lock clicks open.", rare: true);
                DocumentSystem.Instance?.Unlock("doc_02", "Cell block 3137. Don't go there.");
                break;

            case "evt_meta_hint_01":
                if (!firstTime) yield break;
                ShowEventMessage("...this feels familiar.");
                DocumentSystem.Instance?.Unlock("doc_meta_01", "You knew this already, didn't you?");
                break;

            case "evt_lucky_trap":
                // Determine which of the three 777 outcomes applies
                // based on the decay state of the three 7s played
                bool allFresh    = played.All(c => c.decayLevel == 0);
                bool allFullyRot = played.All(c => c.isDecayed);
                // Mixed = any decay present but not all fully rotted

                if (allFresh)
                {
                    // ── JACKPOT: 3 fresh 7s ───────────────────────────────
                    ShowEventMessage("✦ JACKPOT ✦  Three clean sevens!", rare: true);
                    yield return new WaitForSeconds(0.6f);
                    ShowEventMessage("+50 gold. Fortune smiles.");
                    GameManager.Instance?.ModifyGold(50);
                    if (ShopManager.Instance != null)
                        ShopManager.Instance.rareChanceModifier += 0.25f;
                    CameraShake(0.5f);
                    if (firstTime)
                        DocumentSystem.Instance?.Unlock("doc_777_jackpot",
                            "Three sevens. All clean. The stars aligned.\nYou won't see this twice.");
                }
                else if (allFullyRot)
                {
                    // ── UNLUCKY 7: all 3 fully rotted ────────────────────
                    ShowEventMessage("Three rotten sevens...", rare: true);
                    yield return new WaitForSeconds(1.0f);
                    ShowEventMessage("The luck turns. -50 gold.");
                    GameManager.Instance?.ModifyGold(-50); // floors at 0
                    if (ShopManager.Instance != null)
                        ShopManager.Instance.rareChanceModifier -= 0.20f;
                    CameraShake(0.7f);
                    // Give the slot machine collectible (persists between runs)
                    ShopManager.Instance?.AddCollectible("collectible_slot_machine");
                    if (firstTime)
                        DocumentSystem.Instance?.Unlock("doc_777_unlucky",
                            "Three rotten sevens. The worst hand.\nSomething small and mechanical appeared on the table.\nIt wasn't there before.");
                    else
                        DocumentSystem.Instance?.Unlock($"doc_777_unlucky_{count}",
                            $"Again. The slot machine ticks once more.\n({count + 1} times now.)");
                }
                else
                {
                    // ── LUCKY 7s: mixed decay ────────────────────────────
                    ShowEventMessage("Lucky sevens — almost perfect.", rare: false);
                    yield return new WaitForSeconds(0.5f);
                    ShowEventMessage("+10 gold. Could be worse.");
                    GameManager.Instance?.ModifyGold(10);
                    if (ShopManager.Instance != null)
                        ShopManager.Instance.rareChanceModifier += 0.12f;
                    if (firstTime)
                        DocumentSystem.Instance?.Unlock("doc_777_lucky",
                            "Three sevens. Not all clean, not all rotten.\nThe luck is partial. Take it.");
                }
                break;

            case "evt_door_sound":
                ShowEventMessage("You hear a door.");
                AudioManager.Instance?.Play("door_click");
                break;

            case "evt_king_and_ace":
                ShowEventMessage("The king bows to the ace.");
                GameManager.Instance?.ModifyGold(2);
                break;

            case "evt_countdown":
                if (!firstTime) yield break;
                ShowEventMessage("3... 2... 1...", rare: true);
                DocumentSystem.Instance?.Unlock("doc_countdown", "Something is counting down.");
                break;

            case "evt_mirror":
                // Played same-different-same — duplicate the middle card next draw
                ShowEventMessage("The mirror reflects.");
                GameManager.Instance?.QueueNextCardValue(played.Count > 1 ? played[1].cardValue : 1);
                break;

            // ── DECAY / ROT COMBOS ─────────────────────────────────────────

            case "evt_rot_666":
                if (!firstTime) yield break;
                ShowEventMessage("Something wakes.", rare: true);
                CameraShake(0.4f);
                DocumentSystem.Instance?.Unlock("doc_rot_666", "Three sixes. Three choices. None of them yours.");
                break;

            case "evt_cursed_kings":
                ShowEventMessage("The dead kings curse you.");
                // Next round, 2 extra decay stacks
                GameManager.Instance?.QueueDecayPenalty(2);
                break;

            case "evt_full_rot_hand":
                if (!firstTime) yield break;
                ShowEventMessage("Everything rots eventually.", rare: true);
                DocumentSystem.Instance?.Unlock("doc_full_rot", "You held on too long.");
                GameManager.Instance?.ModifyGold(5); // reward for the risk
                break;

            case "evt_rotten_ace":
                ShowEventMessage("Even the highest falls.");
                // Rotten ace gives bonus score this hand
                GameManager.Instance?.AddBonusScore(14);
                break;

            case "evt_infection":
                ShowEventMessage("It spreads.");
                // Apply 1 extra decay to a random non-decayed hand card
                GameManager.Instance?.InfectRandomCard();
                break;

            case "evt_deaths_hand":
                if (!firstTime) yield break;
                ShowEventMessage("K... Q... J... The court is dead.", rare: true);
                CameraShake(0.6f);
                DocumentSystem.Instance?.Unlock("doc_deaths_hand", "The full court, rotten. No one is safe.");
                GameManager.Instance?.ModifyGold(8);
                break;

            // ── MATHEMATICAL PATTERNS ──────────────────────────────────────

            case "evt_blackjack":
                ShowEventMessage("Twenty-one! The dealer nods.");
                GameManager.Instance?.AddBonusScore(21);
                break;

            case "evt_perfect_ascend":
                ShowEventMessage("Rising.");
                GameManager.Instance?.AddBonusScore(10);
                break;

            case "evt_perfect_descend":
                ShowEventMessage("Falling.");
                // Slight penalty — descending is the wrong direction
                GameManager.Instance?.QueueDecayPenalty(1);
                break;

            case "evt_all_evens":
                ShowEventMessage("The machine hums.");
                if (firstTime)
                    DocumentSystem.Instance?.Unlock("doc_machine", "Even numbers. It accepts this.");
                GameManager.Instance?.AddBonusScore(8);
                break;

            case "evt_prime_hand":
                if (!firstTime) yield break;
                ShowEventMessage("A secret log entry appears.", rare: true);
                DocumentSystem.Instance?.Unlock("doc_primes", "2, 3, 5, 7. The first ones. The only ones that matter.");
                break;

            case "evt_bakers_dozen":
                ShowEventMessage("Thirteen. An old number.");
                GameManager.Instance?.ModifyGold(1);
                break;

            case "evt_zero_sum":
                ShowEventMessage("Nothing gained. Nothing lost.");
                // Zero sum — no effect, but unlock a document if first time
                if (firstTime)
                    DocumentSystem.Instance?.Unlock("doc_zero_sum", "You played nothing and got nothing. And yet.");
                break;

            case "evt_fibonacci":
                if (!firstTime) yield break;
                ShowEventMessage("The sequence continues...", rare: true);
                DocumentSystem.Instance?.Unlock("doc_fibonacci", "1, 1, 2, 3, 5. Where does it end?");
                GameManager.Instance?.AddBonusScore(20);
                break;

            // ── SUIT PATTERNS ──────────────────────────────────────────────

            case "evt_full_suit":
                string suit = played.FirstOrDefault()?.cardSuit ?? "?";
                ShowEventMessage($"All {suit}. The suit is pleased.");
                GameManager.Instance?.AddBonusScore(12);
                break;

            case "evt_rotten_flush":
                ShowEventMessage("A rotten flush. Impressive.");
                GameManager.Instance?.AddBonusScore(15);
                GameManager.Instance?.ModifyGold(2);
                break;

            // ── BREAKING THE RULES ─────────────────────────────────────────

            case "evt_triple_duplicate":
                ShowEventMessage("Three of a kind. The pattern repeats.");
                // Next draw gets a copy of the played value
                GameManager.Instance?.QueueNextCardValue(played[0].cardValue);
                break;

            case "evt_overkill":
                ShowEventMessage("Too much.");
                if (firstTime)
                    DocumentSystem.Instance?.Unlock("doc_overkill", "You didn't need to do that.");
                CameraShake(0.3f);
                break;

            case "evt_royal_court":
                ShowEventMessage("The full court assembled.");
                GameManager.Instance?.AddBonusScore(25);
                if (firstTime)
                    DocumentSystem.Instance?.Unlock("doc_royal_court", "Jack, Queen, King. But where is the Ace?");
                break;

            case "evt_peasants_hand":
                ShowEventMessage("Three twos. Humble.");
                // Small gold reward — the lowest combo still counts
                GameManager.Instance?.ModifyGold(1);
                break;

            // ── RELIC-GATED ────────────────────────────────────────────────

            case "evt_paradox":
                if (!firstTime) yield break;
                ShowEventMessage("The highest and lowest together. A paradox.", rare: true);
                DocumentSystem.Instance?.Unlock("doc_paradox", "The ace outranks the king. The rotten outranks the fresh.");
                GameManager.Instance?.AddBonusScore(30);
                break;

            case "evt_jackpot":
                ShowEventMessage("JACKPOT!");
                GameManager.Instance?.ModifyGold(10);
                CameraShake(0.5f);
                break;

            case "evt_plague_bloom":
                if (!firstTime) yield break;
                ShowEventMessage("The plague blooms.", rare: true);
                DocumentSystem.Instance?.Unlock("doc_plague", "You let it spread. Now look at it.");
                GameManager.Instance?.AddBonusScore(40);
                break;

            // ── LATE GAME ──────────────────────────────────────────────────

            case "evt_the_offering":
                if (!firstTime) yield break;
                ShowEventMessage("An offering accepted.", rare: true);
                CameraShake(0.4f);
                DocumentSystem.Instance?.Unlock("doc_offering", "Ace, Jack, Queen, King. They took it.");
                GameManager.Instance?.ModifyGold(15);
                break;

            case "evt_endgame":
                if (!firstTime) yield break;
                ShowEventMessage("The end approaches.", rare: true);
                CameraShake(0.8f);
                DocumentSystem.Instance?.Unlock("doc_endgame", "Three tens. You knew this was coming.");
                break;

            // ── PERSISTENT META PUZZLES ───────────────────────────────────────

            case "evt_ascent_gold":
                // A, 2, 3 played — +1 permanent starting gold
                if (!firstTime)
                {
                    ShowEventMessage("You already know this sequence.");
                    yield break;
                }
                ShowEventMessage("A... 2... 3. The beginning.", rare: true);
                yield return new WaitForSeconds(0.8f);
                ShowEventMessage("Next run starts with 1 more gold. Always.");
                if (ShopManager.Instance != null)
                    ShopManager.Instance.bonusStartGold += 1;
                DocumentSystem.Instance?.Unlock("doc_ascent",
                    "Ace. Two. Three.\nThe simplest sequence.\nSomething has shifted. You'll notice at the start.");
                break;

            case "evt_rot_sequence_gold":
                // Decay levels 1, 2, 3 in order — +1 permanent starting gold
                if (!firstTime)
                {
                    ShowEventMessage("The rot remembers.");
                    yield break;
                }
                ShowEventMessage("One stack... two... three. In order.", rare: true);
                yield return new WaitForSeconds(0.8f);
                ShowEventMessage("The rot teaches. Next run starts with 1 more gold.");
                if (ShopManager.Instance != null)
                    ShopManager.Instance.bonusStartGold += 1;
                DocumentSystem.Instance?.Unlock("doc_rot_sequence",
                    "You let them decay \u2014 precisely.\nOne stack. Two stacks. Three stacks. In that order.\nSomething rewards patience.");
                break;

            default:
                Debug.Log($"[EventManager] Unhandled event: {id}");
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FEEDBACK HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    void ShowEventMessage(string msg, bool rare = false)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowMessage(msg);

        if (rare && rareSoundEffect != null)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = rareSoundEffect;
            src.Play();
            Destroy(src, rareSoundEffect.length + 0.1f);
        }
    }

    void CameraShake(float strength)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.transform.DOShakePosition(0.4f, strength, 15, 90, false, true);
    }
}