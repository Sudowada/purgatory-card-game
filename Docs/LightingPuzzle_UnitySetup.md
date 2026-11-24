# Lighting puzzles

## Wire four installed switches in a cell

1. Open the cell scene. Put an empty, unit-scale anchor at about eye height on the inside of the clue wall. Point its local negative Z into the room.
2. Open **Tools → Purgatory → Wire Lighting Puzzle**. Assign the first-person camera, the wall anchor, and the four imported model roots and four distinct active room Light objects. Use a 3D forward renderer on the cell camera so the lights illuminate the wall.
3. Use **Red**, **Blue**, **Green/Yellow**, and **Magenta/Orange** for fixture slots 1–4. The importer identifies the left selector as `Light_Color` and the power paddle as `OnOff`. A single model may use either mesh name for its lone power paddle. Choose the model roots without legacy switch input.
4. Click **Wire Switches and Wall Clue** and save the scene. If a Light Sequence Panel is already installed, use **Add Wall Clue** to keep its current configuration. For an already wired scene, choose **Apply First Puzzle Answer** in the same window, then save. A configured reward door is optional. Without one, sequence completion is visible in the Console.

The default activation order is **red → yellow → magenta → blue**. Only power-on inputs enter the sequence. On the double fixtures, the left rocker sets the lamp's color and the right rocker switches power. A wrong light or wrong color resets the step counter. The player can turn off the lamps to restart from a clean state.

The independent wall clue needs red and blue on and both double fixtures off. Click the writing to flash it and reveal a keyhole. The keyhole stays visible after the lights go off and after a reload. Clicking it displays **A key is needed** and exposes an Inspector event for the future lamp key.

Correct steps, wrong inputs, and completion briefly pulse the crosshair in different colors. In an existing scene, choose **Add Puzzle Sounds** in the same window to assign the three existing sound effects, then save the scene. You can replace the clips or adjust the volume in the Inspector.

To playtest, first try blue out of order, then reset the switches and enter the correct sequence. For the wall clue, check red alone, blue alone, red and blue together, and red and blue with an extra light. Discover the keyhole, switch both lights off, and reload the scene to check persistence.

## Play the sample

1. Open the project in Unity 2022.3.16f1. The project uses **Both** under **Player → Active Input Handling**.
2. Choose **Tools → Purgatory → Open Lighting Puzzle Playtest**, then enter Play Mode. Walk with WASD and aim with the mouse.
3. Aim at the right rocker. The faint center dot grows and brightens when the target is usable. Left-click to power on the blue light. Aim at the left rocker and left-click to select violet.
4. Look right for the writing. Aim at it and left-click to flash the text and grow the wall keyhole. Click the keyhole to see **A key is needed.**

The sample uses its own discovery save. In Edit Mode, select the clue component and choose **Clear Saved Clue Discovery** from its context menu to replay from the beginning.

## Install in the scene you use

Create an empty, unit-scale anchor about 1.45 m above the floor on the inside wall face, with its local −Z pointing into the room. Select it and run **Tools → Purgatory → Add Two-Rocker Puzzle at Selected Wall Anchor**. The command adds the wired switch panel and clue to the open scene. It places the writing 0.8 m to the right of the anchor. Move the clue and lamp as needed so the light covers the writing and both click targets sit just in front of the wall collider. Save the scene.

The tool uses the existing first-person camera. It needs a 3D forward renderer for the lamp to illuminate the room. The sample camera uses **LightingPlaytestRenderer** in renderer slot 2.

| Object or component | Inspector setup |
| --- | --- |
| SwitchPanelInteract | Assign the player camera, first-person controller and both switches. Enable **Use World Interaction**. |
| Left rocker | **PuzzleSwitch** set to **Binary**, **Click Only** enabled. **Binary Up Color** starts blue and **Binary Down Color** is violet. |
| Right rocker | **PuzzleSwitch** set to **Lever**, **Click Only** enabled. It starts off. |
| TwoRockerLightCircuit | Assign **Color Rocker**, **Power Rocker**, and **Room Light**. The circuit controls this light. Leave the individual switches' light outputs empty. |
| LightConditionClue | **Required Colors** contains the room light and the left rocker's violet color. The lamp must be active with positive intensity. Other clues can still use required or forbidden lights and colors. |
| On Keyhole Clicked | The starter shows a locked message. This event can be wired to the future lamp-key interaction. |

### Replace the starter geometry in Blender

The mounting plate is **30 × 20 cm**. Each rocker face is **10 × 13 cm**, and the faces have a **3 cm** gap. The stationary colliders are **12 × 15 cm** each with a **1 cm** gap. Keep each moving mesh under its switch's **Lever Handle** pivot and align the pivot's local Y with the hinge. Keep the existing colliders when swapping meshes. The generated switches have a 2.5 m interaction range.

## Playtest checklist

- [ ] Aim at the space between the rockers, at each face, and at each face beyond 2.5 m. The crosshair brightens only over an in-range rocker. Verify both faces are easy to select from 1.5–2 m.
- [ ] With power off, click the left rocker. The paddle latches into violet without lighting the room. Power on and verify the lamp turns violet immediately. Toggle power off and back on and verify it retains the selected color.
- [ ] Verify that power off and power on with blue selected both hide the writing. Select violet with the lamp on to reveal it. Toggle blue and violet before discovery to confirm the clue follows the lamp.
- [ ] Hold left-click or move the mouse during a press. Verify one flip per press, with normal mouse look and no zoom.
- [ ] Aim at the writing and click. Verify the text flashes and the keyhole becomes clickable once its animation completes. Check the locked message and verify the keyhole still works with the light off.
- [ ] Restart the scene and then restart the game. Verify the keyhole returns without replaying the animation. Clear that clue's saved discovery in Edit Mode to verify the original state.
- [ ] In the cell scene, verify existing switch sequences, radio, shop, and settings still work. Over an obstructed switch or clue, verify there is no highlight or interaction.
