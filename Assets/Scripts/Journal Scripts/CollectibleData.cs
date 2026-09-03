using UnityEngine;

/// <summary>
/// ScriptableObject for a physical collectible the player finds in the cell.
///
/// Collectibles live on a physical shelf in the cell.
/// They reset to their world position every run but their visual/functional
/// STATE persists permanently across runs.
///
/// Example — The Big Cheese:
///   Default state:     Cheese in acrylic casing
///   StateA:            Casing melted — cheese accessible
///   StateB:            Cheese partially eaten (after feeding rat)
///
/// Interactions:
///   A collectible can have an optional interaction that requires a specific
///   relic to be in the player's inventory. When triggered, it fires a GameEvent,
///   changes the collectible's persistent state, upgrades the relic, and
///   unlocks new journal lore.
/// </summary>
[CreateAssetMenu(menuName = "Journal/Collectible", fileName = "Collectible_")]
public class CollectibleData : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique ID. Format: collectible_name e.g. collectible_big_cheese")]
    public string collectibleID;

    [Tooltip("Display name shown in the journal and on the shelf tooltip.")]
    public string displayName;

    // ── State definitions ─────────────────────────────────────────────────────
    // Each state has its own sprite, lore blurb, and shelf description.
    // States after Default are only shown once unlocked via interaction.

    [Header("Default State")]
    [Tooltip("The collectible as first found. No interactions yet.")]
    public Sprite defaultSprite;

    [Tooltip("Short shelf label shown when hovering in the cell. 1 sentence max.")]
    [TextArea(1, 3)]
    public string defaultShelfLabel;

    [Tooltip("Lore blurb shown in the journal for this state.")]
    [TextArea(4, 10)]
    public string defaultLoreBlurb;

    [Header("State A — first permanent change")]
    [Tooltip("Sprite after first state change. e.g. Cheese out of casing.")]
    public Sprite stateASprite;

    [Tooltip("Shelf label in State A.")]
    [TextArea(1, 3)]
    public string stateAShelfLabel;

    [Tooltip("New lore blurb unlocked when State A is reached.")]
    [TextArea(4, 10)]
    public string stateALoreBlurb;

    [Tooltip("Note ID to unlock when State A is reached. Leave empty if none.")]
    public string stateAUnlocksNoteID;

    [Header("State B — second permanent change (optional)")]
    [Tooltip("Sprite after second state change. e.g. Cheese partially eaten.")]
    public Sprite stateBSprite;

    [Tooltip("Shelf label in State B.")]
    [TextArea(1, 3)]
    public string stateBShelfLabel;

    [Tooltip("New lore blurb unlocked when State B is reached.")]
    [TextArea(4, 10)]
    public string stateBLoreBlurb;

    [Tooltip("Note ID to unlock when State B is reached. Leave empty if none.")]
    public string stateBUnlocksNoteID;

    [Header("State C — third permanent change (optional)")]
    public Sprite stateCSprite;

    [TextArea(1, 3)]
    public string stateCShelfLabel;

    [TextArea(4, 10)]
    public string stateCLoreBlurb;

    public string stateCUnlocksNoteID;

    // ── Interaction ───────────────────────────────────────────────────────────

    [Header("Interaction")]
    [Tooltip("If set, the player must have this relic in their inventory to trigger the interaction. " +
             "e.g. relic_rat — player must be carrying the rat to use it on the cheese.")]
    public string requiredRelicID;

    [Tooltip("Which state this interaction transitions the collectible TO. " +
             "e.g. if the player uses the rat on the cheese in Default state, it moves to StateB.")]
    public CollectibleState interactionResultState;

    [Tooltip("The GameEvent fired when the interaction succeeds. " +
             "e.g. collectible_cheese_fed_to_rat — other systems listen to this.")]
    public string interactionEventID;

    [Tooltip("If the interaction also upgrades the required relic, set that upgrade event here. " +
             "e.g. relic_rat_upgraded — JournalManager will upgrade the relic automatically.")]
    public string interactionUpgradesRelicEventID;

    [Tooltip("Flavour text shown to the player when the interaction succeeds. " +
             "e.g. The rat devours the cheese greedily.")]
    [TextArea(2, 4)]
    public string interactionSuccessText;

    [Tooltip("Flavour text shown when the player tries the interaction but requirements aren't met.")]
    [TextArea(2, 4)]
    public string interactionFailText;

    // ── Unlock ────────────────────────────────────────────────────────────────

    [Header("Unlock")]
    [Tooltip("The GameEvent ID fired when the player picks this up in the world. " +
             "e.g. picked_up_big_cheese. Resets each run — picking it up re-fires every run.")]
    public string pickupEventID;

    [Tooltip("If true, this collectible appears in the journal from the start " +
             "even before the player picks it up. Use for items the player can see but not yet reach.")]
    public bool visibleInJournalByDefault;

    // ── Convenience properties ────────────────────────────────────────────────

    /// <summary>Returns the correct sprite for the current persistent state.</summary>
    public Sprite CurrentSprite
    {
        get
        {
            return currentState switch
            {
                CollectibleState.StateA => stateASprite != null ? stateASprite : defaultSprite,
                CollectibleState.StateB => stateBSprite != null ? stateBSprite : stateASprite ?? defaultSprite,
                CollectibleState.StateC => stateCSprite != null ? stateCSprite : stateBSprite ?? defaultSprite,
                CollectibleState.Destroyed => defaultSprite,
                _ => defaultSprite
            };
        }
    }

    /// <summary>Returns the correct shelf label for the current persistent state.</summary>
    public string CurrentShelfLabel
    {
        get
        {
            return currentState switch
            {
                CollectibleState.StateA => !string.IsNullOrEmpty(stateAShelfLabel) ? stateAShelfLabel : defaultShelfLabel,
                CollectibleState.StateB => !string.IsNullOrEmpty(stateBShelfLabel) ? stateBShelfLabel : defaultShelfLabel,
                CollectibleState.StateC => !string.IsNullOrEmpty(stateCShelfLabel) ? stateCShelfLabel : defaultShelfLabel,
                _ => defaultShelfLabel
            };
        }
    }

    /// <summary>Returns the correct lore blurb for the current persistent state.</summary>
    public string CurrentLoreBlurb
    {
        get
        {
            return currentState switch
            {
                CollectibleState.StateA => !string.IsNullOrEmpty(stateALoreBlurb) ? stateALoreBlurb : defaultLoreBlurb,
                CollectibleState.StateB => !string.IsNullOrEmpty(stateBLoreBlurb) ? stateBLoreBlurb : defaultLoreBlurb,
                CollectibleState.StateC => !string.IsNullOrEmpty(stateCLoreBlurb) ? stateCLoreBlurb : defaultLoreBlurb,
                _ => defaultLoreBlurb
            };
        }
    }

    /// <summary>Returns the note ID to unlock for the current state transition, or empty.</summary>
    public string GetUnlocksNoteIDForState(CollectibleState state)
    {
        return state switch
        {
            CollectibleState.StateA => stateAUnlocksNoteID,
            CollectibleState.StateB => stateBUnlocksNoteID,
            CollectibleState.StateC => stateCUnlocksNoteID,
            _ => string.Empty
        };
    }

    /// <summary>Returns true if this collectible has a relic interaction.</summary>
    public bool HasRelicInteraction => !string.IsNullOrEmpty(requiredRelicID);

    // ── Runtime state — managed by JournalManager, do not edit manually ───────

    [Header("Runtime State — do not edit manually")]
    [HideInInspector] public CollectibleState currentState = CollectibleState.Default;
    [HideInInspector] public bool isPickedUpThisRun;
    [HideInInspector] public bool hasBeenPickedUpEver;
    [HideInInspector] public bool hasBeenViewedInJournal;

    // ── Editor helper ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(collectibleID))
        {
            collectibleID = name.ToLower().Replace(" ", "_");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (string.IsNullOrEmpty(displayName))
            Debug.LogWarning($"[CollectibleData] '{collectibleID}' has no displayName.", this);

        if (defaultSprite == null)
            Debug.LogWarning($"[CollectibleData] '{collectibleID}' has no defaultSprite.", this);

        if (string.IsNullOrEmpty(defaultLoreBlurb))
            Debug.LogWarning($"[CollectibleData] '{collectibleID}' has no defaultLoreBlurb. " +
                             $"Every collectible should deepen the lore.", this);

        if (string.IsNullOrEmpty(pickupEventID))
            Debug.LogWarning($"[CollectibleData] '{collectibleID}' has no pickupEventID. " +
                             $"The journal won't know when the player finds it.", this);

        if (HasRelicInteraction && string.IsNullOrEmpty(interactionEventID))
            Debug.LogWarning($"[CollectibleData] '{collectibleID}' has a requiredRelicID but no interactionEventID. " +
                             $"The interaction will fire nothing when triggered.", this);
    }
#endif
}
