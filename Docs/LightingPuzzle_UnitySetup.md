# Wall-writing lighting puzzle

## Open the playtest

1. Open the project with the editor version in `ProjectSettings/ProjectVersion.txt` (2022.3.16f1). Let Unity resolve the Input System package. The project uses **Both** under **Player → Active Input Handling**, so the existing legacy input still works.
2. Choose **Tools → Purgatory → Open Lighting Puzzle Playtest**. Unity prompts about unsaved changes before opening a generated cell. You can save this scene under a new name to reopen it later.
3. Enter Play. Use **WASD** and mouse look. The blue switch starts straight ahead. Aim at any of the three switches and press **E** to zoom in. Left-click **Red** and **Blue**. Leave **Green** off. Press **E** or **Esc** to exit the panel.
4. Turn right and aim the center dot at the writing on the same wall. Left-click. The writing flashes and a keyhole grows out of the wall just below it. Click the keyhole to show **A key is needed.**

The playtest uses placeholder geometry and text. Its save ID is separate from the production default. The existing build opens the card scene.

## Wire an existing cell in the Inspector

Use the existing first-person camera, electrical box and room lights. Keep the clue controller active and put the two click targets on separate GameObjects.

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
| Puzzle Id | A unique, stable ID for each independent clue. Use `cell_wall_keyhole` here. |
| Player Camera / Player Controller | The first-person camera and its controller. Assign the controller to block clicks while zoomed into another interaction. |
| Required Lights | Red and blue room **Light** components. |
| Forbidden Lights | Green room **Light** component. Leave unrelated lights out of both lists. |
| Writing / Writing Target | World-space TextMeshPro and a dedicated, non-trigger collider on its object or parent. |
| Writing Glow | Optional point Light under Writing Target. Set a low idle intensity. The flash temporarily multiplies it. |
| Keyhole Target | Dedicated non-trigger collider on the keyhole root, with the art underneath it. |
| On Keyhole Clicked | Connect a locked-message response or a future lamp-key handler. The playtest binds this to its locked message. |

Disable both target GameObjects in the authored scene. Place their collider faces slightly into the cell from the wall so the wall collider sits behind them. Remove colliders on child art that could intercept clicks. Keep both targets on layers included in default physics raycasts. Start with a 3 m interaction distance.

A configured light counts as on when its object and component are active and its intensity is positive. Use one Light per independently controlled color. On a lever, assign that Light to **Lever Light**. For a binary switch that changes one indicator between two colors, wire separate room lights with your circuit logic. The clue reads each referenced room light's on/off state. Use a 3D forward renderer on the cell camera so the room lights illuminate the wall. The generated camera selects **LightingPlaytestRenderer** in renderer slot 2.

Discovery is saved when the player clicks the writing. Scene reload restores the keyhole directly. With Play Mode stopped, open the clue component's context menu and select **Clear Saved Clue Discovery** to clear just that clue's PlayerPrefs entry.

## Playtest checklist

- [ ] Clear the playtest clue's saved discovery. Verify that the writing and keyhole are absent on entry. Aim at the intended writing location and left-click to confirm there is no reveal.
- [ ] Try red alone, blue alone, and all three colors. Verify that the writing remains hidden. The wall should still respond to the room lights.
- [ ] Turn green off with red and blue on. Verify that glowing writing appears. Break the combination and confirm it disappears before discovery.
- [ ] Restore the combination in the reverse switch order. Exit the panel and click the writing within 3 m. Verify the flash and short keyhole growth. Try a click beyond 3 m and a click with an intervening collider before completing discovery.
- [ ] Click the resulting keyhole and check the locked message. Change the lights, including switching all three off. Verify the keyhole remains clickable and the writing stays hidden.
- [ ] Stop and restart Play Mode. Quit and reopen Unity, then regenerate the cell or reopen the saved playtest scene. Verify that the keyhole returns immediately and remains clickable with all three switches off.
- [ ] Clear just the clue's saved discovery again and confirm it starts hidden. In the production cell, check that opening the switch panel, radio, shop or settings blocks clue clicks and that those interactions still work.
