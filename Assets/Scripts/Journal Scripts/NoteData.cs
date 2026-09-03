using UnityEngine;

[CreateAssetMenu(menuName = "Journal/Note", fileName = "Note_")]
public class NoteData : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique ID used by JournalManager and GameEvents. Format: author_dayXX e.g. bram_day05")]
    public string noteID;

    [Tooltip("Which prisoner wrote this note.")]
    public PrisonerAuthor author;

    [Tooltip("Note, Relic, or Puzzle — used for home screen counters and filtering.")]
    public NoteCategory category;

    [Tooltip("The in-world day number. Used for sorting and display.")]
    public int day;

    // ── Content ───────────────────────────────────────────────────────────────

    [Header("Content")]
    [Tooltip("The full text shown in the reading pane. Write exactly as it should appear, including line breaks.")]
    [TextArea(10, 30)]
    public string noteText;

    [Tooltip("Optional ink sketch shown below the note text. Leave empty if this note has no sketch.")]
    public Sprite sketchImage;

    // ── Mira-specific ─────────────────────────────────────────────────────────

    [Header("Mira Only — ignored for all other authors")]
    [Tooltip("Controls which handwriting font is used. Early = neat, Mid = unstable, Late = near-illegible.")]
    public MiraStage miraStage;

    // ── Relationships ─────────────────────────────────────────────────────────

    [Header("Relationships")]
    [Tooltip("IDs of notes shown in the Related Notes sidebar. e.g. { lira_day66, bram_day19 }")]
    public string[] relatedNoteIDs;

    [Tooltip("If this note references a relic, put its ID here. Shows the relic chip in the reading pane.")]
    public string relicID;

    // ── Unlock ────────────────────────────────────────────────────────────────

    [Header("Unlock")]
    [Tooltip("If true, this note is available from the very start of the game. Use for Lira's early helpful notes.")]
    public bool unlockedByDefault;

    [Tooltip("The GameEvents ID that unlocks this note. e.g. found_tooth_box. Leave empty if unlockedByDefault is true.")]
    public string unlockEventID;

    // ── Runtime state — managed by JournalManager, do not edit manually ───────

    [Header("Runtime State — do not edit manually")]
    [HideInInspector] public bool isUnlocked;
    [HideInInspector] public bool hasBeenRead;

    // ── Editor helper ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-format the noteID from the asset filename if left blank
        if (string.IsNullOrEmpty(noteID))
        {
            noteID = name.ToLower().Replace(" ", "_");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Warn if an unlock event ID is missing on a non-default note
        if (!unlockedByDefault && string.IsNullOrEmpty(unlockEventID))
        {
            Debug.LogWarning($"[NoteData] '{noteID}' has no unlockEventID and unlockedByDefault is false. " +
                             $"This note will never unlock. Set one or the other.", this);
        }

        // Warn if miraStage is set on a non-Mira note (easy mistake to leave at default)
        if (author != PrisonerAuthor.Mira && miraStage != MiraStage.Early)
        {
            Debug.LogWarning($"[NoteData] '{noteID}' has a miraStage set but author is {author}. " +
                             $"miraStage is ignored for non-Mira notes.", this);
        }
    }
#endif
}
