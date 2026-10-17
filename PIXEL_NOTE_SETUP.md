# Pixel note: Unity setup and testing

## What was added
PixelNoteInteractable extends the existing WorldInteractable, retaining its Read icon and E prompt and adding a deliberate left click from the center reticle. It opens an enlarged paper overlay with Pixelify Sans text and a dimmed backdrop. While reading, the exploration controller is paused and the cursor is unlocked. Escape, right click or clicking outside the paper closes it. The camera transform is never changed.

This is a fresh note asset, not an exact reproduction of the missing original. The default text is a proposed clue:
> Watch the room, not the switches.
>
> Follow the lights.
> Remember their order.

Replace Note Text with the original wording when found. It is kept separate from the paper texture so editing a clue does not require regenerating artwork. No switch order, puzzle registration, save keys or solved events are changed.

## Add it in Unity
1. Use the feature/free-look-switch-interaction branch. The new component is Assets/Scripts/PixelNoteInteractable.cs; existing switch changes remain on this branch.
2. Create an empty GameObject named Switch Puzzle Note and add PixelNoteInteractable. SpriteRenderer and BoxCollider are added automatically. A small point-filtered pixel paper sprite is generated if no Paper Sprite is supplied, so the component works without importing the optional image.
3. Position and rotate the note against a wall or flat on a table. Its local XY plane is the paper surface. Start with scale (0.3, 0.3, 0.3). Keep its BoxCollider enabled and outside Ignore Raycast; adjust it to the paper if you replace the sprite or change its bounds.
4. Assign the player FirstPersonController explicitly, or keep the main camera beneath it so the component can find it. Camera.main must be the exploration camera. Keep GameStateManager in the scene.
5. Set Note Title and Note Text. The default draft clue is editable. Pixel Font automatically loads the existing Resources/Fonts/PixelifySans-Regular SDF; assign another TMP font if desired.
6. Optional generated artwork: import the supplied PixelNotePaper.png into Assets/Art. Set Texture Type = Sprite (2D and UI), Sprite Mode = Single, Filter Mode = Point (no filter), Compression = None, Generate Mip Maps = off, Alpha Is Transparency = on, and Pixels Per Unit = 1000. Apply, then assign the sprite to Paper Sprite. For custom artwork also assign it to SpriteRenderer to preview it outside Play Mode. Adjust world size/collider after importing. The generated PNG is delivered separately; it is not embedded in the GitHub commit.
7. Keep or create the scene's EventSystem with InputSystemUIInputModule for mouse-wheel scrolling of long notes. The project uses Input System in its scripts but currently omits the package from its manifest; install it through Package Manager if necessary and enable new Input System input handling. Closing through Escape/right click/outside click is handled directly and does not depend on a UI button.
8. Existing Read icon and E prompt work through WorldInteractable. No additional note canvas, view-point object, animation or switch-puzzle wiring is required.

## Check in Play Mode
- Aim at the note within Interact Distance (default 3.5 m). Left click and release without moving more than 6 pixels: one focused view opens. E should also open it.
- Sweep the mouse while clicking, click empty space, release over a different object, or stand behind an occluder: the note must not open.
- Confirm enlarged paper, legible pixel font, line breaks and clue text. Test 16:9, 4:3 and a narrow window; the paper should remain inside the viewport. For long notes, scroll and confirm the last line is reachable.
- While reading, WASD/look and switch inputs must not operate. Click inside the paper to keep reading; click outside, right click or Escape to close.
- Close while pointing at a switch: no switch should toggle from the closing input. Camera pose must match the pose when reading began; normal movement and look resume immediately.
- Reopen repeatedly. Disable/destroy the note or lose window focus while reading; verify the overlay closes and exploration controls recover.
- Transition to card mode while reading (the existing C debug key can exercise this): the note must close without re-enabling exploration controls or restoring a locked cursor over card mode.
- Confirm existing switch, table, radio and other puzzle prompts/controls still work.

## Validation and limits
The new component and the real WorldInteractable/FirstPersonController compiled against installed Unity 2022.3.16f1 engine, UI and TextMeshPro assemblies. Input System and GameStateManager were stubbed for the isolated compile; this is not a full-project build or Play Mode test.

The generated paper was visually inspected. The committed scenes do not contain the switch puzzle setup, so the actual scene, collider placement, modal transitions, font rendering, long-note scrolling and input must be verified in Unity using the checklist above. Other modal systems should transition GameStateManager or close this note before taking controller ownership.

## Artwork provenance
Created with the built-in imagegen tool. Prompt: a single blank aged portrait paper note, orthographic front view, transparent background, large crisp pixel clusters, stair-stepped torn edges, restrained bone/ochre/brown palette, worn margins, subtle folds, broad clean pale center, no text or symbols. Output: PixelNotePaper.png.
