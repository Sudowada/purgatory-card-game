using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton. The single source of truth for all journal state.
/// Tracks notes, relics, and collectibles — both per-run and persistent state.
///
/// Setup:
///   1. Create an empty GameObject in your main scene named "JournalManager"
///   2. Attach this script to it
///   3. Drag all NoteData assets into allNotes
///   4. Drag all RelicData assets into allRelics
///   5. Drag all PrisonerData assets into allPrisoners
///   6. Drag all CollectibleData assets into allCollectibles
/// </summary>
public class JournalManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static JournalManager Instance { get; private set; }

    // ── Inspector lists ───────────────────────────────────────────────────────

    [Header("All Journal Assets — populate in Inspector")]
    public List<NoteData>        allNotes        = new List<NoteData>();
    public List<RelicData>       allRelics       = new List<RelicData>();
    public List<PrisonerData>    allPrisoners    = new List<PrisonerData>();
    public List<CollectibleData> allCollectibles = new List<CollectibleData>();

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<NoteData>        OnNoteUnlocked;
    public event Action<NoteData>        OnNoteRead;
    public event Action<RelicData>       OnRelicUnlocked;
    public event Action<RelicData>       OnRelicUpgraded;
    public event Action<CollectibleData> OnCollectiblePickedUp;
    public event Action<CollectibleData> OnCollectibleStateChanged;

    // ── Persistent runtime state ──────────────────────────────────────────────

    private HashSet<string>         unlockedNoteIDs       = new HashSet<string>();
    private HashSet<string>         readNoteIDs           = new HashSet<string>();
    private HashSet<string>         unlockedRelicIDs      = new HashSet<string>();
    private HashSet<string>         examinedRelicIDs      = new HashSet<string>();
    private HashSet<string>         upgradedRelicIDs      = new HashSet<string>();
    private Dictionary<string, int> collectibleStates     = new Dictionary<string, int>();
    private HashSet<string>         collectiblesEverFound = new HashSet<string>();

    // ── Per-run runtime state (resets each run) ───────────────────────────────

    private HashSet<string> collectiblesPickedUpThisRun = new HashSet<string>();

    // ── Lookup dictionaries ───────────────────────────────────────────────────

    private Dictionary<string, NoteData>        noteLookup        = new Dictionary<string, NoteData>();
    private Dictionary<string, RelicData>       relicLookup       = new Dictionary<string, RelicData>();
    private Dictionary<string, PrisonerData>    prisonerLookup    = new Dictionary<string, PrisonerData>();
    private Dictionary<string, CollectibleData> collectibleLookup = new Dictionary<string, CollectibleData>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildLookups();
    }

    private void Start()
    {
        LoadJournalState();
        RegisterEventListeners();
    }

    private void OnDestroy()
    {
        UnregisterEventListeners();
    }

    // ── Lookup builder ────────────────────────────────────────────────────────

    private void BuildLookups()
    {
        noteLookup.Clear();
        foreach (var note in allNotes)
        {
            if (note == null) continue;
            if (noteLookup.ContainsKey(note.noteID))
            { Debug.LogWarning($"[JournalManager] Duplicate noteID: '{note.noteID}'"); continue; }
            noteLookup[note.noteID] = note;
        }

        relicLookup.Clear();
        foreach (var relic in allRelics)
        {
            if (relic == null) continue;
            if (relicLookup.ContainsKey(relic.relicID))
            { Debug.LogWarning($"[JournalManager] Duplicate relicID: '{relic.relicID}'"); continue; }
            relicLookup[relic.relicID] = relic;
        }

        prisonerLookup.Clear();
        foreach (var prisoner in allPrisoners)
        {
            if (prisoner == null) continue;
            prisonerLookup[prisoner.prisonerID] = prisoner;
        }

        collectibleLookup.Clear();
        foreach (var col in allCollectibles)
        {
            if (col == null) continue;
            if (collectibleLookup.ContainsKey(col.collectibleID))
            { Debug.LogWarning($"[JournalManager] Duplicate collectibleID: '{col.collectibleID}'"); continue; }
            collectibleLookup[col.collectibleID] = col;
        }
    }

    // ── Event registration ────────────────────────────────────────────────────

    private void RegisterEventListeners()
    {
        foreach (var note in allNotes)
        {
            if (note == null || string.IsNullOrEmpty(note.unlockEventID)) continue;
            string id = note.noteID;
            GameEvents.Subscribe(note.unlockEventID, () => TryUnlockNote(id));
        }

        foreach (var relic in allRelics)
        {
            if (relic == null) continue;
            if (!string.IsNullOrEmpty(relic.unlockEventID))
            {
                string id = relic.relicID;
                GameEvents.Subscribe(relic.unlockEventID, () => TryUnlockRelic(id));
            }
            if (!string.IsNullOrEmpty(relic.upgradeEventID))
            {
                string id = relic.relicID;
                GameEvents.Subscribe(relic.upgradeEventID, () => TryUpgradeRelic(id));
            }
        }

        foreach (var col in allCollectibles)
        {
            if (col == null) continue;
            if (!string.IsNullOrEmpty(col.pickupEventID))
            {
                string id = col.collectibleID;
                GameEvents.Subscribe(col.pickupEventID, () => OnCollectiblePickedUpEvent(id));
            }
            if (!string.IsNullOrEmpty(col.interactionEventID))
            {
                string id = col.collectibleID;
                GameEvents.Subscribe(col.interactionEventID, () => TryCollectibleInteraction(id));
            }
        }
    }

    private void UnregisterEventListeners()
    {
        foreach (var note in allNotes)
        {
            if (note == null || string.IsNullOrEmpty(note.unlockEventID)) continue;
            string id = note.noteID;
            GameEvents.Unsubscribe(note.unlockEventID, () => TryUnlockNote(id));
        }
        foreach (var relic in allRelics)
        {
            if (relic == null) continue;
            if (!string.IsNullOrEmpty(relic.unlockEventID))
            {
                string id = relic.relicID;
                GameEvents.Unsubscribe(relic.unlockEventID, () => TryUnlockRelic(id));
            }
            if (!string.IsNullOrEmpty(relic.upgradeEventID))
            {
                string id = relic.relicID;
                GameEvents.Unsubscribe(relic.upgradeEventID, () => TryUpgradeRelic(id));
            }
        }
        foreach (var col in allCollectibles)
        {
            if (col == null) continue;
            if (!string.IsNullOrEmpty(col.pickupEventID))
            {
                string id = col.collectibleID;
                GameEvents.Unsubscribe(col.pickupEventID, () => OnCollectiblePickedUpEvent(id));
            }
        }
    }

    // ── Note methods ──────────────────────────────────────────────────────────

    public void TryUnlockNote(string noteID)
    {
        if (string.IsNullOrEmpty(noteID) || unlockedNoteIDs.Contains(noteID)) return;
        if (!noteLookup.TryGetValue(noteID, out NoteData note))
        { Debug.LogWarning($"[JournalManager] Note not found: '{noteID}'"); return; }

        unlockedNoteIDs.Add(noteID);
        note.isUnlocked = true;
        if (!string.IsNullOrEmpty(note.relicID)) TryUnlockRelic(note.relicID);
        UpdatePrisonerNoteCount(note.author);
        OnNoteUnlocked?.Invoke(note);
        Save();
        Debug.Log($"[JournalManager] Note unlocked: {note.author} — {note.noteID}");
    }

    public void MarkNoteAsRead(string noteID)
    {
        if (string.IsNullOrEmpty(noteID) || readNoteIDs.Contains(noteID)) return;
        if (!noteLookup.TryGetValue(noteID, out NoteData note)) return;
        readNoteIDs.Add(noteID);
        note.hasBeenRead = true;
        OnNoteRead?.Invoke(note);
        GameEvents.Fire("read_" + noteID);
        Save();
    }

    // ── Relic methods ─────────────────────────────────────────────────────────

    public void TryUnlockRelic(string relicID)
    {
        if (string.IsNullOrEmpty(relicID) || unlockedRelicIDs.Contains(relicID)) return;
        if (!relicLookup.TryGetValue(relicID, out RelicData relic))
        { Debug.LogWarning($"[JournalManager] Relic not found: '{relicID}'"); return; }

        unlockedRelicIDs.Add(relicID);
        relic.isUnlocked = true;
        if (!string.IsNullOrEmpty(relic.unlocksNoteID)) TryUnlockNote(relic.unlocksNoteID);
        OnRelicUnlocked?.Invoke(relic);
        Save();
        Debug.Log($"[JournalManager] Relic unlocked: {relic.relicID}");
    }

    public void TryUpgradeRelic(string relicID)
    {
        if (string.IsNullOrEmpty(relicID) || upgradedRelicIDs.Contains(relicID)) return;
        if (!relicLookup.TryGetValue(relicID, out RelicData relic)) return;

        upgradedRelicIDs.Add(relicID);
        relic.upgradeState = RelicUpgradeState.Upgraded;

        // Unlock any note tied to this upgrade
        if (!string.IsNullOrEmpty(relic.upgradeUnlocksNoteID))
            TryUnlockNote(relic.upgradeUnlocksNoteID);

        OnRelicUpgraded?.Invoke(relic);
        Save();
        Debug.Log($"[JournalManager] Relic upgraded: {relic.relicID} → {relic.CurrentDisplayName}");
    }

    public void MarkRelicAsExamined(string relicID)
    {
        if (string.IsNullOrEmpty(relicID) || examinedRelicIDs.Contains(relicID)) return;
        if (!relicLookup.TryGetValue(relicID, out RelicData relic)) return;
        examinedRelicIDs.Add(relicID);
        relic.hasBeenExamined = true;
        Save();
    }

    // ── Collectible methods ───────────────────────────────────────────────────

    private void OnCollectiblePickedUpEvent(string collectibleID)
    {
        if (!collectibleLookup.TryGetValue(collectibleID, out CollectibleData col)) return;

        collectiblesPickedUpThisRun.Add(collectibleID);
        collectiblesEverFound.Add(collectibleID);
        col.isPickedUpThisRun   = true;
        col.hasBeenPickedUpEver = true;

        OnCollectiblePickedUp?.Invoke(col);
        Save();
        Debug.Log($"[JournalManager] Collectible picked up: {col.collectibleID}");
    }

    /// <summary>
    /// Advances a collectible's persistent state.
    /// Call this when a collectible interaction succeeds.
    /// Also fires the relic upgrade if configured.
    /// </summary>
    public void TryCollectibleInteraction(string collectibleID)
    {
        if (!collectibleLookup.TryGetValue(collectibleID, out CollectibleData col)) return;

        CollectibleState newState = col.interactionResultState;
        if (col.currentState == newState) return; // already in this state

        col.currentState = newState;
        collectibleStates[collectibleID] = (int)newState;

        // Unlock any note tied to this state
        string noteToUnlock = col.GetUnlocksNoteIDForState(newState);
        if (!string.IsNullOrEmpty(noteToUnlock))
            TryUnlockNote(noteToUnlock);

        // Upgrade the relic if this interaction does that
        if (!string.IsNullOrEmpty(col.interactionUpgradesRelicEventID))
            TryUpgradeRelic(col.requiredRelicID);

        OnCollectibleStateChanged?.Invoke(col);
        Save();
        Debug.Log($"[JournalManager] Collectible state changed: {col.collectibleID} → {newState}");
    }

    /// <summary>
    /// Resets per-run collectible pickup state. Call at the start of every new run.
    /// Does NOT reset persistent states (visual changes, relic upgrades).
    /// </summary>
    public void ResetRunState()
    {
        collectiblesPickedUpThisRun.Clear();
        foreach (var col in allCollectibles)
            if (col != null) col.isPickedUpThisRun = false;

        Debug.Log("[JournalManager] Per-run state reset. Persistent states preserved.");
    }

    // ── Prisoner tracking ─────────────────────────────────────────────────────

    private void UpdatePrisonerNoteCount(PrisonerAuthor author)
    {
        foreach (var prisoner in allPrisoners)
            if (prisoner.author == author)
            { prisoner.foundNoteCount = GetUnlockedNotesByAuthor(author).Count; break; }
    }

    // ── Query methods ─────────────────────────────────────────────────────────

    public List<NoteData> GetUnlockedNotes()
    {
        var result = new List<NoteData>();
        foreach (var note in allNotes)
            if (unlockedNoteIDs.Contains(note.noteID)) result.Add(note);
        result.Sort((a, b) => a.day.CompareTo(b.day));
        return result;
    }

    public List<NoteData> GetUnlockedNotesByAuthor(PrisonerAuthor author)
    {
        var result = new List<NoteData>();
        foreach (var note in allNotes)
            if (note.author == author && unlockedNoteIDs.Contains(note.noteID)) result.Add(note);
        result.Sort((a, b) => a.day.CompareTo(b.day));
        return result;
    }

    public List<RelicData> GetUnlockedRelics()
    {
        var result = new List<RelicData>();
        foreach (var relic in allRelics)
            if (unlockedRelicIDs.Contains(relic.relicID)) result.Add(relic);
        return result;
    }

    public List<CollectibleData> GetAllCollectiblesEverFound()
    {
        var result = new List<CollectibleData>();
        foreach (var col in allCollectibles)
            if (collectiblesEverFound.Contains(col.collectibleID)) result.Add(col);
        return result;
    }

    public List<CollectibleData> GetCollectiblesPickedUpThisRun()
    {
        var result = new List<CollectibleData>();
        foreach (var col in allCollectibles)
            if (collectiblesPickedUpThisRun.Contains(col.collectibleID)) result.Add(col);
        return result;
    }

    public NoteData        GetNote(string id)        { noteLookup.TryGetValue(id, out var n); return n; }
    public RelicData       GetRelic(string id)       { relicLookup.TryGetValue(id, out var r); return r; }
    public CollectibleData GetCollectible(string id) { collectibleLookup.TryGetValue(id, out var c); return c; }

    public bool IsNoteUnlocked(string id)        => unlockedNoteIDs.Contains(id);
    public bool IsNoteRead(string id)            => readNoteIDs.Contains(id);
    public bool IsRelicUnlocked(string id)       => unlockedRelicIDs.Contains(id);
    public bool IsRelicUpgraded(string id)       => upgradedRelicIDs.Contains(id);
    public bool IsCollectibleFoundEver(string id)=> collectiblesEverFound.Contains(id);
    public bool IsCollectibleHeldThisRun(string id) => collectiblesPickedUpThisRun.Contains(id);

    public int GetTotalUnlockedNoteCount()       => unlockedNoteIDs.Count;
    public int GetTotalNoteCount()               => allNotes.Count;
    public int GetTotalUnlockedRelicCount()      => unlockedRelicIDs.Count;
    public int GetTotalRelicCount()              => allRelics.Count;
    public int GetTotalCollectiblesFound()       => collectiblesEverFound.Count;
    public int GetTotalCollectibleCount()        => allCollectibles.Count;

    // ── Save/load ─────────────────────────────────────────────────────────────

    private void Save()
    {
        JournalSaveSystem.SaveJournalState(
            unlockedNoteIDs,
            readNoteIDs,
            unlockedRelicIDs,
            examinedRelicIDs,
            upgradedRelicIDs,
            collectibleStates,
            collectiblesEverFound);
    }

    private void LoadJournalState()
    {
        var state = JournalSaveSystem.LoadJournalState();
        if (state == null) return;

        unlockedNoteIDs       = new HashSet<string>(state.unlockedNoteIDs);
        readNoteIDs           = new HashSet<string>(state.readNoteIDs);
        unlockedRelicIDs      = new HashSet<string>(state.unlockedRelicIDs);
        examinedRelicIDs      = new HashSet<string>(state.examinedRelicIDs);
        upgradedRelicIDs      = new HashSet<string>(state.upgradedRelicIDs);
        collectibleStates     = state.collectibleStates;
        collectiblesEverFound = new HashSet<string>(state.collectiblesEverFound);

        // Rehydrate ScriptableObject runtime flags
        foreach (var note in allNotes)
        {
            note.isUnlocked  = unlockedNoteIDs.Contains(note.noteID);
            note.hasBeenRead = readNoteIDs.Contains(note.noteID);
        }

        foreach (var relic in allRelics)
        {
            relic.isUnlocked      = unlockedRelicIDs.Contains(relic.relicID);
            relic.hasBeenExamined = examinedRelicIDs.Contains(relic.relicID);
            relic.upgradeState    = upgradedRelicIDs.Contains(relic.relicID)
                                    ? RelicUpgradeState.Upgraded
                                    : RelicUpgradeState.Base;
        }

        foreach (var col in allCollectibles)
        {
            col.hasBeenPickedUpEver = collectiblesEverFound.Contains(col.collectibleID);
            if (collectibleStates.TryGetValue(col.collectibleID, out int stateInt))
                col.currentState = (CollectibleState)stateInt;
        }

        foreach (var prisoner in allPrisoners)
            prisoner.foundNoteCount = GetUnlockedNotesByAuthor(prisoner.author).Count;

        Debug.Log($"[JournalManager] State loaded — " +
                  $"{unlockedNoteIDs.Count} notes, " +
                  $"{unlockedRelicIDs.Count} relics, " +
                  $"{collectiblesEverFound.Count} collectibles found.");
    }
}