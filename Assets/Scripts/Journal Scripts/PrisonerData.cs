using UnityEngine;

[CreateAssetMenu(menuName = "Journal/Prisoner", fileName = "Prisoner_")]
public class PrisonerData : ScriptableObject
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique ID used by JournalManager. Format: prisoner_name e.g. prisoner_bram")]
    public string prisonerID;

    [Tooltip("Display name shown in the journal. e.g. Bram")]
    public string displayName;

    [Tooltip("The enum value that links this asset to NoteData.author and filtering.")]
    public PrisonerAuthor author;

    // ── Profile content ───────────────────────────────────────────────────────

    [Header("Profile Content")]
    [Tooltip("Occupation shown on the profile screen. e.g. Dentist")]
    public string occupation;

    [Tooltip("Known facts listed on the profile screen. Each string is one bullet point. " +
             "e.g. { Logical, Methodical, Trusts Lira }")]
    public string[] knownFacts;

    [Tooltip("Short flavour line shown beneath the prisoner's name on the profile. " +
             "Should feel like a player observation, not a biography. " +
             "e.g. His notes are the most reliable. That might be the problem.")]
    [TextArea(2, 4)]
    public string profileNote;

    // ── Visuals ───────────────────────────────────────────────────────────────

    [Header("Visuals")]
    [Tooltip("Black silhouette sprite shown on the prisoner list and profile screens. " +
             "No faces — let the player imagine.")]
    public Sprite silhouetteSprite;

    [Tooltip("Small symbol icon associated with this prisoner. " +
             "Bram = compass, Mira = spiral, Lira = crown, Yuri = feather. " +
             "Players learn symbols before names.")]
    public Sprite symbolSprite;

    [Tooltip("Accent color used for this prisoner's notes in the sidebar and profile header. " +
             "Stored as a Color so the UI can tint TMP text and sprite overlays at runtime.")]
    public Color accentColor = Color.white;

    // ── Font assets ───────────────────────────────────────────────────────────

    [Header("Fonts")]
    [Tooltip("Used for all notes by this prisoner, except Mira who has three stages.")]
    public TMPro.TMP_FontAsset primaryFont;

    [Tooltip("Mira only — Days 1-10ish. Neat and coherent. Ignored for all other authors.")]
    public TMPro.TMP_FontAsset miraFontEarly;

    [Tooltip("Mira only — Days 11-20ish. Uneven, erratic. Ignored for all other authors.")]
    public TMPro.TMP_FontAsset miraFontMid;

    [Tooltip("Mira only — Days 21+. Near-illegible, scratched. Ignored for all other authors.")]
    public TMPro.TMP_FontAsset miraFontLate;

    // ── Note tracking ─────────────────────────────────────────────────────────

    [Header("Note Tracking")]
    [Tooltip("Total number of notes this prisoner has in the game, including locked ones. " +
             "Used for the X/Y counter on the profile screen. " +
             "Set this manually — JournalManager counts found notes at runtime.")]
    public int totalNoteCount;

    // ── Runtime state — managed by JournalManager, do not edit manually ───────

    [Header("Runtime State — do not edit manually")]
    [HideInInspector] public int foundNoteCount;
    [HideInInspector] public bool hasBeenViewed;

    // ── Font resolver ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the correct TMP_FontAsset for this prisoner.
    /// Pass miraStage — it is ignored for all authors except Mira.
    /// Returns null if the relevant font asset has not been assigned.
    /// </summary>
    public TMPro.TMP_FontAsset GetFont(MiraStage miraStage = MiraStage.Early)
    {
        if (author != PrisonerAuthor.Mira)
            return primaryFont;

        return miraStage switch
        {
            MiraStage.Early => miraFontEarly,
            MiraStage.Mid   => miraFontMid,
            MiraStage.Late  => miraFontLate,
            _               => miraFontEarly
        };
    }

    // ── Editor helper ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(prisonerID))
        {
            prisonerID = name.ToLower().Replace(" ", "_");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (string.IsNullOrEmpty(displayName))
        {
            Debug.LogWarning($"[PrisonerData] '{prisonerID}' has no displayName. " +
                             $"It will appear blank in the journal.", this);
        }

        if (silhouetteSprite == null)
        {
            Debug.LogWarning($"[PrisonerData] '{prisonerID}' has no silhouetteSprite assigned. " +
                             $"The prisoner list will show a blank portrait.", this);
        }

        if (symbolSprite == null)
        {
            Debug.LogWarning($"[PrisonerData] '{prisonerID}' has no symbolSprite assigned. " +
                             $"Players learn symbols before names — this should not be empty.", this);
        }

        if (primaryFont == null && author != PrisonerAuthor.Mira)
        {
            Debug.LogWarning($"[PrisonerData] '{prisonerID}' has no primaryFont assigned. " +
                             $"Notes will render in Unity's default font.", this);
        }

        if (author == PrisonerAuthor.Mira)
        {
            if (miraFontEarly == null)
                Debug.LogWarning($"[PrisonerData] Mira is missing miraFontEarly.", this);
            if (miraFontMid == null)
                Debug.LogWarning($"[PrisonerData] Mira is missing miraFontMid.", this);
            if (miraFontLate == null)
                Debug.LogWarning($"[PrisonerData] Mira is missing miraFontLate.", this);
        }

        if (totalNoteCount <= 0)
        {
            Debug.LogWarning($"[PrisonerData] '{prisonerID}' has totalNoteCount of {totalNoteCount}. " +
                             $"The X/Y counter on the profile screen will show 0 as the denominator.", this);
        }
    }
#endif
}
