# Switch interaction setup and playtest

## Branch and scope
This change starts from main at fa990f3968267f4daac5d4f9392c51c984aba2c0 on feature/free-look-switch-interaction. The enemy-attack branch is separate work. FirstPersonController, GameStateManager, WorldInteractable, radio/knob controls, sequence managers, and existing script meta GUIDs are unchanged.

SwitchPanelInteract no longer zooms, disables the player controller, unlocks the cursor, sways the camera, or re-locks the view. Existing E input now directly activates the aimed switch. PuzzleSwitch accepts direct left clicks and deliberate vertical drags while normal camera look and movement continue. There is no panel mode to enter or exit.

## Unity setup
1. Open the project in Unity 2022.3.16f1 (ProjectSettings/ProjectVersion.txt), or your separately upgraded project version.
2. The committed Packages/manifest.json does not declare com.unity.inputsystem despite existing scripts using it. Ensure Input System is installed through Package Manager, and Active Input Handling includes the new Input System. This is an existing repository dependency gap; no package version is guessed by this change.
3. In your puzzle scene, keep SwitchPanelInteract enabled. Retain its switches array, playerCamera, playerController, and interactDistance assignments. Camera/controller references fall back to Camera.main and its parent FirstPersonController. Assign explicitly if your camera is not under the controller.
4. Each switch needs an enabled PuzzleSwitch, its existing handle/lights/audio/Morse references, and a collider on itself or a child. Keep that collider outside the Ignore Raycast layer. Retain sequence and color-puzzle registrations and solved-event wiring.
5. Preserve existing prompt objects and WorldInteractable components. E remains usable; left click is an additional direct interaction. Old panel zoom/parallax/free-look settings are removed and no longer needed. Keep other puzzle controls as configured.
6. Interaction range is the smaller of the switch's range (default 3 m) and its panel's range (default 5 m). A standalone PuzzleSwitch can also work using its camera/controller fallback.
7. The four committed gameplay scenes contain neither PuzzleSwitch nor SwitchPanelInteract script GUIDs. Use your actual puzzle scene, or build a fixture with the components and sequence wiring above. This commit cannot validate scene wiring that is absent from GitHub.

## Controls and tuning
- Aim the center reticle at a switch, then left click. Release over that same switch with total mouse travel at most 6 pixels to toggle once.
- For a drag, press on the switch, pause at least 0.15 seconds, then move mostly vertically at least 12 pixels. Release to use the existing nearest-state snapping, including the middle/Morse state.
- Drag movement previews the handle; puzzle callbacks occur only on release. A long hold alone does not become a drag.
- Right mouse, Escape, more than 12 pixels of net horizontal movement, a rotation over 45 degrees from capture, loss of focus, component disable, leaving exploration, or losing the enabled player controller cancels without puzzle callbacks. Looking away during a click prevents it from toggling on release.
- Look and WASD remain active during both capture and drag. Vertical dragging therefore also changes pitch. No mouse delta is consumed or suppressed by the switches.
- At default mouse sensitivity 2 and drag sensitivity 1.2, a 180-degree handle travel takes 150 pixels and rotates camera pitch about 30 degrees, within the 45-degree limit. Tune dragSensitivity and lookCancelAngle together if your mouse sensitivity or handle range differs. Horizontal turning cancels early so it does not operate the switch.
- Input is processed in LateUpdate, after FirstPersonController.Update. Only one switch owns a left-button gesture. Card mode, unlocked cursor modes, disabled controllers and UI pointer capture block switch input.
- ForceExit only cancels pending gestures; it does not restore an old camera pose or overwrite another interaction's cursor/controller state.

## Play Mode checks (required)
1. Click each switch, including child colliders, and use E: exactly one activation per press, existing sounds/lights/animations, no zoom or camera lock.
2. Hold without moving and release: one click. Add less than 6 pixels of jitter: still one click.
3. Press, immediately swipe horizontally or vertically, release: no activation. Press empty space and sweep across switches: no activation. Press one switch and release over another: no click on either.
4. Hold for 0.15 s, drag vertically beyond 12 pixels, release near each endpoint and middle. Confirm lever/binary states, Morse start/stop, and one existing puzzle notification path on release. Test both upAngle < downAngle and reversed ranges if used.
5. During a pending click or drag, turn sideways, press right mouse, press Escape, walk out of range, disable the panel/switch, or lose application focus. Confirm preview restores, state/progress is unchanged, and a fresh click works afterward.
6. While looking around normally, including turning 180 degrees away and back, confirm no switch changes and no re-lock. While dragging, confirm pitch/WASD still respond.
7. Enter card mode, radio zoom, shop view, and UI overlays while holding left mouse. Confirm cancellation and no release-triggered switch operation. Verify radio knobs, other puzzles, table prompts and their existing E/Escape controls.
8. Complete correct sequence and color sequence; try wrong order and wait for reset; solve and reload. Verify saved progress, solved events, lights and Morse match prior behavior. Use the existing Reset Puzzle Progress context menu for a disposable test save; do not erase a player's save.
9. Repeat at low/high frame rates, multiple mouse sensitivities, and with rapid clicks during snapping. New switch gestures are blocked during snap animation.

## Validation performed and limitations
- Compiled the modified scripts plus the repository FirstPersonController with the installed Unity 2022.3.16f1 C# compiler and real Unity engine/UI assemblies. Input System and game-specific dependencies were stubbed for this isolated compile, so it confirms C# syntax/API usage against those engine assemblies, not a full project build.
- Compared eight existing switch state, animation, visual and Morse methods verbatim against main; their puzzle callbacks and snapping behavior are preserved. SnapTo additionally records its destination so disabling a switch can finish an accepted snap and clear its input guard. Sequence manager scripts and persistence logic are unchanged.
- Inspected camera ownership in FirstPersonController, GameStateManager, SwitchPanelInteract, and RadioInteractable, plus WorldInteractable prompt logic and KnobInteractable drag input.
- No Unity Play Mode, physics/input simulation, scene wiring, audiovisual verification, or full-project compilation was performed. The local checkout could not be cloned because command-line Git requires missing Xcode developer tools; repository changes were committed through the GitHub connector. The missing Input System manifest dependency and absent committed switch scene also limit reproducible full-project testing.
