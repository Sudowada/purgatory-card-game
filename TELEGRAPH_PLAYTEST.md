# Telegraphed enemy attacks

Work is on feature/telegraphed-enemy-attacks. Main was not edited. The existing staging, shield, hover-follow, undo, dealing and offering integration were reviewed and extended.

## Rules

One attack is chosen uniformly at random after each completed deal/refill. Targets and damage are captured before staging or hand reordering. Disable Random Attack Kind to select an attack for testing.

| Attack | Announced recipients | Effect |
| --- | --- | --- |
| Decay | Random distinct cards (Targets Per Attack) | Face value times Decay Fraction |
| Plague | Three random distinct cards, capped by hand size | 30% of face value |
| Spread | Immediate original neighbours of cards with existing decay | +1 decay; no chained propagation |
| Fester | Cards with existing decay | Adds the announced existing decay, doubling it |
| Wither | All original cards | +1 decay |
| Flank | Original leftmost and rightmost cards | 50% of face value |
| PointValue | Random distinct cards | Subtract Point Damage from scoring points, clamped at zero |

Percentage decay rounds up. Decay Multiplier mitigation is applied before rounding down. Decay is cumulative and caps at rotThreshold; face value, rank and suit stay unchanged. Existing rotten-card scoring remains in place. Point attacks are separate from decay and do not rot cards or respond to decay freeze/protection. New draws reset point damage; Return cards retain it.

Staging targeted cards immediately hides the indicator and plays the centered shield. Undo exits the shield smoothly and restores the same target without waiting for the animation. Confirmation and legacy played cards defend their announced attacks. Resolve runs once before replacement slots are reused. Spread occurs only when explicitly rolled, never passively each turn.

Slow Rot, Anchor Card, Value Lock, Double Decay, Instant Rot, Purifier and freeze remain supported. PlagueAll broadens recipients at announcement, so affected cards show indicators. Rot Burst is called only on the transition to fully rotten. Existing shop, scoring formula, altar animation and deck ownership are retained.

Hover a healthy in-hand card and press D to discard and replace immediately. Any existing decay (including partial decay) prevents discard. Empty draw piles reject discard. The announced slot remains targeted with its original damage, preventing discard from erasing an attack. This is a free discard action; no turn cost or discard limit was specified. TryDiscardCard is available for later button wiring. Hand Refresh consumable wiring remains an existing stub and is not implemented here.

## Inspector setup

- Add RandomEnemyTelegraph to the offering-controller object before Play to tune its settings. GameManager supplies the hand reference. Use one selector only. Random Attack Kind defaults on; Attack Kind is used when random selection is off. Targets Per Attack affects generic decay and point attacks; named attacks have fixed recipient rules.
- Card3Droot already has CardAttackVisuals. Drop art into Attack Indicator Prefab and Shield Impact Prefab. Empty slots use point-filtered pixel arrow/shield placeholders. Prefab origins should be centered; optional anchors and offsets position each effect. Effects follow VisualRoot and keep their own scale outside burn meshes. Art colliders are disabled.
- A TextMesh child displays attack kind and amount; a prefab TextMesh can replace its appearance. The default text is a diagnostic label, not final pixel-font art. Import custom art with Point filtering and no compression.
- Shield defaults: 0.07s entry, 0.35s display, 0.12s exit, 0.08s undo exit. Burn holds the effect until shrink completes. Bob height/speed and all shield timings are tunable on CardAttackVisuals.
- GameManager exposes Offered Attack Lead In (0.3s) and Attack Recovery (0.1s). Existing altar animation and scoring timings are retained.
- Full Screen Pass uses Purgatory/Fullscreen Dither, After Rendering Post Processing, Fetch Color Buffer enabled, Depth requirement enabled. Corner Concentration defaults 0.9; Corner Reach defaults 0.65. Existing materials may need these set explicitly. Increase Grime/Coverage for stronger islands. Near Clear/Near Full suppress close surfaces; Highlight Fade controls lit-surface protection. Fixed screen-space patches use no animation. This is brightness-adaptive, not a separate lighting buffer: dark albedo cannot be distinguished from shadow.

## Playtest

1. Force each attack kind. Confirm Plague has three distinct targets, Flank only original endpoints, Wither all cards, Fester only already decayed cards. Test a single card and empty hand.
2. For Spread, infect only the middle of five cards: only its original neighbours should be announced/hit. Move/stage the source and neighbours; recipients must not shift. Repeat with several infected cards and ensure hits deduplicate.
3. Hover and drag a target: arrow/text follow the visual. Stage it: shield appears immediately on its face. Undo immediately during burn and repeatedly stage/undo. Input should remain responsive; the original indicator should return.
4. Confirm zero, one or all targets. Played/Return cards defend the current attack; new cards receive only the next announced attack. Resolve twice must not hit twice.
5. Test Ace and King rounding, partial decay, rot threshold, every decay relic, freeze, and point attacks. Points should change without changing rank, face value or decay. Draw replacements reset point damage.
6. Hover and press D: a healthy card replaces immediately. Partially decayed/rotten cards and empty deck reject discard. Targets remain announced. Verify no discard action during combat resolution or exploration.
7. Finish rounds and the run, open/leave shop, stand/reseat, exhaust deck, and test Return. Check score, relic bonuses, altar burn, hand visibility and no lingering indicators.
8. Inspect grime at all corners, with lanterns, close cards and bright objects. Adjust material controls at multiple resolutions. Verify little camera movement variation and no noisy animation.

## Validation and limits

Tests/Telegraph/Validate.ps1 was run with Unity 6000.3.10f1 Editor/Data. Card, RandomEnemyTelegraph and CardAttackVisuals compile against real UnityEngine assemblies using C# 9; unassigned Inspector-slot warnings are expected. All 26 behavior checks pass, including named recipients, no chained Spread, block/undo, rounding, freeze, relic protection and independent point damage. git diff --check passes.

These checks use narrow substitutes for unrelated game systems. They do not compile GameManager/Card3DInteractable or the full project, run Play Mode, validate pointer interaction/scene serialization, or compile/render the shader. Unity Editor playtesting and final art tuning were not performed.

The repository declares Unity 2022.3.16f1 and URP 14.0.9, while Unity 6000.3.10f1 is installed here. Project/package migration to Unity 6.3 was not performed. Fullscreen shader compatibility must be validated in the intended URP version. No final custom attack/shield art was supplied, so prefab slots and placeholders remain.
