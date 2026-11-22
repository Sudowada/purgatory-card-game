# Purgatory toilet puzzle: click to start, mouse to aim

This update extends the already merged click-to-pee puzzle. **Left-clicking the
toilet** begins the use animation. While the stream is flowing, **move the
mouse** to aim. The player can look around freely but **WASD movement is
temporarily locked**. Neither holding P nor keeping the mouse button held is
required. This click-detection update is on `fix/toilet-shopwindow-click-pattern` until merged into `main`.

## Scene setup

1. Attach **ToiletPuzzleInteractable** to the toilet's scene root.
   Its click detection now uses the shop window's **ScreenPointToRay +
   Physics.Raycast + collider match** pattern, with the center crosshair
   always used for this toilet interaction.
2. In the Inspector, **Interaction Collider** is optional, just like
   ShopWindowInteractable. Assign a specific **non-trigger** collider on
   the toilet body if you want to click one precise target. With that
   field empty, any collider on the toilet root or its children counts.
   **Auto Add Click Colliders** creates missing MeshColliders on imported
   porcelain parts only in this fallback mode. You do NOT have to
   hand-create a collider if the FBX parts are covered by that option.
   Avoid broad colliders covering the bowl opening because they can also
   block the pee stream from reaching the water.
3. If the imported FBX Torus/Cylinder/Cube mesh parts are outside the
   GameObject carrying the script, assign their shared parent into
   **Toilet Model Root** (fallback matching). Otherwise leave it empty.
   **Debug Clicks** defaults to ON, and reports the clicked collider,
   distance, or blocker under a `[ToiletClick]` Console prefix.
4. Create **WaterAnchor** (an empty transform) in the bowl at the lowest
   intended water height. Assign it to the script's **Water Anchor** field.
   Leave **Water Surface** empty to get a 12-sided temporary mesh.
   You don't need to create a Water Material yet.
5. Assign the **Player Camera** and **Player Controller** if needed,
   or let the script find Camera.main and FirstPersonController.
6. Optional: provide **Use Animator** (trigger `Use`), Pee/Water/
   Reveal/Pickup audio clips, and a **Vial Prefab**; the puzzle runs with
   the automatic camera dip and placeholder vial if these are empty.

## Interaction flow

- In **Exploration**, put the **fixed crosshair at the center of the screen**
  over any toilet mesh and **left-click once**. The actual mouse cursor
  position does not matter, even when the cursor is unlocked.
  You can click from anywhere the toilet is visible and unobstructed,
  provided the crosshair ray reaches its collider.
- The **use/camera animation starts immediately on click**, but the actual
  pee stream and pee audio start **0.4 seconds afterward** by default.
  This lead-in is adjustable using **Pee Start Delay** in the Inspector.
  Moving the mouse continues to aim the stream once it appears; no more
  clicks or held keys are needed.
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
| Pee Duration | 2.5s | Total use-animation time from initial click |
| Pee Start Delay | 0.4s | Time from use-animation start to visible pee/audio; clamped below duration |
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

1. Put the fixed center crosshair on the toilet and left-click.
2. Confirm the use/camera animation begins immediately. About **0.4s later**,
   the stream and pee audio begin; mouse movement should change the aim.
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

- **Console says `[ToiletPuzzle] Click blocked: GameManager.IsInputBlocked is true`:**
  That was an overly strict check. In Exploration mode, the game can keep
  card gameplay input locked while room interactions are allowed, as it
  already does for the shop window. The toilet no longer blocks on that flag
  and will not end a pee animation because of it. Radio, shop, settings,
  controller and game-state restrictions remain.
- **Interaction Collider shows `Toilet (Mesh Collider)`, but that collider
  has `Mesh: None` in the Inspector:** An empty MeshCollider cannot be
  raycasted. The toilet now warns and falls back to root/child detection,
  repairing empty colliders on actual MeshFilter objects or adding missing
  colliders when **Auto Add Click Colliders** is on. For simplest testing,
  clear **Interaction Collider** to None, keep **Auto Add Click Colliders**
  ON, and leave **Toilet Model Root** assigned to the imported model parent.
  You can instead assign a valid non-trigger mesh/box collider if desired.
- **No use animation after clicking:** Enable **Debug Clicks**, check
  `[ToiletPuzzle] Started` and `[ToiletClick] Interaction input received`
  messages. Confirm WaterAnchor and the correct FBX hierarchy.
- **The stream doesn't reach water even when aimed into the opening:**
  The porcelain's MeshCollider or a large BoxCollider may cover the
  bowl's interior. Adjust its shape or target radius and position.
- **The click is detected but there's no stream immediately:** That is
  expected for the first **Pee Start Delay** seconds. With Debug Clicks
  enabled, look for `Use animation started` and then `Pee stream started`
  in the Console. The stream only appears once the lead-in finishes.
- **Water does not rise:** A use must finish and accumulate enough
  successful water hit time *after the stream starts*; enable Debug Clicks
  for miss logging.
- **Mouse cannot aim:** Make sure the **FirstPersonController** script
  from the same feature branch was updated as well; it adds
  `MovementLocked` and `LookPitchOffset`, allowing looking without walking.
- **Existing assets:** Water surface, stream and vial are temporary procedural
  visuals. Real art, sound and character movement can be fitted later.
- **Testing status:** Source reviewed, but not Unity-compiled or Play Mode
  tested in the user's scene.
