// JournalEnums.cs
// All enums for the journal system in one file.
// Do not split these across multiple files — JournalManager,
// NoteData, RelicData, PrisonerData, and CollectibleData all reference these.

// ─── Prisoners ────────────────────────────────────────────────────────────────

public enum PrisonerAuthor
{
    Bram,   // The dentist. Rational, methodical, trusts Lira.
    Mira,   // Starts coherent. Deteriorates. References Lira constantly.
    Lira,   // Helpful. Elegant. Knows too much.
    Yuri    // Cryptic. Sparse. Almost never names anyone.
}

// ─── Note categories ──────────────────────────────────────────────────────────

public enum NoteCategory
{
    Note,           // Standard written notes — the majority of content
    Relic,          // Physical objects found in the cell (tooth box, etc.)
    Puzzle,         // Notes tied to an active puzzle or solved state
    Collectible     // Notes unlocked by collectible state changes
}

// ─── Mira's handwriting stages ────────────────────────────────────────────────

public enum MiraStage
{
    Early,  // Days 1-10ish. Neat, coherent, sympathetic.
    Mid,    // Days 11-20ish. Uneven spacing, erratic pressure.
    Late    // Days 21+. Near-illegible. Some words repeated or scratched out.
}

// ─── Journal screens ──────────────────────────────────────────────────────────

public enum JournalScreen
{
    Home,               // The 3x2 category tile screen
    NoteList,           // Scrollable list of unlocked notes
    NoteReader,         // Full reading pane for a single note
    PrisonerList,       // The four prisoner rows
    PrisonerProfile,    // Individual prisoner detail screen
    RelicList,          // Unlocked relics
    RelicViewer,        // Single relic detail
    PuzzleList,         // Puzzle tracking screen
    CollectibleList,    // All collectibles found on the shelf
    CollectibleViewer   // Single collectible detail with lore blurb
}

// ─── Unlock trigger types ─────────────────────────────────────────────────────

public enum UnlockTriggerType
{
    OnGameStart,            // Available immediately — no event needed
    OnGameEvent,            // Fires when GameEvents.Fire(eventID) is called
    OnNoteRead,             // Fires when a specific other note is opened
    OnAllNotesRead,         // Fires when every note from a specific author is read
    OnRelicFound,           // Fires when a specific relic is discovered
    OnRelicUpgraded,        // Fires when a relic changes state (e.g. rat becomes full)
    OnCollectiblePickedUp,  // Fires when a collectible is picked up this run
    OnCollectibleStateChanged, // Fires when a collectible's persistent state changes
    OnDayReached            // Fires when the in-game day counter hits a threshold
}

// ─── Note sort order ──────────────────────────────────────────────────────────

public enum NoteSortOrder
{
    DayAscending,   // Day 1 → Day 66. Default.
    DayDescending,  // Most recent first
    AuthorThenDay,  // Grouped by prisoner, then chronological within each
    Unread          // Unread notes bubble to the top
}

// ─── Relic condition ──────────────────────────────────────────────────────────

public enum RelicCondition
{
    Intact,
    Damaged,
    Partial,    // e.g. only some of Bram's teeth are in the box
    Unknown
}

// ─── Relic upgrade state ──────────────────────────────────────────────────────
// Relics can have a base state and an upgraded state with different
// stats, sprites, descriptions, and buff values.
// Not all relics have an upgrade — IsUpgraded defaults to false.

public enum RelicUpgradeState
{
    Base,       // Default state. e.g. Starving Rat — weak buffs
    Upgraded    // Permanently upgraded. e.g. Full Rat — stronger buffs
}

// ─── Collectible visual state ─────────────────────────────────────────────────
// Tracks the persistent visual/functional state of a collectible across runs.
// Add new values here as you design collectibles that can change.

public enum CollectibleState
{
    Default,        // As found — no interactions yet
    StateA,         // First state change. e.g. Cheese: casing melted
    StateB,         // Second state change if needed. e.g. Cheese: partially eaten
    StateC,         // Third state change if needed
    Destroyed       // Fully consumed — still shows in journal, gone from shelf
}

// ─── Journal open/close animation style ──────────────────────────────────────

public enum JournalTransition
{
    Instant,    // For testing — no animation
    PageFlip,   // Scale X fold/unfold. Default.
    Fade        // Simple alpha fade. Fallback for low-end devices.
}