# Wall-writing lighting puzzle

## Open the playtest

1. Open the project with the editor version in `ProjectSettings/ProjectVersion.txt` (2022.3.16f1). Let Unity resolve the Input System package. The project uses **Both** under **Player → Active Input Handling**, so the existing legacy input still works.
2. Choose **Tools → Purgatory → Open Lighting Puzzle Playtest**. Unity prompts about unsaved changes before opening a generated cell. You can save this scene under a new name to reopen it later.
3. Enter Play. Use **WASD** and mouse look. The blue switch starts straight ahead. Aim the center dot at a switch and left-click to flip it, or hold left-click and drag vertically to choose a position. Turn **Red** and **Blue** on and leave **Green** off.
4. Turn right and aim the center dot at the writing on the same wall. Left-click. The writing flashes and a keyhole grows out of the wall just below it. Click the keyhole to show **A key is needed.**

The playtest uses placeholder geometry and text. Its save ID is separate from the production default. The existing build opens the card scene.

## Wire an existing cell in the Inspector

Use the existing first-person camera, electrical box and room lights. Keep the clue controller active and put the two click targets on separate GameObjects. To add the starter writing and keyhole to the scene you already have open, place an empty, unit-scale anchor on the inside wall surface. Orient its local **−Z** toward the player. Select the anchor and choose **Tools → Purgatory → Add Lighting Clue at Selected Wall Anchor**. Check the camera and controller assignments, fill in the light slots, and save your scene. Move the anchor a little farther into the cell if the wall collider intercepts clicks.

```text
Electrical Box                         SwitchPanelInteract
  Red Switch                           PuzzleSwitch → Red Room Light
  Blue Switch                          PuzzleSwitch → Blue Room Light
  Green Switch                         PuzzleSwitch → Green Room Light
Wall Clue                              LightConditionClue
  Wall Writing                         TextMeshPro + Box Collider
    Writing Glow                       Optional purple point Light
  Wall Keyhole                         Box Collider
    Keyhole Art
```

| Field | Assignment |
| --- | --- |
| Puzzle Id | A unique, stable ID for each independent clue. The scene tool assigns one automatically. |
| Player Camera / Player Controller | The first-person camera and its controller. Assign the controller to block clicks while zoomed into another interaction. |
| Required Lights | Red and blue room **Light** components. |
| Forbidden Lights | Green room **Light** component. Leave unrelated lights out of both lists. |
| Required Colors / Forbidden Colors | Optional rules for a room Light that changes color. Assign the Light and copy the matching up/down color from its switch. Clear the starter on/off slots if this clue uses color rules alone. |
| Writing / Writing Target | World-space TextMeshPro and a dedicated, non-trigger collider on its object or parent. |
| Writing Glow | Optional point Light under Writing Target. Set a low idle intensity. The flash temporarily multiplies it. |
| Keyhole Target | Dedicated non-trigger collider on the keyhole root, with the art underneath it. |
| On Keyhole Clicked | Connect a locked-message response or a future lamp-key handler. The playtest binds this to its locked message. |

Disable both target GameObjects in the authored scene. Place their collider faces slightly into the cell from the wall so the wall collider sits behind them. Remove colliders on child art that could intercept clicks. Keep both targets on layers included in default physics raycasts. Start with a 3 m interaction distance.

A configured light counts as on when its object and component are active and its intensity is positive. Assign independent color emitters to **Lever Light** or use a **Binary Light** that can change between two colors. A color rule matches only an active light with each RGB channel within 0.02 of the assigned color. Use a 3D forward renderer on the cell camera so the room lights illuminate the wall. The generated camera selects **LightingPlaytestRenderer** in renderer slot 2.

On **SwitchPanelInteract**, keep **Use World Interaction** enabled and assign the existing camera, controller, and switches. Put a non-trigger collider on each switch root or its moving handle. Assign each **Lever Handle** to the animated pivot, with its local Y rotation axis aligned to the actual hinge and its up/down angles set for that model. The crosshair captures the switch until release and temporarily holds mouse look while dragging. A click and a completed drag use the original switch and color-sequence callbacks. Existing middle detents still work. Turn off **Use World Interaction** to use the legacy panel focus interaction during comparison testing.

Discovery is saved when the player clicks the writing. Scene reload restores the keyhole directly. With Play Mode stopped, open the clue component's context menu and select **Clear Saved Clue Discovery** to clear just that clue's PlayerPrefs entry.

## Playtest checklist

- [ ] Clear the playtest clue's saved discovery. Verify that the writing and keyhole are absent on entry. Aim at the intended writing location and left-click to confirm there is no reveal.
- [ ] Try red alone, blue alone, and all three colors. Verify that the writing remains hidden. The wall should still respond to the room lights.
- [ ] Turn green off with red and blue on. Verify that glowing writing appears. Break the combination and confirm it disappears before discovery.
- [ ] Restore the combination in the reverse switch order using click and drag. Verify the camera stays in place and releases mouse look afterward. Try the existing sequence puzzle and check that each release submits one step.
- [ ] Click the writing within 3 m. Verify the flash and short keyhole growth. Try a click beyond 3 m and a click with an intervening collider before completing discovery.
- [ ] Click the resulting keyhole and check the locked message. Change the lights, including switching all three off. Verify the keyhole remains clickable and the writing stays hidden.
- [ ] Stop and restart Play Mode. Quit and reopen Unity, then regenerate the cell or reopen the saved playtest scene. Verify that the keyhole returns immediately and remains clickable with all three switches off.
- [ ] Clear just the clue's saved discovery again and confirm it starts hidden. In the production cell, check that radio, shop and settings block switch and clue clicks. Start dragging, then switch away from the game window and verify mouse look comes back without submitting a switch input.
