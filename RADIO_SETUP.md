# Radio controls — mouse and keyboard

## Import and connect

1. Import `RadioControls.unitypackage`. It contains radio components, an editor setup tool, and updates to RadioInteractable, SettingsUI, GameStateManager, and CardOfferingController from GitHub main. Existing scripts keep their GUIDs. Merge instead of replacing if your local versions have newer edits.
2. The mesh needs separate objects for the **top-left volume knob**, **top-right AM/FM switch**, and **large bottom tuning knob**. Add an empty parent centered on each control's axis if its mesh origin is off-center. Assign that parent as the control.
3. Create an empty `RadioCloseUp` target where the player camera should sit to view the radio. Face its front. Keep this target under the stationary radio root, not under a rotating control or the camera.
4. Select the radio root and open **Tools > Purgatory > Wire Radio**. Assign the three controls, close-up target, player camera, and player controller. The switch is optional while the new mesh is being prepared: F and arrow keys work without it.
5. Assign a dedicated radio AudioSource and optionally a separate looping static AudioSource. Keep these routed through your existing audio mixer. Static should have a looping clip and Play On Awake enabled if you want it audible from startup.
6. Optionally assign a separate click AudioSource and a short mechanical click clip in the setup window. Each actual knob detent or band change plays a click. This source should not be the transmission or static source, so knob sounds do not interrupt them.
7. Click **Wire radio controls** and save. This disables legacy KnobInteractable components on this radio and old RadioConfirmButton components. The top-right part now changes bands rather than confirming.
8. Inspect colliders on the controls. The tool adds a box when no collider exists. Avoid a body collider sitting in front of the controls and blocking pointer rays.

If updating from the first package, run Wire Radio again so existing knobs receive the slower sensitivity and the old confirm button is disabled. Existing serialized knob values do not change merely by importing updated scripts.

## Controls

| Input | Action |
| --- | --- |
| Click the radio or press E while looking at it | Enter close-up view |
| Click and drag top-left knob | Adjust radio volume |
| Click top-right switch or press F | Toggle AM/FM |
| Click and drag bottom knob | Tune station |
| Up / Down | Cycle Volume -> Band -> Tuning (wraps) |
| Left / Right | Adjust selected control one step |
| Space | Confirm the current station |
| Backspace / Delete | Undo last station / clear attempt |
| W / A / S / D or Escape | Leave radio |

Left selects AM and Right selects FM while Band is selected. Holding an adjustment arrow repeats after 0.35 seconds, then every 0.1 seconds. Repeated selection of the same band does not toggle it. Confirmation always records the tuned station regardless of which control is selected. Enter is no longer the confirmation key. Card-offering keyboard and pointer input is suspended while the radio is active, so Space does not also trigger an altar press.

Movement keys also cancel the initial zoom. Movement resumes once the camera returns; arrow keys stay assigned to the radio and do not exit the view. A click uses the center reticle while the cursor is locked, or the pointer position when unlocked.

## Precise, tactile movement

There is no inertia or animation smoothing. Tuning and its needle snap to 13 positions per band. Volume snaps to 2% steps by default. Rotation stops with the pointer, and sub-step pointer travel accumulates so slow movements still work.

Default Drag Sensitivity is **0.0015**, down from 0.004: about **667 horizontal pixels** cover the full range. One tuning step is about 56 pixels and one 2% volume step is about 13 pixels. Lower sensitivity further if needed. Both knobs have an optional click sound on actual value changes, including keyboard adjustments.

Floating values appear above knobs during dragging and remain for 0.7 seconds afterward. With keyboard selection, the selected knob's value stays visible. Volume shows a percentage; tuning shows Hz. Adjust Readout Offset Pixels, or assign a stationary Readout Anchor under the radio root to position a readout. Avoid parenting this anchor under the rotating mesh if you want it stationary.

Knob rotation defaults to local Z; choose local X or Y in RadioKnob if the mesh needs it. Negative Rotation Degrees reverses visual rotation. The switch has its own local axis and AM/FM angles, defaulting to X and -20/+20 degrees. Set these to suit a rotary or lever-style mesh.

## Code and audio

AM A-M: **440-560 Hz** in increments of 10.
FM N-Z: **570-690 Hz** in increments of 10.

**ONE = FM 580, FM 570, AM 480**, pressing Space after each selection.

These are fictional puzzle frequencies, not actual audio pitch changes. The player readout shows numbers and entry count without revealing letters. A wrong complete code starts over on the next confirmation. Successful entry fires RadioTuner > On Code Accepted once per scene instance, stops static, and plays the optional Transmission clip through Radio Audio. Connect narrative actions through that event. Persistence across reloads remains for the later story implementation.

Radio volume changes only the assigned radio/static sources. Global Master/Music/SFX settings remain in the existing menu.

For an optional sliding dial needle, assign Needle and set Needle Start / End in its parent's local coordinates. Replace frequency artwork separately.

## Verification and first playtest

82 executable checks cover frequency mapping, code handling, slow pointer accumulation, snapping, no drift, reversals, endpoints, and 2% volume steps. The runtime scripts and setup window compile against Unity 6.3 libraries with stand-ins for Input System and existing project interfaces. The active scene and audio have not been playtested.

Check both mouse and keyboard paths: hover and drag each knob, release and reverse, hit both ends, change band, enter ONE using Space, undo a wrong station, and leave during zoom. Verify Space does not offer cards during radio use, volume leaves other sounds alone, and Escape does not also open settings.

Arrow-key controls are implemented now; no later reminder is needed. No changes have been pushed to GitHub.
