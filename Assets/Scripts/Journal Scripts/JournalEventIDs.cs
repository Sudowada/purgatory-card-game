/// <summary>
/// All GameEvent ID strings used by the journal system in one place.
/// Always use these constants instead of typing raw strings.
///
/// Usage:
///   GameEvents.Fire(JournalEventIDs.FoundToothBox);
///
/// In NoteData / RelicData / CollectibleData Inspector fields, type the string
/// value exactly as it appears after the = sign. e.g. "found_tooth_box"
///
/// Adding a new event:
///   1. Add a new const string here
///   2. Set it as the relevant eventID on the asset
///   3. Call GameEvents.Fire(JournalEventIDs.YourNewEvent) at the trigger point
/// </summary>
public static class JournalEventIDs
{
    // ── Relic discoveries ─────────────────────────────────────────────────────

    public const string FoundToothBox           = "found_tooth_box";
    public const string FoundLooseBrick         = "found_loose_brick";
    public const string FoundHiddenFood         = "found_hidden_food";
    public const string FoundDrainageNote       = "found_drainage_note";
    public const string FoundHiddenCompartment  = "found_hidden_compartment";

    // ── Relic upgrades ────────────────────────────────────────────────────────

    public const string RelicRatUpgraded        = "relic_rat_upgraded";

    // ── Collectible pickups ───────────────────────────────────────────────────
    // Format: "picked_up_" + collectible name
    // These re-fire every run when the player picks up the object.

    public const string PickedUpBigCheese       = "picked_up_big_cheese";
    public const string PickedUpRat             = "picked_up_rat";

    // ── Collectible interactions ──────────────────────────────────────────────
    // Format: "collectible_" + description of interaction
    // These fire when a relic+collectible interaction succeeds.

    public const string CheeseRatInteraction    = "collectible_cheese_fed_to_rat";

    // ── Collectible state changes ─────────────────────────────────────────────
    // Format: "collectible_" + id + "_state_" + state name
    // Fired automatically by JournalManager when state changes.
    // Reference these in unlockEventID on notes that unlock from state changes.

    public const string BigCheeseStateA         = "collectible_big_cheese_state_a";
    public const string BigCheeseStateB         = "collectible_big_cheese_state_b";

    // ── Note read events — auto-fired by JournalManager.MarkNoteAsRead() ──────
    // Format: "read_" + noteID
    // Do not fire these manually — JournalManager handles them.
    // Reference them in unlockEventID on notes that cascade from reading another note.

    public const string ReadBramDay05           = "read_bram_day05";
    public const string ReadBramDay14           = "read_bram_day14";
    public const string ReadBramDay29           = "read_bram_day29";

    public const string ReadMiraDay03           = "read_mira_day03";
    public const string ReadMiraDay19           = "read_mira_day19";
    public const string ReadMiraDay22           = "read_mira_day22";

    public const string ReadLiraDay07           = "read_lira_day07";
    public const string ReadLiraDay14           = "read_lira_day14";
    public const string ReadLiraDay22           = "read_lira_day22";

    public const string ReadYuriDay09           = "read_yuri_day09";

    // ── Location / interaction events ─────────────────────────────────────────

    public const string InspectedDrainChannel   = "inspected_drain_channel";
    public const string InspectedLooseStone     = "inspected_loose_stone";
    public const string InspectedEastWall       = "inspected_east_wall";
    public const string InspectedBucket         = "inspected_bucket";

    // ── Puzzle events ─────────────────────────────────────────────────────────

    public const string SolvedScratchPuzzle     = "solved_scratch_puzzle";
    public const string SolvedToothPuzzle       = "solved_tooth_puzzle";

    // ── Game state events ─────────────────────────────────────────────────────

    public const string NewGameStarted          = "new_game_started";
    public const string RunStarted              = "run_started";
    public const string RunEnded                = "run_ended";
    public const string GameCompleted           = "game_completed";
}