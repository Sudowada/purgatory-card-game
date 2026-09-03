using UnityEngine;

[CreateAssetMenu(menuName = "Journal/Relic", fileName = "Relic_")]
public class RelicData : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique ID referenced by NoteData.relicID and GameEvents. Format: relic_name e.g. relic_tooth_box")]
    public string relicID;

    [Tooltip("Which prisoner this relic belongs to or is most associated with.")]
    public PrisonerAuthor associatedAuthor;

    // ── Base state ────────────────────────────────────────────────────────────

    [Header("Base State")]
    [Tooltip("Display name in base state. e.g. Starving Rat")]
    public string displayName;

    [Tooltip("Short description in base state. 1-2 sentences.")]
    [TextArea(3, 6)]
    public string description;

    [Tooltip("Longer examination text in base state.")]
    [TextArea(6, 15)]
    public string examinationText;

    [Tooltip("Sprite in base state.")]
    public Sprite relicSprite;

    [Tooltip("Optional close-up sprite in base state.")]
    public Sprite examinationSprite;

    [Tooltip("Buffs or modifiers in base state. Plain text for now — wire to your buff system later.")]
    [TextArea(2, 5)]
    public string baseBuffDescription;

    // ── Upgraded state ────────────────────────────────────────────────────────
    // Leave all upgraded fields empty if this relic cannot be upgraded.

    [Header("Upgraded State — leave empty if relic has no upgrade")]
    [Tooltip("Display name after upgrade. e.g. Full Rat")]
    public string upgradedDisplayName;

    [Tooltip("Short description after upgrade.")]
    [TextArea(3, 6)]
    public string upgradedDescription;

    [Tooltip("Longer examination text after upgrade.")]
    [TextArea(6, 15)]
    public string upgradedExaminationText;

    [Tooltip("Sprite after upgrade. Leave empty to reuse base sprite.")]
    public Sprite upgradedRelicSprite;

    [Tooltip("Optional close-up sprite after upgrade.")]
    public Sprite upgradedExaminationSprite;

    [Tooltip("Buffs or modifiers after upgrade.")]
    [TextArea(2, 5)]
    public string upgradedBuffDescription;

    [Tooltip("The GameEvent ID that triggers the upgrade. e.g. relic_rat_upgraded. " +
             "Leave empty if this relic cannot be upgraded.")]
    public string upgradeEventID;

    [Tooltip("If set, unlocks this note when the relic is upgraded.")]
    public string upgradeUnlocksNoteID;

    // ── Convenience properties — use these in UI code instead of checking state manually ──

    /// <summary>Returns the correct display name for the current upgrade state.</summary>
    public string CurrentDisplayName =>
        upgradeState == RelicUpgradeState.Upgraded && !string.IsNullOrEmpty(upgradedDisplayName)
            ? upgradedDisplayName : displayName;

    /// <summary>Returns the correct description for the current upgrade state.</summary>
    public string CurrentDescription =>
        upgradeState == RelicUpgradeState.Upgraded && !string.IsNullOrEmpty(upgradedDescription)
            ? upgradedDescription : description;

    /// <summary>Returns the correct examination text for the current upgrade state.</summary>
    public string CurrentExaminationText =>
        upgradeState == RelicUpgradeState.Upgraded && !string.IsNullOrEmpty(upgradedExaminationText)
            ? upgradedExaminationText : examinationText;

    /// <summary>Returns the correct sprite for the current upgrade state.</summary>
    public Sprite CurrentSprite =>
        upgradeState == RelicUpgradeState.Upgraded && upgradedRelicSprite != null
            ? upgradedRelicSprite : relicSprite;

    /// <summary>Returns the correct examination sprite for the current upgrade state.</summary>
    public Sprite CurrentExaminationSprite =>
        upgradeState == RelicUpgradeState.Upgraded && upgradedExaminationSprite != null
            ? upgradedExaminationSprite : examinationSprite ?? relicSprite;

    /// <summary>Returns the correct buff description for the current upgrade state.</summary>
    public string CurrentBuffDescription =>
        upgradeState == RelicUpgradeState.Upgraded && !string.IsNullOrEmpty(upgradedBuffDescription)
            ? upgradedBuffDescription : baseBuffDescription;

    /// <summary>Returns true if this relic supports an upgrade.</summary>
    public bool CanBeUpgraded => !string.IsNullOrEmpty(upgradeEventID);

    // ── Physical state ────────────────────────────────────────────────────────

    [Header("Physical State")]
    [Tooltip("Intact, Damaged, Partial, or Unknown.")]
    public RelicCondition condition;

    [Tooltip("Optional flavour text describing the physical condition.")]
    [TextArea(2, 4)]
    public string conditionNote;

    // ── Relationships ─────────────────────────────────────────────────────────

    [Header("Relationships")]
    [Tooltip("IDs of notes that reference or are unlocked by this relic.")]
    public string[] relatedNoteIDs;

    [Tooltip("If finding this relic should unlock a note immediately, put that note's ID here.")]
    public string unlocksNoteID;

    // ── Unlock ────────────────────────────────────────────────────────────────

    [Header("Unlock")]
    [Tooltip("If true, this relic is visible from the start of the game.")]
    public bool unlockedByDefault;

    [Tooltip("The GameEvents ID that discovers this relic. e.g. found_tooth_box")]
    public string unlockEventID;

    // ── Runtime state — managed by JournalManager, do not edit manually ───────

    [Header("Runtime State — do not edit manually")]
    [HideInInspector] public bool isUnlocked;
    [HideInInspector] public bool hasBeenExamined;
    [HideInInspector] public RelicUpgradeState upgradeState = RelicUpgradeState.Base;

    // ── Editor helper ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(relicID))
        {
            relicID = name.ToLower().Replace(" ", "_");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (!unlockedByDefault && string.IsNullOrEmpty(unlockEventID))
        {
            Debug.LogWarning($"[RelicData] '{relicID}' has no unlockEventID and unlockedByDefault is false. " +
                             $"This relic will never appear. Set one or the other.", this);
        }

        if (string.IsNullOrEmpty(displayName))
        {
            Debug.LogWarning($"[RelicData] '{relicID}' has no displayName. " +
                             $"It will appear blank in the journal.", this);
        }

        if (!string.IsNullOrEmpty(upgradeEventID) && string.IsNullOrEmpty(upgradedDisplayName))
        {
            Debug.LogWarning($"[RelicData] '{relicID}' has an upgradeEventID but no upgradedDisplayName. " +
                             $"The journal will show a blank name after upgrade.", this);
        }
    }
#endif
}