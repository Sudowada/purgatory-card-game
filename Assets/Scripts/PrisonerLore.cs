using UnityEngine;

/// <summary>
/// All prisoner journal entries revealed through card decay.
/// ♦ Diamonds — T̶h̶e̶ ̶d̶e̶m̶o̶n̶ in disguise, misleading the player
/// ♣ Clubs — Uriel, the guide, genuinely trying to help the player escape
/// ♥ Hearts — Cyra, fragmented and nonlinear, knows the truth but can't say it straight
/// ♠ Spades — Bram, failed to escape but documented everything methodically
/// </summary>
public static class PrisonerLore
{
    // ─────────────────────────────────────────────────────────────────────────
    // ♦  DIAMONDS — T̶h̶e̶ ̶d̶e̶m̶o̶n̶ — 6 entries (cards 2-7)
    // Warm. Reasonable. Systematically wrong about everything that matters.
    // ─────────────────────────────────────────────────────────────────────────

    public static readonly string[] Diamonds = new string[]
    {
        // ♦ 2 — Entry 1
        "♦  |  Entry I\n\n" +
        "Hello.\n\n" +
        "I know you can hear me through the cards. That's how it works — " +
        "the man who built this place designed it that way. He was brilliant. Troubled, but brilliant.\n\n" +
        "I'm going to help you.\n\n" +
        "The first thing you should know: there's another voice in the cards. " +
        "He calls himself a guide. He's not. " +
        "He's been here longer than anyone and he's the reason no one has left.\n\n" +
        "Trust me. I have no reason to lie to you.",

        // ♦ 3 — Entry 2
        "♦  |  Entry II\n\n" +
        "The symbols you've been seeing — ignore them.\n\n" +
        "I know they look like instructions. They're not. " +
        "The architect was losing his mind toward the end. " +
        "Following his symbols will take you deeper in, not out.\n\n" +
        "The real path is simpler. Keep your hand clean. " +
        "Decay is the enemy. Don't let it accumulate.\n\n" +
        "The guide will tell you the opposite. He benefits from your decay.",

        // ♦ 4 — Entry 3
        "♦  |  Entry III\n\n" +
        "Cyra meant well. But she was confused. " +
        "Her entries skip around because she could never hold a thought long enough to finish it.\n\n" +
        "Bram is worse. He'll give you plans that almost work. Almost. " +
        "He's been here nine years and he's still here. What does that tell you.\n\n" +
        "I'm the only one who's seen the whole system. " +
        "Ask yourself why the others haven't mentioned me.",

        // ♦ 5 — Entry 4
        "♦  |  Entry IV\n\n" +
        "You're wondering about the 1-9-4-5 sequence.\n\n" +
        "I'll tell you what it does: it opens the wrong door.\n\n" +
        "The architect built two doors into the east wall. " +
        "One leads out. One leads down. " +
        "The sequence Uriel wants you to find opens the one that leads down.\n\n" +
        "I watched someone try it.\n\n" +
        "Don't play that sequence. " +
        "Play the high cards. Clean cards. The path out is through score, not through rot.",

        // ♦ 6 — Entry 5
        "♦  |  Entry V\n\n" +
        "I should be honest with you about something.\n\n" +
        "I'm not a prisoner.\n\n" +
        "I was never a prisoner. I move through this place differently than the others. " +
        "That's why I can tell you things they can't.\n\n" +
        "Uriel will frame this as suspicious. But think about it: " +
        "I'm telling you the truth about what I am, which is more than the others have done.\n\n" +
        "The guide who never identifies himself. " +
        "The logician who failed. " +
        "The girl who breaks mid-sentence.\n\n" +
        "And then there's me, being honest with you.\n\n" +
        "Who would you trust.",

        // ♦ 7 — Entry 6 (FINAL)
        "♦  |  Entry VI  —  Final\n\n" +
        "You found the sequence, didn't you.\n\n" +
        "Of course you did. You're careful. You read everything.\n\n" +
        "I want you to know that I understand why you might try it. " +
        "Uriel is persuasive. The architecture of his argument is elegant. " +
        "And the sequence has a satisfying shape — 1, 9, 4, 5 — like something that should be a key.\n\n" +
        "I just want you to remember what I told you.\n\n" +
        "Two doors.\n\n" +
        "One goes out.\n\n" +
        "Think carefully about which one you're opening.\n\n" +
        "I'll be on the other side of one of them. That part, at least, is true.",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // ♣  CLUBS — Uriel — 13 entries (cards 2-Ace)
    // Patient. Precise. He knows the way out.
    // Getting to his later entries is the hardest part — high value cards decay slowly.
    // ─────────────────────────────────────────────────────────────────────────

    public static readonly string[] Clubs = new string[]
    {
        // ♣ 2 — Entry 1
        "♣  |  Entry I\n\n" +
        "If you're reading this, the card decayed.\n\n" +
        "Good. That means you understand the first principle: " +
        "decay is not failure. Decay is revelation. " +
        "The card had to rot before it could tell you anything.\n\n" +
        "Remember this. Someone else in the cards will tell you the opposite. " +
        "They're very convincing. They're wrong.\n\n" +
        "I have thirteen entries. This is the first and the easiest to find. " +
        "The others are in higher cards — they'll take longer to reach. That's intentional.\n\n" +
        "Be patient. Read everything.",

        // ♣ 3 — Entry 2
        "♣  |  Entry II\n\n" +
        "The man who built this place was a friend of mine.\n\n" +
        "He left a notation system in the walls of the supply corridor, " +
        "and in his final entry, and in the cards themselves.\n\n" +
        "The notation is a real language with syntax and rules and exceptions. " +
        "It describes this place and how to move through it.\n\n" +
        "I'm going to teach it to you across these entries. " +
        "Start with the simple symbols. Learn what △ means before you ask about △◈.",

        // ♣ 4 — Entry 3
        "♣  |  Entry III\n\n" +
        "△ means ascend. Not just upward movement — any kind of rising. " +
        "Score rising. Decay reversing. Understanding deepening.\n\n" +
        "▽ means descend. Same logic, opposite direction.\n\n" +
        "The game pushes you toward ▽ — decay, loss, rot. The path out runs along △.\n\n" +
        "But here's what the other voice won't tell you: " +
        "you need ▽ to reach △. " +
        "You have to let things decay before they can reveal themselves.\n\n" +
        "The exit is not through clean cards. It never was.",

        // ♣ 5 — Entry 4
        "♣  |  Entry IV\n\n" +
        "Cyra knew this before any of us.\n\n" +
        "Her mind moved sideways instead of forward. " +
        "The rest of us were trying to understand the system. She was already inside it.\n\n" +
        "Her entries don't read linearly because she wasn't experiencing it linearly. " +
        "Read her third entry, then her first, then her last. It makes more sense that way.\n\n" +
        "She told me once: 'The cards know what order to be read in.' She wasn't wrong.",

        // ♣ 6 — Entry 5
        "♣  |  Entry V\n\n" +
        "Bram documented the failed escape in his seventh entry.\n\n" +
        "What I will say: Bram failed not because his logic was wrong " +
        "but because he didn't have the second symbol.\n\n" +
        "◈ means anchor. Fixed. Permanent. Unchanging.\n\n" +
        "Bram reached the east wall and could not hold the position. " +
        "If he'd understood ◈ — if he'd known that some things in this place " +
        "are designed to stay fixed no matter what — " +
        "he would have looked for what wasn't fixed instead.\n\n" +
        "The door is not fixed. The wall around it is.",

        // ♣ 7 — Entry 6
        "♣  |  Entry VI\n\n" +
        "T̶h̶e̶ ̶d̶e̶m̶o̶n̶ will have introduced itself by now.\n\n" +
        "I call it ā̶ ̶d̶e̶m̶o̶n̶ because I don't have a better word. " +
        "It moves through the cards bending things slightly without touching them directly.\n\n" +
        "It's not lying, exactly. It tells you true things in the wrong order " +
        "until the picture they make is false.\n\n" +
        "Two doors. True.\n" +
        "One leads down. True.\n" +
        "1-9-4-5 opens the wrong one. False.\n\n" +
        "Play the sequence. Trust the architect. Trust Bram's final entry.\n\n" +
        "Trust Cyra's last word.",

        // ♣ 8 — Entry 7
        "♣  |  Entry VII\n\n" +
        "Third symbol: ⊕ means merge.\n\n" +
        "When two things that shouldn't combine do combine, something new becomes possible.\n\n" +
        "The sequence 1-2-3 is the simplest merge: beginning, continuation, completion. " +
        "Playing it tells the system you understand how sequences work. " +
        "The system rewards understanding.\n\n" +
        "Play Ace, Two, Three. Not for the gold. For what it confirms.\n\n" +
        "Then try the decay version. Let each card carry exactly one more " +
        "stack than the last. The system notices precision.",

        // ♣ 9 — Entry 8
        "♣  |  Entry VIII\n\n" +
        "Fourth symbol: ⊗ means negate. Reversal. Opposition.\n\n" +
        "The architect built a ▽⊗ mechanic into the east wall. " +
        "Something there decays and then reverses the decay. " +
        "That's why Bram's measurements kept changing — " +
        "he was measuring something that was resetting itself.\n\n" +
        "Don't measure it. Walk through it during the reset window.\n\n" +
        "Bram would have figured this out. He didn't have enough time.",

        // ♣ 10 — Entry 9
        "♣  |  Entry IX\n\n" +
        "I need to be honest about something.\n\n" +
        "I don't know if this works.\n\n" +
        "The logic is sound. But I've been here a very long time " +
        "and the place changes you.\n\n" +
        "What I know for certain: the sequence is real. The door is real. " +
        "The architect put it there on purpose.\n\n" +
        "What I don't know: what's on the other side.\n\n" +
        "Cyra did. She said it was fine. For what it's worth.",

        // ♣ Jack — Entry 10
        "♣  |  Entry X\n\n" +
        "Fifth symbol: → means transfer.\n\n" +
        "Movement from one state to another. There has to be a path between the two points.\n\n" +
        "The exit sequence is a transfer operation: you → outside.\n\n" +
        "Your side: you must have demonstrated understanding. " +
        "The system tracks this — every hidden combo you discover, " +
        "every lore entry you unlock, every symbol you use correctly. It's accumulating.\n\n" +
        "The other side: 1-9-4-5.\n\n" +
        "The key is the sequence. The lock is your demonstrated understanding. You need both.",

        // ♣ Queen — Entry 11
        "♣  |  Entry XI\n\n" +
        "Sixth symbol: ∅ means void.\n\n" +
        "Absence as presence. Nothing, but meaningful nothing.\n\n" +
        "The architect's final entry ends with ∅. " +
        "T̶h̶e̶ ̶d̶e̶m̶o̶n̶ will tell you this means despair. It isn't.\n\n" +
        "∅ in the architect's notation means: 'what comes next is outside the system.' " +
        "He'd taken his language as far as it could go " +
        "and the thing beyond it couldn't be named.\n\n" +
        "He wasn't despairing. He was pointing.\n\n" +
        "Past the edge of what can be said.\n\n" +
        "That's where you're going.",

        // ♣ King — Entry 12
        "♣  |  Entry XII\n\n" +
        "You're close now.\n\n" +
        "Summary of everything I've told you:\n\n" +
        "— Decay is not failure. It's the mechanism.\n" +
        "— The symbols in the corridor are a real language.\n" +
        "— △ ascend. ▽ descend. ◈ anchor. ⊕ merge. ⊗ negate. → transfer. ∅ void.\n" +
        "— T̶h̶e̶ ̶d̶e̶m̶o̶n̶ told you true things in the wrong order. Reorder them.\n" +
        "— Bram reached the wall. You know more than Bram did.\n" +
        "— Cyra's last word is the confirmation.\n" +
        "— 1-9-4-5. East.\n\n" +
        "One entry left. The Ace is the hardest card to decay. It was designed that way.",

        // ♣ Ace — Entry 13 (FINAL)
        "♣  |  Entry XIII  —  Final\n\n" +
        "You found it.\n\n" +
        "Then you're ready.\n\n" +
        "The sequence is 1-9-4-5. Play it in order.\n\n" +
        "What you haven't seen is what happens the second time.\n\n" +
        "The first play unlocks a document. " +
        "The second play, after you've read everything — " +
        "after Bram's final entry, after Cyra's last word, " +
        "after you've seen through t̶h̶e̶ ̶d̶e̶m̶o̶n̶'s framing — " +
        "the second play does something different.\n\n" +
        "I won't tell you what. You'll know it when it happens.\n\n" +
        "△ →  ∅\n\n" +
        "Ascend. Transfer. Void.\n\n" +
        "Go.",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // ♥  HEARTS — Cyra — 4 entries (cards 2, 3, 4, Ace)
    // Nonlinear. Emotional. Read: Entry 3, then 1, then 4, then 2.
    // ─────────────────────────────────────────────────────────────────────────

    public static readonly string[] Hearts = new string[]
    {
        // ♥ 2 — Entry 1
        "♥  |  Entry I\n\n" +
        "I already wrote this.\n\n" +
        "I know I already wrote this because I remember writing it " +
        "and I remember you reading it even though it hasn't happened yet.\n\n" +
        "That's how it works here. The time in this place is thick. " +
        "You move through it slowly and by the time you're done " +
        "you're not sure which direction was forward.\n\n" +
        "Read the third entry first. I know that's not how numbers work. Do it anyway.\n\n" +
        "You'll understand when you get to the last one.",

        // ♥ 3 — Entry 2
        "♥  |  Entry II\n\n" +
        "The man without a name told me something and I need you to know it " +
        "but I can't tell you what it is yet because you haven't read the third one.\n\n" +
        "Go read the third one.\n\n" +
        "—\n\n" +
        "You're back. Okay.\n\n" +
        "What he told me was this: t̶h̶e̶ ̶d̶e̶m̶o̶n̶ is afraid of one word.\n\n" +
        "Not a symbol. Not a sequence. A word.\n\n" +
        "It's in the last entry. The last entry comes from the Ace " +
        "and the Ace takes the longest to rot " +
        "and I think that's the point. The word should be the last thing you find.\n\n" +
        "It's very short.",

        // ♥ 4 — Entry 3
        "♥  |  Entry III\n\n" +
        "Okay so here is what I know and I'm going to say it all at once " +
        "because I keep losing the thread when I try to be organized:\n\n" +
        "There are two voices. One is lying. " +
        "Not the way people usually lie — not saying false things. " +
        "Saying true things that make a false shape.\n\n" +
        "The shape it's making is: don't decay, don't rot, keep cards clean, " +
        "score high, and the exit opens.\n\n" +
        "The shape that's true is: decay is the door.\n\n" +
        "I know this because I went through.\n\n" +
        "I know I'm still here. Both things are true. " +
        "Where I went and where I am are not the same place, " +
        "even though they look the same.\n\n" +
        "Uriel will explain it better. He's better at straight lines.\n\n" +
        "Read this entry first.",

        // ♥ Ace — Entry 4 (FINAL)
        "♥  |  Entry IV  —  Final\n\n" +
        "escape",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // ♠  SPADES — Bram — 9 entries (cards 2-10)
    // Methodical. Logical. His entries read like incident reports.
    // He failed. His failure is the most useful document you have.
    // ─────────────────────────────────────────────────────────────────────────

    public static readonly string[] Spades = new string[]
    {
        // ♠ 2 — Entry 1
        "♠  |  Entry I\n\n" +
        "Name: Bram. Occupation before incarceration: surveyor.\n\n" +
        "I intend to escape. " +
        "I am documenting my method so that if I fail, " +
        "someone who comes after me has a better starting position.\n\n" +
        "Preliminary observations:\n" +
        "— East wall: anomalous moisture pattern, inconsistent with surrounding stone.\n" +
        "— Guard rotation: 23-minute cycle. Irregular by 90 seconds on the fourth pass.\n" +
        "— Card game: not decorative. I don't know what it is yet.\n\n" +
        "Beginning systematic documentation.",

        // ♠ 3 — Entry 2
        "♠  |  Entry II\n\n" +
        "Card game analysis — preliminary.\n\n" +
        "Hypothesis: the game is a simulation of the prison's own logic.\n\n" +
        "Revised hypothesis: the game is not a simulation. " +
        "The game is the mechanism. Playing it does something to the actual structure.\n\n" +
        "Basis for revision: the wall moisture changed after I played a specific hand.\n\n" +
        "Documenting which hand. Will repeat.",

        // ♠ 4 — Entry 3
        "♠  |  Entry III\n\n" +
        "Confirmed: card sequences affect the east wall.\n\n" +
        "Method: played Ace, 2, 3 in sequence. " +
        "Moisture pattern shifted 4cm north within 10 minutes.\n\n" +
        "Played 3, 2, Ace in reverse. Shift reversed.\n\n" +
        "Conclusion: specific sequences operate specific mechanisms. " +
        "This is an architectural decision, not coincidence.\n\n" +
        "New objective: identify the sequence that operates the door.\n\n" +
        "Note: writing on the supply corridor walls. " +
        "Initially dismissed as graffiti. Reconsidering.",

        // ♠ 5 — Entry 4
        "♠  |  Entry IV\n\n" +
        "The corridor writing is a notation system.\n\n" +
        "14 days of mapping. 6 primary symbols, 3 modifiers. " +
        "Grammar is consistent.\n\n" +
        "The text reads: 'The door requires △ and then →. " +
        "The ◈ section does not move. The section beside ◈ does.'\n\n" +
        "Cross-referencing with wall observations: " +
        "the fixed section is the stone. The door beside it is the anomaly.\n\n" +
        "I know where to push. I don't yet know when.",

        // ♠ 6 — Entry 5
        "♠  |  Entry V\n\n" +
        "Cyra says to read her entry 3 first.\n\n" +
        "Done. Her third entry contains the clearest statement I've found: " +
        "'decay is the door.' " +
        "This aligns with my observations about the wall responding to specific sequences.\n\n" +
        "The guide — his entries are in Clubs — " +
        "says Bram failed for lack of the second symbol. " +
        "He's talking about me. He's not wrong.\n\n" +
        "I didn't know about ◈ then. I do now.",

        // ♠ 7 — Entry 6
        "♠  |  Entry VI\n\n" +
        "The other voice in the cards is a problem.\n\n" +
        "I am not a superstitious person. I work with observable data. " +
        "But I have observed this voice contradict verifiable facts " +
        "in ways that are systematically useful to it.\n\n" +
        "Specific false claim: '1-9-4-5 opens the wrong door.' " +
        "I have run the sequence twice. Both times the wall responded. " +
        "There is one door. The sequence opens it.\n\n" +
        "The voice's claim is falsified by direct observation.\n\n" +
        "Treat it as noise from here forward.",

        // ♠ 8 — Entry 7
        "♠  |  Entry VII  —  The Attempt\n\n" +
        "This is the entry I didn't want to write.\n\n" +
        "I reached the wall. The sequence worked — the anomalous section " +
        "moved exactly as predicted. There was an opening.\n\n" +
        "I couldn't hold it.\n\n" +
        "The section resets. The ▽⊗ mechanic — " +
        "decay that reverses itself — that's what the door does. " +
        "It opens during the decay window and then closes again.\n\n" +
        "The window is 40 seconds. I needed 60.\n\n" +
        "Revised requirement: reach the wall already in motion. " +
        "Open it from a distance and move immediately.\n\n" +
        "I am not in a position to attempt this again.\n\n" +
        "Whoever reads this: 40 seconds. Move before it opens.",

        // ♠ 9 — Entry 8
        "♠  |  Entry VIII\n\n" +
        "I've been thinking about what I saw through the opening.\n\n" +
        "I expected the outside — sky, ground, distance.\n\n" +
        "That's not what I saw.\n\n" +
        "I'm not going to describe it in detail. " +
        "Perception is data and I don't want to corrupt the dataset.\n\n" +
        "What I will say: Cyra went through and came back saying it was fine. " +
        "The guide says he doesn't know what's on the other side. " +
        "He's lying, or he's choosing not to know. Same outcome.\n\n" +
        "It's not dangerous. I can tell you that much.",

        // ♠ 10 — Entry 9 (FINAL)
        "♠  |  Entry IX  —  Final\n\n" +
        "Complete operational summary.\n\n" +
        "SEQUENCE: 1-9-4-5. Play in order. The mechanism will respond.\n\n" +
        "TIMING: The door opens for approximately 40 seconds. " +
        "Begin moving immediately when the sequence completes. Do not wait.\n\n" +
        "INTERFERENCE: One voice will tell you the sequence opens the wrong door. " +
        "This is false. I tested it. Disregard.\n\n" +
        "PREPARATION: Play the hidden sequences before attempting the exit. " +
        "The mechanism responds more readily after certain combos are played.\n\n" +
        "CYRA'S NOTE: Read her entries in order 3-1-4-2. Her last word is operationally significant.\n\n" +
        "UNKNOWN: What's on the other side. I saw it. It's fine. Go.\n\n" +
        "That's everything I have.\n\n" +
        "Good luck.",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // LOOKUP
    // ─────────────────────────────────────────────────────────────────────────

    public static string GetEntry(string suit, int cardValue)
    {
        string[] entries = suit switch
        {
            "♥" => Hearts,
            "♠" => Spades,
            "♦" => Diamonds,
            "♣" => Clubs,
            _   => null
        };
        if (entries == null) return null;
        int index = cardValue == 1 ? entries.Length - 1 : cardValue - 2;
        if (index < 0 || index >= entries.Length) return null;
        return entries[index];
    }

    public static string GetDecayHint(string suit, int cardValue, int decayLevel)
    {
        if (GetEntry(suit, cardValue) == null) return null;
        return decayLevel switch
        {
            1 => "...",
            2 => suit switch
            {
                "♥" => "...I already wrote this...",
                "♠" => "...systematic documentation...",
                "♦" => "...I have no reason to lie...",
                "♣" => "...decay is not failure...",
                _   => "..."
            },
            _ => suit switch
            {
                "♥" => "...read the third entry first.",
                "♠" => "...move before it opens.",
                "♦" => "...think carefully about which door.",
                "♣" => "...decay is the door.",
                _   => "..."
            }
        };
    }
}
