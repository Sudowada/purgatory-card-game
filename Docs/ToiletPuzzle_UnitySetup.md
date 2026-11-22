# Purgatory toilet puzzle: click to start, mouse to aim

This update extends the already merged click-to-pee puzzle. **Left-clicking the
toilet** begins the use animation. While the stream is flowing, **move the
mouse** to aim. The player can look around freely but **WASD movement is
temporarily locked**. Neither holding P nor keeping the mouse button held is
required. The change lives in `feature/toilet-mouse-aim-after-click`.

## Scene setup

1. Attach **ToiletPuzzleInteractable** to the toilet's scene root. Enable
   **Auto Add Click Colliders** or provide non-trigger colliders on the
   actual porcelain meshes. **Debug Clicks** will log any blocked clicks
   and the collider hit by the click raycast.
2. If the imported FBX Torus/Cylinder/Cube mesh parts are outside the
   GameObject carrying the script, assign their shared parent into
   **Toilet Model Root**. Otherwise leave it empty.
3. Create **WaterAnchor** (an empty transform) in the bowl at the lowest
   intended water height. Assign it to the script's **Water Anchor** field.
   Leave **Water Surface** empty to get a 12-sided temporary mesh.
   You don't need to create a Water Material yet.
4. Assign the **Player Camera** and **Player Controller** if needed,
   or let the script find Camera.main and FirstPersonController.
5. Optional: provide **Use Animator** (trigger `Use`), Pee/Water/
   Reveal/Pickup audio clips, and a **Vial Prefab**; the puzzle runs with
   the automatic camera dip and placeholder vial if these are empty.

## Interaction flow

- In **Exploration**, **left-click the toilet** once while its mesh is
  under the crosshair or mouse pointer. The click can be from anywhere
  you can see it, provided the click ray reaches the toilet's collider.
- The use animation and pee stream begin **immediately on click**.
- For ~**2.5 seconds**, use the **mouse** to steer the camera and stream.
  Walking is locked, but looking left/right/up/down remains active.
  The stream follows a ballistic arc and can hit the wall, floor or rim.
- The prompt says **HITTING WATER** when the stream is reaching the bowl,
  or **MISSED THE BOWL** when it isn't. Ripple shows on successful hits.
- For each animation to count toward the secret, at least **0.35 seconds
  during that session** must reach the water. If it misses entirely,
  that click does **not** raise the water.
- **Four successful uses** raise the water to its capped maximum, then
  a sealed corked vial floats up. Approach and **left-click or press E**
  within 2.75 m to collect it; it appears in **CollectiblesTable**.
- Each use is automatic once started; no cooldown or battle requirement.
  After an animation finishes, the mouse stays facing wherever the
  player last aimed—no snap back to the original direction.

## Main Inspector controls

| Property | Default | Purpose |
| --- | --- | --- |
| Pee Duration | 2.5s | How long each click activates the stream |
| Required Bowl Hit Seconds | 0.35s | Time on water needed to count as a success |
| Stream Launch Speed | 9.5 m/s | Faster stream reaches further |
| Stream Gravity | 9 m/s² | Curves downward; aim higher farther away |
| Stream Flight Time | 1.6s | Maximum simulated arc duration |
| Bowl Hit Radius X/Z | 0.13/0.16 m | Scoring ellipse at current water surface |
| Camera Dip | 0.035m | Temporary first-person head movement |
| Camera Tilt | 5 degrees | Added pitch, preserving mouse look |
| Uses To Reveal | 4 | Successful pee sessions needed |
| Total Water Rise | 0.10m | Water movement; keep under porcelain rim |

## Play Mode checklist

1. Look at the toilet and left-click.
2. Confirm pee stream starts and camera dips, while mouse movement still
   changes the aim.
3. Move mouse away from the bowl. The stream should hit another surface,
   the ripple disappear, and the status say **MISSED THE BOWL**.
4. Aim into water for at least 0.35s within the same 2.5s session.
   After the animation ends, the water should rise once.
5. Repeat for four successful uses and collect the vial.
6. While using the toilet, press WASD: the player should stay in place.
   After the stream ends, normal movement should return.
7. Verify CardGame, shop, radio, and settings don't allow starting
   new pee interactions.

## Troubleshooting

- **No use animation after clicking:** Enable **Debug Clicks**, check
  `[ToiletPuzzle] Started` and `[ToiletPuzzle] Click ...` messages.
  Confirm WaterAnchor and the correct FBX hierarchy.
- **The stream doesn't reach water even when aimed into the opening:**
  The porcelain's MeshCollider or a large BoxCollider may cover the
  bowl's interior. Adjust its shape or target radius and position.
- **Water does not rise:** A use must finish and accumulate enough
  successful water hit time; enable Debug Clicks for miss logging.
- **Mouse cannot aim:** Make sure the **FirstPersonController** script
  from the same feature branch was updated as well; it adds
  `MovementLocked` and `LookPitchOffset`, allowing looking without walking.
- **Existing assets:** Water surface, stream and vial are temporary procedural
  visuals. Real art, sound and character movement can be fitted later.
- **Testing status:** Source reviewed, but not Unity-compiled or Play Mode
  tested in the user's scene.
