# Telegraphed decay playtest

The GameManager initializes a single RandomEnemyTelegraph on the offering controller (or itself in the legacy selection flow). Each initial deal and completed refill announces two distinct random cards, capped by the remaining hand size. Staging hides that card's indicator and plays its shield; undo restores the same target. Confirmation blocks offered targets. After playing, remaining announced targets receive ceil(faceValue * 0.30) decay before slots are reused. Decay accumulates up to the rot threshold. No automatic Spread or adjacency damage is implemented.

Existing score calculation, shop, deck spending, Return behavior, and burn/draw animations remain in place. Slow Rot, Anchor Card, Value Lock, Double Decay, Instant Rot, PlagueAll, Purifier, and decay freeze are respected by the resolver. PlagueAll explicitly broadens damage to the remaining hand; Double Decay doubles decay amount rather than adding unannounced random targets. The optional decayMultiplier applies mitigation before flooring. The old random three-card ticking phase and its all-rotten instant-loss check are removed. Existing forced selection and scoring penalties for rotten cards are otherwise unchanged; redesigning those is outside this attack implementation.

## Inspector setup

Card3Droot now contains CardAttackVisuals with stable script metadata. Drop art into Attack Indicator Prefab and Shield Impact Prefab. Empty slots use point-filtered pixel arrow/shield placeholders. Custom prefabs should have their origin at the art center and use the card's local X/Z plane; sprite-only placeholder orientation is handled automatically. Use optional anchors or the local offsets to position the art. Effects follow the moving VisualRoot pose while keeping their own scale and materials outside the burn mesh. Art colliders are disabled to preserve card input.

Default tempo: 0.07s shield entry/pop, 0.35s minimum display, 0.12s normal exit, 0.08s undo exit; burn holds the shield until the shrink finishes. All values, bob height/speed, and art slots are editable on the card prefab. Add RandomEnemyTelegraph to the offering-controller object before Play to tune targetsPerAttack, decayFraction, or decayMultiplier; runtime initialization fills its hand reference. Do not add a second selector elsewhere.

## Unity checks

1. Start a round: exactly two cards show indicators. Hover, drag, and move them; indicators should track their visual faces and bob without taking pointer hits.
2. Stage a targeted card. Shield appears immediately, centered, retains its size during burn/shrink, then exits. Press Escape immediately during the burn: card returns, shield exits smoothly, and the same target indicator returns. Repeatedly stage/undo, including undoing an older preview during another card's burn.
3. Stage an untargeted card: no shield. Confirm with zero, one, and both targeted cards offered. Only unblocked targets take decay. New/Return cards must not receive the prior attack; next targets appear after refill settles.
4. Check Ace (1 decay = fully rotten), King (4 decay for a 30% hit), repeat hits, freeze, Slow Rot, protective relics, and Double Decay. Adjacent cards never receive ordinary attack damage.
5. Finish a round, open/leave shop, stand/reseat, exhaust the deck, and finish a run. Check that no stale indicators follow spent slots and that shop, score, Return, draw, and card animations still behave normally.

## Automated checks

Run `Tests/Telegraph/Validate.ps1 -UnityData '<Unity installation>/Editor/Data'` using an installation with bundled Roslyn/NetCoreRuntime and NetStandard references. It compiles Card, RandomEnemyTelegraph, and CardAttackVisuals against real UnityEngine assemblies, with narrow interface substitutes for unrelated game classes. Then 17 behavior checks run the actual Card and RandomEnemyTelegraph source against lightweight Unity substitutes (two targets, block/undo, one-shot resolution, no Spread, rounding, accumulation, freeze, relic protection, mitigation, reused slots, and empty hand).

These checks do not compile the full project or test rendering, DOTween, pointer interaction, or scene serialization. The repository declares Unity 2022.3.16f1; only Unity 6000.3.10f1 reference assemblies were available for the external compiler check. Use the project's normal editor and installed packages for final Console and Play Mode validation.
